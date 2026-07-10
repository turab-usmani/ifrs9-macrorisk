using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ifrs9Simulator.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ifrs9Simulator.Tests;

public class SimulationEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SimulationEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostSimulate_WithValidInputs_Returns200AndValidSimulationResults()
    {
        // Arrange
        var request = new StressTestRequest(DeltaUnemployment: 2.0, DeltaInflation: 1.5);

        // Act
        var httpResponse = await _client.PostAsJsonAsync("/api/simulate", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);

        var result = await httpResponse.Content.ReadFromJsonAsync<SimulationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(5000, result.TotalLoans);
        Assert.True(result.TotalBaselineEcl > 0, "Baseline ECL should be positive");
        Assert.True(result.TotalStressedEcl > result.TotalBaselineEcl, "Stressed ECL should exceed baseline under positive shocks");
        Assert.Equal(5000, result.Stage1Count + result.Stage2Count + result.Stage3Count);
        Assert.True(result.EclDelta > 0, "ECL Delta must be positive");
    }

    [Theory]
    [InlineData(11.0, 2.0, "DeltaUnemployment")]   // Unemployment exceeds max (10.0)
    [InlineData(-3.0, 2.0, "DeltaUnemployment")]  // Unemployment below min (-2.0)
    [InlineData(2.0, 15.0, "DeltaInflation")]     // Inflation exceeds max (12.0)
    [InlineData(2.0, -4.0, "DeltaInflation")]     // Inflation below min (-2.0)
    public async Task PostSimulate_WithOutOfRangeInputs_Returns400ProblemDetails(
        double deltaUnemployment,
        double deltaInflation,
        string expectedErrorKey)
    {
        // Arrange
        var request = new StressTestRequest(deltaUnemployment, deltaInflation);

        // Act
        var httpResponse = await _client.PostAsJsonAsync("/api/simulate", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, httpResponse.StatusCode);

        var problemDetails = await httpResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>(_jsonOptions);
        Assert.NotNull(problemDetails);
        Assert.Equal("Validation Failed", problemDetails.Title);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemDetails.Status);
        Assert.True(problemDetails.Errors.ContainsKey(expectedErrorKey),
            $"ProblemDetails should contain validation error for key '{expectedErrorKey}'");
    }

    [Fact]
    public async Task GetConfig_ReturnsActiveSensitivitiesAndThresholds()
    {
        // Act
        var httpResponse = await _client.GetAsync("/api/config");

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var content = await httpResponse.Content.ReadAsStringAsync();
        Assert.Contains("betaUnemployment", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("betaInflation", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stage3Threshold", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sicrMultiplier", content, StringComparison.OrdinalIgnoreCase);
    }
}
