using Ifrs9Simulator.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ifrs9Simulator.Api.Data;

public static class DbInitializer
{
    public const int LoanCount = 5000;
    public const int SeedValue = 42;

    public static async Task InitializeAsync(
        Ifrs9DbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

        if (await context.Loans.AsNoTracking().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already contains seeded loan records. Skipping initialization.");
            return;
        }

        logger.LogInformation("Seeding {Count} dummy loans with reproducible seed {Seed}...", LoanCount, SeedValue);

        var random = new Random(SeedValue);
        var loans = new List<Loan>(LoanCount);

        for (int i = 0; i < LoanCount; i++)
        {
            // EAD: $5,000 to $55,000
            double ead = Math.Round(5000.0 + random.NextDouble() * 50000.0, 2);

            // LGD: 20% to 60% (0.20 to 0.60)
            double lgd = Math.Round(0.20 + random.NextDouble() * 0.40, 4);

            // BasePd12m: 0.5% to 3.5% (0.005 to 0.035)
            double basePd12m = Math.Round(0.005 + random.NextDouble() * 0.030, 5);

            // BasePdLifetime: 1.5x to 3.5x higher than 12m PD
            double multiplier = 1.5 + random.NextDouble() * 2.0;
            double basePdLifetime = Math.Round(Math.Min(0.9999, basePd12m * multiplier), 5);

            loans.Add(new Loan
            {
                Id = Guid.NewGuid(),
                Ead = ead,
                Lgd = lgd,
                BasePd12m = basePd12m,
                BasePdLifetime = basePdLifetime
            });
        }

        await context.Loans.AddRangeAsync(loans, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Successfully persisted {Count} seeded loan facilities to SQLite database.", LoanCount);
    }
}
