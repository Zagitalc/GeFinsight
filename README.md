# FinSight 💰
**Personal finance tracker with a polymorphic budget rule engine and AI-powered spending analysis.**

Built with ASP.NET Core MVC (.NET 7), Entity Framework Core, SQLite (dev) / SQL Server (prod), and the Anthropic Claude API.

## Quick start

```bash
dotnet run --project FinSight.Web
# then open http://localhost:5099
# demo account:  demo@finsight.local / Demo12345!
```

The database (`FinSight.Web/finsight.db`) is created automatically on first run and seeded
with a demo user, transactions and budgets. Set `Anthropic:ApiKey` in user-secrets or
`appsettings.json` to enable the Claude-powered insight panel.

---

## Tech Stack
| Layer | Technology |
|---|---|
| Backend | C# / ASP.NET Core MVC (.NET 8) |
| ORM | Entity Framework Core |
| Database | SQL Server (local) / Azure SQL (production) |
| Frontend | Razor Views, jQuery, Bootstrap 5, Chart.js |
| AI | Anthropic Claude API |
| Auth | ASP.NET Core Identity |
| Deployment | Azure App Service + Azure SQL Database |
| CI/CD | GitHub Actions |

---

## Project Structure
```
FinSight.Core/             # Domain logic — no framework dependencies
  Domain/                  # Entities: Transaction, Budget, Category, AppUser
  Interfaces/              # ITransactionRepository, IBudgetRepository, IBudgetRule, etc.
  Rules/                   # Budget rule hierarchy (polymorphism lives here)
  Reports/                 # Abstract ReportGenerator + concrete report types

FinSight.Infrastructure/   # EF Core, repositories, external services
  Data/                    # AppDbContext, migrations, seed data
  Repositories/            # Concrete implementations of Core interfaces
  Services/                # ClaudeService, ExportService

FinSight.Web/              # ASP.NET Core MVC app
  Controllers/             # HomeController, TransactionsController, BudgetController
  Models/                  # ViewModels (not domain models)
  Views/                   # Razor views per controller
```

---

## Setup

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB is fine for dev)
- An Anthropic API key (free tier works)

### 1. Clone and restore
```bash
git clone https://github.com/YOUR_USERNAME/FinSight.git
cd FinSight
dotnet restore
```

### 2. Configure secrets
```bash
cd FinSight.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=FinSight;Trusted_Connection=True;"
dotnet user-secrets set "Anthropic:ApiKey" "YOUR_KEY_HERE"
```

### 3. Apply migrations
```bash
dotnet ef database update --project FinSight.Infrastructure --startup-project FinSight.Web
```

### 4. Run
```bash
dotnet run --project FinSight.Web
```

---

## Key OOP Concepts Demonstrated
- **Interfaces & Dependency Injection** — all repositories and services injected via `IServiceCollection`
- **Polymorphism** — `IBudgetRule` implemented by 4 concrete rule types, each evaluated the same way
- **Abstract classes** — `ReportGenerator` with overridden `Generate()` per report type
- **Inheritance** — `RecurringTransaction` extends `Transaction`
- **Strategy pattern** — `IExportStrategy` with CSV and PDF implementations
- **Repository pattern** — data access abstracted behind interfaces, swappable for testing
