namespace Ifrs9Simulator.Api.Configuration;

/// <summary>
/// Quantitative parameters for IFRS 9 macro-shock sensitivity and staging thresholds.
/// </summary>
public class Ifrs9Settings
{
    public const string SectionName = "Ifrs9";

    /// <summary>
    /// Logit coefficient sensitivity to percentage point shift in unemployment.
    /// Default: 0.25
    /// </summary>
    public double BetaUnemployment { get; set; } = 0.25;

    /// <summary>
    /// Logit coefficient sensitivity to percentage point shift in inflation.
    /// Default: 0.10
    /// </summary>
    public double BetaInflation { get; set; } = 0.10;

    /// <summary>
    /// Stressed 12-month PD threshold for automatic migration to Stage 3 (default: 0.20 or 20%).
    /// </summary>
    public double Stage3Threshold { get; set; } = 0.20;

    /// <summary>
    /// Relative multiplier of stressed 12m PD over baseline 12m PD triggering Significant Increase in Credit Risk (SICR) (default: 2.0x).
    /// </summary>
    public double SicrMultiplier { get; set; } = 2.0;
}
