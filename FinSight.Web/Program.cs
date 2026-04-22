using FinSight.Core.Domain;
using FinSight.Core.Interfaces;
using FinSight.Infrastructure.Data;
using FinSight.Infrastructure.Repositories;
using FinSight.Infrastructure.Services;
using FinSight.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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

builder.Services.AddHttpClient<IClaudeService, ClaudeService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
