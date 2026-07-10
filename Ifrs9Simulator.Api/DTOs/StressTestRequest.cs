namespace Ifrs9Simulator.Api.DTOs;

/// <summary>
/// Macroeconomic scenario shock request parameters.
/// </summary>
/// <param name="DeltaUnemployment">Percentage point change in unemployment (Allowed range: -2.0 to 10.0).</param>
/// <param name="DeltaInflation">Percentage point change in headline inflation (Allowed range: -2.0 to 12.0).</param>
public sealed record StressTestRequest(double DeltaUnemployment, double DeltaInflation)
{
    public const double MinUnemployment = -2.0;
    public const double MaxUnemployment = 10.0;
    public const double MinInflation = -2.0;
    public const double MaxInflation = 12.0;

    public bool IsValid(out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();

        if (DeltaUnemployment < MinUnemployment || DeltaUnemployment > MaxUnemployment)
        {
            errors[nameof(DeltaUnemployment)] =
            [
                $"DeltaUnemployment must be between {MinUnemployment} and {MaxUnemployment} percentage points."
            ];
        }

        if (DeltaInflation < MinInflation || DeltaInflation > MaxInflation)
        {
            errors[nameof(DeltaInflation)] =
            [
                $"DeltaInflation must be between {MinInflation} and {MaxInflation} percentage points."
            ];
        }

        return errors.Count == 0;
    }
}
