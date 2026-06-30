using GeFinsight.Core.Domain;
using GeFinsight.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GeFinsight.Web;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();

        var config  = services.GetRequiredService<IConfiguration>();
        var enabled = config.GetValue<bool>("DemoSeed:Enabled");
        if (!enabled) return;

        var userMgr = services.GetRequiredService<UserManager<AppUser>>();
        var email   = config["DemoSeed:Email"]    ?? "demo@gefinsight.local";
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
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Demo user could not be created: {errors}");
            }
        }

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        await UpsertDemoTransactionsAsync(db, user.Id, BuildDemoTransactions(user.Id, startOfMonth));
        await UpsertDemoBudgetsAsync(db, user.Id, BuildDemoBudgets(user.Id));

        await db.SaveChangesAsync();
    }

    private static IEnumerable<Transaction> BuildDemoTransactions(string userId, DateTime startOfMonth)
    {
        var seeds = new[]
        {
            new DemoTransactionSeed("Income",        "Salary",      2500m, TransactionType.Income,  0),
            new DemoTransactionSeed("Housing",       "Rent",        -950m, TransactionType.Expense, 1),
            new DemoTransactionSeed("Savings",       "Transfer",    -200m, TransactionType.Expense, 2),
            new DemoTransactionSeed("Groceries",     "Tesco",        -68m, TransactionType.Expense, 3),
            new DemoTransactionSeed("Transport",     "Rail",         -35m, TransactionType.Expense, 4),
            new DemoTransactionSeed("Eating Out",    "Dinner",       -55m, TransactionType.Expense, 6),
            new DemoTransactionSeed("Entertainment", "Cinema",       -15m, TransactionType.Expense, 8),
            new DemoTransactionSeed("Health",        "Dentist",     -120m, TransactionType.Expense, 9),
            new DemoTransactionSeed("Groceries",     "Sainsbury's",  -42m, TransactionType.Expense, 10),
            new DemoTransactionSeed("Eating Out",    "Lunch",        -22m, TransactionType.Expense, 12)
        };

        return seeds.Select(seed => new Transaction
        {
            UserId = userId,
            Amount = seed.Amount,
            CategoryName = seed.CategoryName,
            Date = startOfMonth.AddDays(seed.DayOffset),
            Type = seed.Type,
            Note = seed.Note
        });
    }

    private static IEnumerable<Budget> BuildDemoBudgets(string userId)
        => new List<Budget>
        {
            new() { UserId = userId, CategoryName = "Groceries",     LimitAmount = 300m, RuleType = BudgetRuleType.HardCap,    IsActive = true },
            new() { UserId = userId, CategoryName = "Eating Out",    LimitAmount = 120m, RuleType = BudgetRuleType.HardCap,    IsActive = true },
            new() { UserId = userId, CategoryName = "Housing",       LimitAmount = 40m,  RuleType = BudgetRuleType.Percentage, IsActive = true },
            new() { UserId = userId, CategoryName = "Entertainment", LimitAmount = 80m,  RuleType = BudgetRuleType.Velocity,   IsActive = true }
        };

    private static async Task UpsertDemoTransactionsAsync(AppDbContext db, string userId, IEnumerable<Transaction> demoTransactions)
    {
        var existingTransactions = await db.Transactions
            .Where(t => t.UserId == userId)
            .ToListAsync();

        foreach (var demo in demoTransactions)
        {
            var existing = existingTransactions.FirstOrDefault(t =>
                t.CategoryName == demo.CategoryName &&
                t.Note == demo.Note &&
                t.Type == demo.Type);

            if (existing is null)
            {
                db.Transactions.Add(demo);
                continue;
            }

            existing.Amount = demo.Amount;
            existing.Date = demo.Date;
        }
    }

    private static async Task UpsertDemoBudgetsAsync(AppDbContext db, string userId, IEnumerable<Budget> demoBudgets)
    {
        var existingBudgets = await db.Budgets
            .Where(b => b.UserId == userId)
            .ToListAsync();

        foreach (var demo in demoBudgets)
        {
            var existing = existingBudgets.FirstOrDefault(b => b.CategoryName == demo.CategoryName);

            if (existing is null)
            {
                db.Budgets.Add(demo);
                continue;
            }

            existing.LimitAmount = demo.LimitAmount;
            existing.RuleType = demo.RuleType;
            existing.IsActive = demo.IsActive;
        }
    }

    private sealed record DemoTransactionSeed(
        string CategoryName,
        string Note,
        decimal Amount,
        TransactionType Type,
        int DayOffset);
}
