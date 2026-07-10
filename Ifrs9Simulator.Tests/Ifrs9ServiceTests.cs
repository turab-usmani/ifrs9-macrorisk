using Ifrs9Simulator.Api.Configuration;
using Ifrs9Simulator.Api.Data;
using Ifrs9Simulator.Api.DTOs;
using Ifrs9Simulator.Api.Models;
using Ifrs9Simulator.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ifrs9Simulator.Tests;

public class Ifrs9ServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<Ifrs9DbContext> _dbOptions;
    private readonly IOptions<Ifrs9Settings> _defaultSettings;

    public Ifrs9ServiceTests()
    {
        // Set up in-memory SQLite connection that persists for the test lifetime
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<Ifrs9DbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new Ifrs9DbContext(_dbOptions);
        context.Database.EnsureCreated();

        _defaultSettings = Options.Create(new Ifrs9Settings
        {
            BetaUnemployment = 0.25,
            BetaInflation = 0.10,
            Stage3Threshold = 0.20,
            SicrMultiplier = 2.0
        });
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CalculateStressedPd_WithExtremeMacroShock_StrictlyBoundsBelowOne()
    {
        // Arrange
        using var context = new Ifrs9DbContext(_dbOptions);
        var service = new Ifrs9Service(context, _defaultSettings, NullLogger<Ifrs9Service>.Instance);
        double basePd = 0.05; // 5% base PD
        double impossibleUnemploymentShock = 100.0; // +100% macro shock
        double zeroInflation = 0.0;

        // Act
        double stressedPd = service.CalculateStressedPd(basePd, impossibleUnemploymentShock, zeroInflation);

        // Assert
        // In linear models: 0.05 + (0.25 * 100) = 25.05 (2,505% probability)
        // In logit space: Stressed Logit = ln(0.05/0.95) + 25.0 = 22.055
        // Stressed PD = 1 / (1 + exp(-22.055)) = ~0.9999999997...
        Assert.True(stressedPd < 1.0, $"Stressed PD ({stressedPd}) must be strictly bounded below 1.0");
        Assert.True(stressedPd > 0.99, $"Stressed PD ({stressedPd}) should reflect severe stress (> 0.99)");
        Assert.False(double.IsInfinity(stressedPd), "Stressed PD must not be infinite");
        Assert.False(double.IsNaN(stressedPd), "Stressed PD must not be NaN");

        // Even under absurd, catastrophic inputs (+10,000 pp), the probability remains strictly bounded below 1.0
        double absurdStressedPd = service.CalculateStressedPd(basePd, 10000.0, 10000.0);
        Assert.True(absurdStressedPd < 1.0, $"Absurdly stressed PD ({absurdStressedPd}) must remain < 1.0");
    }

    [Fact]
    public async Task SimulateAsync_WithModerateShock_TriggersStage2SicrMigration()
    {
        // Arrange
        using var context = new Ifrs9DbContext(_dbOptions);
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            Ead = 10000.0,
            Lgd = 0.40,
            BasePd12m = 0.01,       // 1.0% Base 12m PD
            BasePdLifetime = 0.035  // 3.5% Base Lifetime PD
        };
        await context.Loans.AddAsync(loan);
        await context.SaveChangesAsync();

        var service = new Ifrs9Service(context, _defaultSettings, NullLogger<Ifrs9Service>.Instance);

        // Moderate shock: +3.5 pp unemployment shock.
        // Base logit = ln(0.01 / 0.99) = -4.5951
        // Macro shock = 0.25 * 3.5 = +0.875
        // Stressed logit = -3.7201 -> Stressed 12m PD = ~0.0236 (2.36%)
        // Ratio = 0.0236 / 0.01 = 2.36x, which exceeds the 2.0x SICR multiplier!
        // But Stressed 12m PD (2.36%) < 20% (Stage 3 threshold).
        var request = new StressTestRequest(DeltaUnemployment: 3.5, DeltaInflation: 0.0);

        // Act
        var response = await service.SimulateAsync(request);

        // Assert
        Assert.Equal(1, response.TotalLoans);
        Assert.Equal(0, response.Stage1Count);
        Assert.Equal(1, response.Stage2Count);
        Assert.Equal(0, response.Stage3Count);

        // Stressed lifetime PD calculation:
        // Lifetime logit = ln(0.035 / 0.965) = -3.3178
        // Stressed lifetime logit = -3.3178 + 0.875 = -2.4428
        // Stressed Lifetime PD = ~0.07996
        // Stressed ECL should use Stressed Lifetime PD * LGD * EAD
        double expectedLifetimeEcl = service.CalculateStressedPd(loan.BasePdLifetime, 3.5, 0.0) * loan.Lgd * loan.Ead;
        Assert.Equal(Math.Round(expectedLifetimeEcl, 2), response.TotalStressedEcl);
        Assert.True(response.EclDelta > 0, "ECL delta should be positive under stress");
    }

    [Fact]
    public async Task SimulateAsync_WithSevereShock_TriggersStage3Default()
    {
        // Arrange
        using var context = new Ifrs9DbContext(_dbOptions);
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            Ead = 20000.0,
            Lgd = 0.50,
            BasePd12m = 0.03, // 3.0%
            BasePdLifetime = 0.08
        };
        await context.Loans.AddAsync(loan);
        await context.SaveChangesAsync();

        var service = new Ifrs9Service(context, _defaultSettings, NullLogger<Ifrs9Service>.Instance);

        // Severe shock: +8.5 pp unemployment + 5.0 pp inflation
        // Stressed 12m PD will exceed 20% (0.20), triggering Stage 3
        var request = new StressTestRequest(DeltaUnemployment: 8.5, DeltaInflation: 5.0);

        // Act
        var response = await service.SimulateAsync(request);

        // Assert
        Assert.Equal(1, response.TotalLoans);
        Assert.Equal(0, response.Stage1Count);
        Assert.Equal(0, response.Stage2Count);
        Assert.Equal(1, response.Stage3Count);

        // In Stage 3: ECL = 1.0 * LGD * EAD = 1.0 * 0.50 * 20000 = 10,000.00
        double expectedStage3Ecl = 1.0 * loan.Lgd * loan.Ead;
        Assert.Equal(expectedStage3Ecl, response.TotalStressedEcl);
    }

    [Fact]
    public void CalculateStressedPd_WithExtremeDeflationaryShock_StrictlyBoundsAboveZero()
    {
        // Arrange
        using var context = new Ifrs9DbContext(_dbOptions);
        var service = new Ifrs9Service(context, _defaultSettings, NullLogger<Ifrs9Service>.Instance);
        double basePd = 0.01;

        // Act - extreme negative macroeconomic shock
        double stressedPd = service.CalculateStressedPd(basePd, -50.0, -50.0);

        // Assert
        Assert.True(stressedPd > 0.0, "Stressed PD must remain strictly positive (> 0.0)");
        Assert.True(stressedPd < basePd, "Negative economic stress should reduce PD");
        Assert.False(double.IsNaN(stressedPd), "Stressed PD must not be NaN");
    }
}
