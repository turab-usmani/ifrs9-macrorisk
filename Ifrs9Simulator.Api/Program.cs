using Ifrs9Simulator.Api.Configuration;
using Ifrs9Simulator.Api.Data;
using Ifrs9Simulator.Api.Endpoints;
using Ifrs9Simulator.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Production Standards: Global Exception Handling via ProblemDetails
builder.Services.AddProblemDetails();

// 2. Configuration: IOptions pattern for IFRS 9 sensitivities and SICR parameters
builder.Services.Configure<Ifrs9Settings>(
    builder.Configuration.GetSection(Ifrs9Settings.SectionName));

// 3. Data Access: EF Core with SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=ifrs9_simulator.db";

builder.Services.AddDbContext<Ifrs9DbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// 4. Core Quantitative Services
builder.Services.AddScoped<IIfrs9Service, Ifrs9Service>();

// 5. OpenAPI / Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "IFRS 9 Macro-Shock Risk Simulator API",
        Version = "v1",
        Description = "Enterprise quantitative credit risk impairment API modeling macroeconomic scenario shocks, logit transmission, and SICR staging migrations under IFRS 9 standards."
    });
});

var app = builder.Build();

// 6. Pipeline: Exception handling & ProblemDetails
app.UseExceptionHandler();
app.UseStatusCodePages();

// 7. Swagger Documentation UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IFRS 9 Simulator API v1");
    c.RoutePrefix = "swagger";
});

// 8. Static Files for the Frontend UI
app.UseDefaultFiles();
app.UseStaticFiles();

// 9. Minimal API Route Mappings
app.MapSimulationEndpoints();

// 10. Database Initialization and Seeding (5,000 loans)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var dbContext = services.GetRequiredService<Ifrs9DbContext>();

    try
    {
        await DbInitializer.InitializeAsync(dbContext, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An unhandled exception occurred during database initialization and loan seeding.");
        throw;
    }
}

app.Run();

// Required for WebApplicationFactory<Program> integration test accessibility
public partial class Program { }
