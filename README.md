# GeFinsight 💰
**Personal finance tracker with a polymorphic budget rule engine, local fallback insights, and optional AI-powered spending analysis.**

Built with ASP.NET Core MVC targeting .NET 7 and developed with the .NET 10 SDK, Entity Framework Core, SQLite for local development, Bootstrap, Chart.js, and the Anthropic Claude API.

## Quick start

```bash
dotnet run --project GeFinsight.Web
# then open the local URL printed by dotnet
# demo account:  demo@gefinsight.local / Demo12345!
```

The database (`GeFinsight.Web/gefinsight.db`) is created automatically on first run and seeded
with a demo user, transactions and budgets when `DemoSeed:Enabled` is true. Set `Anthropic:ApiKey`
in user-secrets or `appsettings.json` to enable Claude-powered insight; otherwise the dashboard
shows a deterministic local insight generated from the monthly report and budget rules.

---

## Tech Stack
| Layer | Technology |
|---|---|
| Backend | C# / ASP.NET Core MVC targeting .NET 7 |
| ORM | Entity Framework Core |
| Database | SQLite locally; Azure SQL deployment notes included |
| Frontend | Razor Views, jQuery, Bootstrap 5, Chart.js |
| AI | Optional Anthropic Claude API with local fallback insight |
| Auth | ASP.NET Core Identity |
| Deployment | Azure App Service notes in `docs/AZURE_DEPLOYMENT.md` |
| CI/CD | GitHub Actions workflow for restore, build and test |

---

## Project Structure
```
GeFinsight.Core/             # Domain logic — no framework dependencies
  Domain/                  # Entities: Transaction, Budget, Category, AppUser
  Interfaces/              # ITransactionRepository, IBudgetRepository, IBudgetRule, etc.
  Rules/                   # Budget rule hierarchy (polymorphism lives here)
  Reports/                 # Abstract ReportGenerator + concrete report types

GeFinsight.Infrastructure/   # EF Core, repositories, external services
  Data/                    # AppDbContext, migrations, seed data
  Repositories/            # Concrete implementations of Core interfaces
  Services/                # ClaudeService, ExportService

GeFinsight.Web/              # ASP.NET Core MVC app
  Controllers/             # HomeController, TransactionsController, BudgetController
  Models/                  # Form view models
  Views/                   # Razor views per controller
GeFinsight.Core.Tests/       # xUnit coverage for rules and reports
GeFinsight.Infrastructure.Tests/ # SQLite integration tests for repositories
```

---

## Setup

### Prerequisites
- .NET 10 SDK
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
- Cloud: Azure App Service and Azure SQL configuration notes are documented in `docs/AZURE_DEPLOYMENT.md`.
