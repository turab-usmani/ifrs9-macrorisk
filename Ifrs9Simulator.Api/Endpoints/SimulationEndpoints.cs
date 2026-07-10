using Ifrs9Simulator.Api.Configuration;
using Ifrs9Simulator.Api.DTOs;
using Ifrs9Simulator.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ifrs9Simulator.Api.Endpoints;

public static class SimulationEndpoints
{
    public static RouteGroupBuilder MapSimulationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api")
            .WithTags("IFRS 9 Risk Simulation");

        group.MapPost("/simulate", async (
            [FromBody] StressTestRequest request,
            [FromServices] IIfrs9Service ifrs9Service,
            CancellationToken cancellationToken) =>
        {
            if (!request.IsValid(out var errors))
            {
                return Results.ValidationProblem(
                    errors: errors,
                    title: "Validation Failed",
                    detail: "Macroeconomic scenario parameters are outside the supported domain boundaries.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var response = await ifrs9Service.SimulateAsync(request, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("RunSimulation")
        .WithSummary("Execute IFRS 9 macro-shock impairment simulation")
        .WithDescription("Calculates stressed 12m and lifetime PDs via logit transmission, assesses SICR thresholds, and determines ECL staging.")
        .Produces<SimulationResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/config", ([FromServices] IOptions<Ifrs9Settings> options) =>
        {
            return Results.Ok(options.Value);
        })
        .WithName("GetSimulationParameters")
        .WithSummary("Retrieve active macroeconomic sensitivities and SICR thresholds")
        .Produces<Ifrs9Settings>(StatusCodes.Status200OK);

        return group;
    }
}
