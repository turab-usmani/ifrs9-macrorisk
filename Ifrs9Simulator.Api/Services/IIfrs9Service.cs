using Ifrs9Simulator.Api.DTOs;

namespace Ifrs9Simulator.Api.Services;

public interface IIfrs9Service
{
    /// <summary>
    /// Executes a macro-shock credit risk simulation across the loan portfolio.
    /// </summary>
    /// <param name="request">Macroeconomic shocks (Delta Unemployment and Delta Inflation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Portfolio ECL and staging migration results.</returns>
    Task<SimulationResponse> SimulateAsync(StressTestRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the stressed probability of default using the logit-transformation model.
    /// Clamps input PD to [1e-9, 1 - 1e-9] to ensure numeric stability.
    /// </summary>
    double CalculateStressedPd(double basePd, double deltaUnemployment, double deltaInflation);
}
