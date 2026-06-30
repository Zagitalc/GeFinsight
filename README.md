# GeFinsight 💰
**Personal finance tracker with a polymorphic budget rule engine, local fallback insights, and optional AI-powered spending analysis.**

Built with ASP.NET Core MVC targeting .NET 8, Entity Framework Core, SQLite for local/demo deployment, Bootstrap, Chart.js, deterministic local insights, and optional Anthropic Claude insights.

## Quick start

```bash
dotnet run --project GeFinsight.Web
# then open the local URL printed by dotnet
# demo account:  demo@gefinsight.local / Demo12345!
```

The database (`GeFinsight.Web/gefinsight.db`) is migrated automatically on first run and seeded
with a demo user, transactions and budgets when `DemoSeed:Enabled` is true. `Insights:Mode` defaults
to `Local`, so no Anthropic API key is required. Set `Insights:Mode` to `Claude` and configure
`Anthropic:ApiKey` only if you want Claude-powered insight.

---

## Tech Stack
| Layer | Technology |
|---|---|
| Backend | C# / ASP.NET Core MVC targeting .NET 8 |
| ORM | Entity Framework Core |
| Database | SQLite locally and for resettable demos; Azure SQL deployment notes included |
| Frontend | Razor Views, jQuery, Bootstrap 5, Chart.js |
| Insight | Deterministic local insight by default; optional Anthropic Claude mode |
| Auth | ASP.NET Core Identity |
| Deployment | Docker/Render demo deployment; Azure App Service notes in `docs/AZURE_DEPLOYMENT.md` |
| CI/CD | GitHub Actions workflow for restore, build and test |

---

## Project Structure
```
GeFinsight.Core/             # Domain logic — no framework dependencies
  Domain/                  # Entities: Transaction, Budget, Category, AppUser
  Interfaces/              # ITransactionRepository, IBudgetRepository, IBudgetRule, etc.
  Rules/                   # Budget rule hierarchy (polymorphism lives here)
  Reports/                 # Abstract ReportGenerator + concrete report types

GeFinsight.Infrastructure/   # EF Core, repositories, insight services
  Data/                    # AppDbContext, migrations, seed data
  Repositories/            # Concrete implementations of Core interfaces
  Services/                # ClaudeService, ExportService

GeFinsight.Web/              # ASP.NET Core MVC app
  Controllers/             # HomeController, TransactionsController, BudgetController
  Models/                  # Form view models
  Views/                   # Razor views per controller
GeFinsight.Core.Tests/       # xUnit coverage for rules and reports
GeFinsight.Infrastructure.Tests/ # SQLite integration tests for repositories, seeding and insights
GeFinsight.Web.Tests/        # ASP.NET Core endpoint tests
```

---

## Setup

### Prerequisites
- .NET 8 SDK
- An Anthropic API key only if you want Claude-powered insights

### 1. Clone and restore
```bash
git clone https://github.com/Zagitalc/GeFinsight.git
cd GeFinsight
dotnet restore
```

### 2. Configure secrets
```bash
cd GeFinsight.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=gefinsight.db"
dotnet user-secrets set "Insights:Mode" "Claude"
dotnet user-secrets set "Anthropic:ApiKey" "YOUR_KEY_HERE"
```

### 3. Run tests
```bash
dotnet test
```

### 4. Run
```bash
dotnet run --project GeFinsight.Web
```

---

## Key OOP Concepts Demonstrated
- **Interfaces & Dependency Injection** — all repositories and services injected via `IServiceCollection`
- **Polymorphism** — `IBudgetRule` implemented by 4 concrete rule types, each evaluated the same way
- **Abstract classes** — `ReportGenerator` with overridden `Generate()` per report type
- **Inheritance** — `RecurringTransaction` extends `Transaction`
- **Strategy pattern** — `IExportStrategy` with a CSV implementation that can be extended for other formats
- **Repository pattern** — data access abstracted behind interfaces, swappable for testing

## Interview Evidence
- OOP: budget rules use a shared interface, abstract base class and factory to evaluate rule types polymorphically.
- SQL: `GeFinsight.Infrastructure/Data/queries.sql` documents joins, aggregations, rolling totals and trend queries.
- Testing: xUnit covers the rule engine, report generators and EF Core repositories using SQLite in-memory tests.
- Cloud: Docker/Render demo deployment is supported; Azure App Service and Azure SQL notes are documented in `docs/AZURE_DEPLOYMENT.md`.

## Render Deployment

FinSight can run as a Dockerised Render Free Web Service for a resettable portfolio demo.

Render settings:

```text
Service Type: Web Service
Runtime: Docker
Plan: Free
Health Check Path: /health
```

Environment variables:

```text
ASPNETCORE_ENVIRONMENT=Production
Insights__Mode=Local
DemoSeed__Enabled=true
DemoSeed__Email=demo@gefinsight.local
DemoSeed__Password=Demo12345!
ConnectionStrings__DefaultConnection=Data Source=/tmp/gefinsight.db
```

Do not set `Anthropic__ApiKey` for the free demo deployment. Claude mode remains available for a future deployment by setting:

```text
Insights__Mode=Claude
Anthropic__ApiKey=<secret value>
```

Build and run locally with Render-like settings:

```bash
docker build -t gefinsight-render .
docker run --rm \
  -p 10000:10000 \
  -e PORT=10000 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Insights__Mode=Local \
  -e DemoSeed__Enabled=true \
  -e DemoSeed__Email=demo@gefinsight.local \
  -e DemoSeed__Password=Demo12345! \
  -e "ConnectionStrings__DefaultConnection=Data Source=/tmp/gefinsight.db" \
  gefinsight-render
```

Then verify:

```text
http://localhost:10000/health
http://localhost:10000
```

Live demo:

```text
https://gefinsight.onrender.com/Transactions
```

Demo account:

```text
demo@gefinsight.local / Demo12345!
```

### Storage Limitation

Render Free Web Services do not provide durable persistent disk storage. The SQLite database used by this portfolio demo may be removed after a restart, redeployment or instance replacement. FinSight therefore recreates the schema and seeds deterministic demo data when required. This configuration is suitable for a resettable public demonstration, not persistent production user data.

### Production Path

A durable production deployment should use PostgreSQL, Azure SQL or another managed relational database instead of ephemeral SQLite. It should also add managed secret storage, controlled migrations, backups, monitoring, production-grade email confirmation and password recovery, and a data-protection review.
