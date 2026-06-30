using GeFinsight.Core.Domain;
using GeFinsight.Core.Interfaces;
using GeFinsight.Infrastructure.Data;
using GeFinsight.Infrastructure.Repositories;
using GeFinsight.Infrastructure.Services;
using GeFinsight.Web;
using GeFinsight.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(connectionString));

var requestedInsightMode = builder.Configuration["Insights:Mode"];
var normalizedInsightMode = string.IsNullOrWhiteSpace(requestedInsightMode)
    ? "Local"
    : requestedInsightMode.Trim();
var isLocalInsightMode = normalizedInsightMode.Equals("Local", StringComparison.OrdinalIgnoreCase);
var isClaudeInsightMode = normalizedInsightMode.Equals("Claude", StringComparison.OrdinalIgnoreCase);
var hasAnthropicApiKey = !string.IsNullOrWhiteSpace(builder.Configuration["Anthropic:ApiKey"]);
var useClaudeInsights = isClaudeInsightMode && hasAnthropicApiKey;

builder.Services.AddDefaultIdentity<AppUser>(opts =>
{
    opts.SignIn.RequireConfirmedAccount = false;
    opts.Password.RequiredLength         = 8;
    opts.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IBudgetRepository,      BudgetRepository>();
builder.Services.AddScoped<ICategoryRepository,    CategoryRepository>();
builder.Services.AddScoped<IBudgetRuleEngine,      BudgetRuleEngine>();
builder.Services.AddScoped<IExportStrategy,        CsvExportStrategy>();

builder.Services.AddSingleton(new InsightDisplayOptions(useClaudeInsights ? "AI Insight" : "Insight"));
builder.Services.AddScoped<LocalInsightService>();
if (useClaudeInsights)
{
    builder.Services.AddHttpClient<ClaudeInsightService>();
    builder.Services.AddScoped<IInsightService, FallbackInsightService>();
}
else
{
    builder.Services.AddScoped<IInsightService, LocalInsightService>();
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (!isLocalInsightMode && !isClaudeInsightMode)
{
    app.Logger.LogWarning(
        "Unsupported insight mode '{Mode}'. Local insight generation will be used.",
        normalizedInsightMode);
}
else if (isClaudeInsightMode && !hasAnthropicApiKey)
{
    app.Logger.LogWarning(
        "Insights mode is set to Claude, but no Anthropic API key is configured. Local insight generation will be used.");
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (builder.Configuration.GetValue<bool>("DemoSeed:Enabled"))
        {
            await DemoDataSeeder.SeedAsync(services);
        }

        logger.LogInformation("Database migration and demo-data startup completed.");
    }
    catch (Exception exception)
    {
        logger.LogCritical(exception, "Database migration or demo-data seeding failed.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return canConnect ? Results.Ok("Healthy") : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

public partial class Program { }
