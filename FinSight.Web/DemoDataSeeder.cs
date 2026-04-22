using FinSight.Core.Domain;
using FinSight.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Web;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var config  = services.GetRequiredService<IConfiguration>();
        var enabled = config.GetValue<bool>("DemoSeed:Enabled");
        if (!enabled) return;

        var userMgr = services.GetRequiredService<UserManager<AppUser>>();
        var email   = config["DemoSeed:Email"]    ?? "demo@finsight.local";
        var pwd     = config["DemoSeed:Password"] ?? "Demo12345!";

        var user = await userMgr.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email    = email,
                EmailConfirmed = true,
                DisplayName = "Demo User"
            };
            var result = await userMgr.CreateAsync(user, pwd);
            if (!result.Succeeded) return;
        }

        if (await db.Transactions.AnyAsync(t => t.UserId == user.Id)) return;

        var today = DateTime.Today;
        var start = new DateTime(today.Year, today.Month, 1);

        var txns = new List<Transaction>
        {
            new() { UserId = user.Id, Amount =  2500m, CategoryName = "Income",       Date = start,                   Type = TransactionType.Income,  Note = "Salary" },
            new() { UserId = user.Id, Amount =  -950m, CategoryName = "Housing",      Date = start.AddDays(1),        Type = TransactionType.Expense, Note = "Rent" },
            new() { UserId = user.Id, Amount =   -68m, CategoryName = "Groceries",    Date = start.AddDays(3),        Type = TransactionType.Expense, Note = "Tesco" },
            new() { UserId = user.Id, Amount =   -42m, CategoryName = "Groceries",    Date = start.AddDays(10),       Type = TransactionType.Expense, Note = "Sainsbury's" },
            new() { UserId = user.Id, Amount =   -35m, CategoryName = "Transport",    Date = start.AddDays(4),        Type = TransactionType.Expense, Note = "Rail" },
            new() { UserId = user.Id, Amount =   -55m, CategoryName = "Eating Out",   Date = start.AddDays(6),        Type = TransactionType.Expense, Note = "Dinner" },
            new() { UserId = user.Id, Amount =   -22m, CategoryName = "Eating Out",   Date = start.AddDays(12),       Type = TransactionType.Expense, Note = "Lunch" },
            new() { UserId = user.Id, Amount =   -15m, CategoryName = "Entertainment",Date = start.AddDays(8),        Type = TransactionType.Expense, Note = "Cinema" },
            new() { UserId = user.Id, Amount =  -120m, CategoryName = "Health",       Date = start.AddDays(9),        Type = TransactionType.Expense, Note = "Dentist" },
            new() { UserId = user.Id, Amount =  -200m, CategoryName = "Savings",      Date = start.AddDays(2),        Type = TransactionType.Expense, Note = "Transfer" }
        };
        db.Transactions.AddRange(txns);

        var budgets = new List<Budget>
        {
            new() { UserId = user.Id, CategoryName = "Groceries",    LimitAmount = 300m, RuleType = BudgetRuleType.HardCap,        IsActive = true },
            new() { UserId = user.Id, CategoryName = "Eating Out",   LimitAmount = 120m, RuleType = BudgetRuleType.HardCap,        IsActive = true },
            new() { UserId = user.Id, CategoryName = "Housing",      LimitAmount = 40m,  RuleType = BudgetRuleType.Percentage,     IsActive = true },
            new() { UserId = user.Id, CategoryName = "Entertainment",LimitAmount = 80m,  RuleType = BudgetRuleType.Velocity,       IsActive = true }
        };
        db.Budgets.AddRange(budgets);

        await db.SaveChangesAsync();
    }
}
