# Azure Deployment Notes

GeFinsight is ready to deploy as an ASP.NET Core MVC app to Azure App Service.

## App Service

- Runtime stack: .NET 8 target framework
- Startup project: `GeFinsight.Web`
- Required app settings:
  - `ConnectionStrings__DefaultConnection`
  - `DemoSeed__Enabled`
  - `DemoSeed__Email`
  - `DemoSeed__Password`
  - `Insights__Mode` (`Local` by default; use `Claude` only when configuring Anthropic)
  - `Anthropic__ApiKey` if Claude insights should be enabled

## Database

Development uses SQLite. For Azure SQL, change the EF provider in `GeFinsight.Web/Program.cs` from `UseSqlite` to `UseSqlServer` and set `ConnectionStrings__DefaultConnection` to the Azure SQL connection string.

## CI

The GitHub Actions workflow at `.github/workflows/dotnet.yml` restores, builds, and runs the xUnit test suite on pushes and pull requests to `main`.
