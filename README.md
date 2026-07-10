# IFRS 9 Macro-Shock Risk Simulator

An enterprise-grade quantitative credit risk impairment platform and model validation framework built with **ASP.NET Core Minimal APIs (.NET 10)**, **Entity Framework Core with SQLite**, and **xUnit**.

The system simulates forward-looking macroeconomic stress scenarios (Unemployment and Headline Inflation shocks) across a 5,000-facility wholesale credit portfolio, applying logit-space probability transmission, Significant Increase in Credit Risk (SICR) migration, and multi-stage Expected Credit Loss (ECL) provisioning under International Financial Reporting Standard 9 (IFRS 9).

---

## 1. Quantitative Methodology

### 1.1 Logit Macroeconomic Transmission
Standard linear stress models suffer from probability unboundedness ($PD > 1.0$ or $PD < 0.0$) under catastrophic tail shocks. To ensure mathematical and economic consistency, baseline probabilities are mapped to unbounded log-odds space via the logit transform:

$$\text{Logit}(PD) = \ln\left(\frac{PD}{1 - PD}\right)$$

Macroeconomic scenario shocks are applied linearly in logit space:

$$\text{Stressed Logit} = \text{Logit}(PD) + (\beta_{\text{Unemployment}} \cdot \Delta\text{Unemployment}) + (\beta_{\text{Inflation}} \cdot \Delta\text{Inflation})$$

The stressed probability is obtained by inverting the logit:

$$\text{Stressed } PD = \frac{1}{1 + \exp(-\text{Stressed Logit})}$$

To avoid numerical singularity at boundaries, baseline PDs are pre-clamped to $[10^{-9}, 1 - 10^{-9}]$. Stressed Lifetime PD is additionally constrained such that:

$$\text{Stressed } PD_{\text{Lifetime}} \ge \text{Stressed } PD_{\text{12m}}$$

### 1.2 Default Sensitivities and Staging Rules
Configured via `appsettings.json` and injected through `IOptions<Ifrs9Settings>`:
- $\beta_{\text{Unemployment}} = 0.25$
- $\beta_{\text{Inflation}} = 0.10$
- **Stage 3 Threshold:** $\text{Stressed } PD_{\text{12m}} \ge 0.20$ (20% boundary). Evaluated as credit-impaired / default:
  $$\text{ECL} = 1.0 \cdot \text{LGD} \cdot \text{EAD}$$
- **Stage 2 (SICR):** Relative deterioration ratio $\frac{\text{Stressed } PD_{\text{12m}}}{\text{Base } PD_{\text{12m}}} \ge 2.00$ ($200\%$ of baseline). Evaluated using Stressed Lifetime PD:
  $$\text{ECL} = \text{Stressed } PD_{\text{Lifetime}} \cdot \text{LGD} \cdot \text{EAD}$$
- **Stage 1:** Performing facilities maintaining a stable credit profile. Evaluated using Stressed 12-Month PD:
  $$\text{ECL} = \text{Stressed } PD_{\text{12m}} \cdot \text{LGD} \cdot \text{EAD}$$

---

## 2. Solution Architecture

```
f:\ag\ifrs9\
│
├── Ifrs9Simulator.sln
│
├── Ifrs9Simulator.Api/
│   ├── Configuration/
│   │   └── Ifrs9Settings.cs          # IOptions configuration model for Betas and staging thresholds
│   ├── Data/
│   │   ├── Ifrs9DbContext.cs        # EF Core DbContext for SQLite
│   │   └── DbInitializer.cs         # Seeds 5,000 reproducible loan facilities (Seed: 42)
│   ├── DTOs/
│   │   ├── StressTestRequest.cs     # Sealed record with scenario bounds validation (-2..10, -2..12)
│   │   └── SimulationResponse.cs    # Sealed record with portfolio ECL KPIs and staging counts
│   ├── Endpoints/
│   │   └── SimulationEndpoints.cs   # ASP.NET Core Minimal API endpoint route definitions
│   ├── Models/
│   │   └── Loan.cs                  # Credit facility domain entity (EAD, LGD, BasePd12m, BasePdLifetime)
│   ├── Services/
│   │   ├── IIfrs9Service.cs         # Quantitative calculation contract
│   │   └── Ifrs9Service.cs          # Logit math, SICR evaluation, .AsNoTracking() streaming queries
│   ├── wwwroot/
│   │   └── index.html               # Pure black & white serif dashboard with dual live sliders
│   ├── appsettings.json             # Structured options and connection string
│   └── Program.cs                   # Minimal API bootstrap, ProblemDetails, Swagger, Seeding
│
└── Ifrs9Simulator.Tests/
    ├── Ifrs9ServiceTests.cs         # Unit tests proving extreme shock bounding (< 1.0) and SICR migration
    └── SimulationEndpointIntegrationTests.cs # WebApplicationFactory integration tests (200 OK & 400 ProblemDetails)
```

### Key Architectural Highlights:
1. **AsNoTracking() & Async Cancellation:** All queries to the SQLite repository strictly use `.AsNoTracking()` to avoid change-tracking overhead across the 5,000-loan portfolio, passing `CancellationToken` throughout.
2. **Global Exception Handling:** Configured via `builder.Services.AddProblemDetails()` and `app.UseExceptionHandler()`.
3. **Structured Logging:** `ILogger<Ifrs9Service>` logs scenario inputs, portfolio metrics, stage migrations, and execution metrics.
4. **Strict Grayscale UI:** The interactive dashboard adheres to a modern, minimalist black/white/grayscale palette rendered exclusively in serif typography.

---

## 3. Quickstart & Execution

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 9)

### 3.1 Run the Web API & Dashboard
From the root directory:

```bash
dotnet run --project Ifrs9Simulator.Api/Ifrs9Simulator.Api.csproj
```

The application automatically seeds 5,000 loan facilities into `ifrs9_simulator.db` upon initial launch.

- **Web Dashboard:** [http://localhost:5000](http://localhost:5000) (or the port specified in console)
- **Interactive Swagger UI:** [http://localhost:5000/swagger](http://localhost:5000/swagger)

### 3.2 Execute the Test Suite
Run all unit and integration tests:

```bash
dotnet test
```

---

## 4. API Endpoints & cURL Examples

### 4.1 Run Macroeconomic Shock Simulation (`POST /api/simulate`)

#### Request:
```bash
curl -X POST "http://localhost:5000/api/simulate" \
     -H "Content-Type: application/json" \
     -d '{
       "deltaUnemployment": 3.5,
       "deltaInflation": 2.0
     }'
```

#### Successful Response (`200 OK`):
```json
{
  "totalBaselineEcl": 298412.45,
  "totalStressedEcl": 842109.12,
  "eclDelta": 543696.67,
  "stage1Count": 1840,
  "stage2Count": 2912,
  "stage3Count": 248,
  "totalLoans": 5000,
  "stage1Ecl": 112450.30,
  "stage2Ecl": 481230.15,
  "stage3Ecl": 248428.67
}
```

### 4.2 Out-of-Range Request Validation (`400 Bad Request`)

#### Request (Exceeding +10.0 pp Unemployment threshold):
```bash
curl -X POST "http://localhost:5000/api/simulate" \
     -H "Content-Type: application/json" \
     -d '{
       "deltaUnemployment": 15.0,
       "deltaInflation": 2.0
     }'
```

#### ProblemDetails Response (`400 Bad Request`):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Failed",
  "status": 400,
  "detail": "Macroeconomic scenario parameters are outside the supported domain boundaries.",
  "errors": {
    "DeltaUnemployment": [
      "DeltaUnemployment must be between -2 and 10 percentage points."
    ]
  }
}
```

### 4.3 Retrieve Active Sensitivity Parameters (`GET /api/config`)

```bash
curl -X GET "http://localhost:5000/api/config"
```

---

## 5. Model Validation Findings

1. **Probability Bounding Under Extreme Tail Shocks:**
   - Under an extreme shock ($\Delta U = +100.0\%$), a standard linear model produces $PD = 2505\%$.
   - The logit model yields $PD = 99.99999997\%$, strictly preserving the mathematical invariant $PD \in (0, 1)$.
2. **SICR Relative Trigger Accuracy:**
   - A moderate shock of $+3.5\text{ pp}$ unemployment raises a $1.0\%$ baseline 12m PD facility to $2.36\%$.
   - The $2.36\text{x}$ increase surpasses the $2.0\text{x}$ SICR threshold, correctly triggering migration from Stage 1 (12-month ECL) to Stage 2 (Lifetime ECL).
