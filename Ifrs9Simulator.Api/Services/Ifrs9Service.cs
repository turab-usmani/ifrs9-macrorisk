using Ifrs9Simulator.Api.Configuration;
using Ifrs9Simulator.Api.Data;
using Ifrs9Simulator.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ifrs9Simulator.Api.Services;

/// <summary>
/// Core quantitative engine implementing IFRS 9 impairment calculation,
/// logit-based macroeconomic shock transmission, and staging migration (Stage 1, 2, 3).
/// </summary>
public class Ifrs9Service : IIfrs9Service
{
    private const double MinPd = 1e-9;
    private const double MaxPd = 1.0 - 1e-9;

    private readonly Ifrs9DbContext _dbContext;
    private readonly Ifrs9Settings _settings;
    private readonly ILogger<Ifrs9Service> _logger;

    public Ifrs9Service(
        Ifrs9DbContext dbContext,
        IOptions<Ifrs9Settings> options,
        ILogger<Ifrs9Service> logger)
    {
        _dbContext = dbContext;
        _settings = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public double CalculateStressedPd(double basePd, double deltaUnemployment, double deltaInflation)
    {
        // 1. Clamp baseline PD to [1e-9, 1 - 1e-9] to strictly prevent log(0) or division by zero
        double clampedPd = Math.Clamp(basePd, MinPd, MaxPd);

        // 2. Compute baseline log-odds (logit)
        double baselineLogit = Math.Log(clampedPd / (1.0 - clampedPd));

        // 3. Apply macro shock: Stressed Logit = Logit + (BetaUnemployment * DeltaUnemp) + (BetaInflation * DeltaInfl)
        double macroShock = (_settings.BetaUnemployment * deltaUnemployment) + (_settings.BetaInflation * deltaInflation);
        double stressedLogit = baselineLogit + macroShock;

        // 4. Invert logit: Stressed PD = 1 / (1 + exp(-Stressed Logit))
        // Protect against numeric overflow for extreme negative exponents
        double stressedPd = 1.0 / (1.0 + Math.Exp(-stressedLogit));

        // 5. Strictly clamp output to [MinPd, MaxPd] so probabilities never exceed 1.0 or drop below 0.0
        return Math.Clamp(stressedPd, MinPd, MaxPd);
    }

    /// <inheritdoc />
    public async Task<SimulationResponse> SimulateAsync(
        StressTestRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing IFRS 9 simulation with DeltaUnemployment={DeltaUnemployment:F2}pp, DeltaInflation={DeltaInflation:F2}pp",
            request.DeltaUnemployment,
            request.DeltaInflation);

        // Query database strictly using AsNoTracking() and passing CancellationToken down the stack
        var loans = await _dbContext.Loans
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        int totalLoans = loans.Count;
        double totalBaselineEcl = 0.0;
        double totalStressedEcl = 0.0;

        int stage1Count = 0;
        int stage2Count = 0;
        int stage3Count = 0;

        double stage1Ecl = 0.0;
        double stage2Ecl = 0.0;
        double stage3Ecl = 0.0;

        foreach (var loan in loans)
        {
            // Baseline ECL = BasePd12m * LGD * EAD for all loans
            double baselineEcl = loan.BasePd12m * loan.Lgd * loan.Ead;
            totalBaselineEcl += baselineEcl;

            // Compute Stressed 12m PD
            double stressedPd12m = CalculateStressedPd(loan.BasePd12m, request.DeltaUnemployment, request.DeltaInflation);

            // Compute Stressed Lifetime PD (enforcing StressedPdLifetime >= StressedPd12m)
            double stressedPdLifetime = CalculateStressedPd(loan.BasePdLifetime, request.DeltaUnemployment, request.DeltaInflation);
            stressedPdLifetime = Math.Min(1.0, Math.Max(stressedPdLifetime, stressedPd12m));

            // Staging Evaluation:
            // 1. Stage 3: StressedPd12m >= Stage3Threshold (e.g. 20%) -> Defaulted, ECL = 1.0 * LGD * EAD
            if (stressedPd12m >= _settings.Stage3Threshold)
            {
                double ecl = 1.0 * loan.Lgd * loan.Ead;
                stage3Count++;
                stage3Ecl += ecl;
                totalStressedEcl += ecl;
            }
            // 2. Stage 2 (SICR): StressedPd12m / BasePd12m >= SicrMultiplier (e.g. 2.0x) -> Lifetime ECL
            else if ((stressedPd12m / loan.BasePd12m) >= _settings.SicrMultiplier)
            {
                double ecl = stressedPdLifetime * loan.Lgd * loan.Ead;
                stage2Count++;
                stage2Ecl += ecl;
                totalStressedEcl += ecl;
            }
            // 3. Stage 1: Standard performing -> 12-month ECL
            else
            {
                double ecl = stressedPd12m * loan.Lgd * loan.Ead;
                stage1Count++;
                stage1Ecl += ecl;
                totalStressedEcl += ecl;
            }
        }

        double eclDelta = totalStressedEcl - totalBaselineEcl;

        _logger.LogInformation(
            "Simulation completed for {TotalLoans} facilities. Baseline ECL: ${BaselineEcl:N2}, Stressed ECL: ${StressedEcl:N2}, Delta: ${EclDelta:N2}. Stages (S1={S1}, S2={S2}, S3={S3})",
            totalLoans,
            totalBaselineEcl,
            totalStressedEcl,
            eclDelta,
            stage1Count,
            stage2Count,
            stage3Count);

        return new SimulationResponse(
            TotalBaselineEcl: Math.Round(totalBaselineEcl, 2),
            TotalStressedEcl: Math.Round(totalStressedEcl, 2),
            EclDelta: Math.Round(eclDelta, 2),
            Stage1Count: stage1Count,
            Stage2Count: stage2Count,
            Stage3Count: stage3Count,
            TotalLoans: totalLoans,
            Stage1Ecl: Math.Round(stage1Ecl, 2),
            Stage2Ecl: Math.Round(stage2Ecl, 2),
            Stage3Ecl: Math.Round(stage3Ecl, 2)
        );
    }
}
