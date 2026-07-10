namespace Ifrs9Simulator.Api.Models;

/// <summary>
/// Represents a credit facility / loan instrument under IFRS 9 impairment modeling.
/// </summary>
public class Loan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Exposure at Default ($5,000 to $55,000).
    /// </summary>
    public double Ead { get; set; }

    /// <summary>
    /// Loss Given Default (20% to 60%, i.e. 0.20 to 0.60).
    /// </summary>
    public double Lgd { get; set; }

    /// <summary>
    /// Baseline 12-Month Probability of Default (0.5% to 3.5%, i.e. 0.005 to 0.035).
    /// </summary>
    public double BasePd12m { get; set; }

    /// <summary>
    /// Baseline Lifetime Probability of Default (1.5x to 3.5x higher than BasePd12m).
    /// </summary>
    public double BasePdLifetime { get; set; }
}
