namespace Ifrs9Simulator.Api.DTOs;

/// <summary>
/// Portfolio-level outcome of an IFRS 9 macro-shock credit risk simulation.
/// </summary>
/// <param name="TotalBaselineEcl">Total Expected Credit Loss under baseline economic conditions ($).</param>
/// <param name="TotalStressedEcl">Total Expected Credit Loss under stressed macroeconomic conditions ($).</param>
/// <param name="EclDelta">Net ECL impairment increase ($) [TotalStressedEcl - TotalBaselineEcl].</param>
/// <param name="Stage1Count">Number of loans in Stage 1 (12-month ECL).</param>
/// <param name="Stage2Count">Number of loans in Stage 2 (Lifetime ECL due to SICR).</param>
/// <param name="Stage3Count">Number of loans in Stage 3 (Credit-impaired default, 100% PD).</param>
/// <param name="TotalLoans">Total loans evaluated in portfolio.</param>
/// <param name="Stage1Ecl">ECL allocated to Stage 1 facilities ($).</param>
/// <param name="Stage2Ecl">ECL allocated to Stage 2 facilities ($).</param>
/// <param name="Stage3Ecl">ECL allocated to Stage 3 facilities ($).</param>
public sealed record SimulationResponse(
    double TotalBaselineEcl,
    double TotalStressedEcl,
    double EclDelta,
    int Stage1Count,
    int Stage2Count,
    int Stage3Count,
    int TotalLoans,
    double Stage1Ecl,
    double Stage2Ecl,
    double Stage3Ecl
);
