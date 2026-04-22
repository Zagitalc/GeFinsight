using Microsoft.AspNetCore.Identity;

namespace FinSight.Core.Domain;

// ─────────────────────────────────────────────
// Base Transaction — all transactions share this
// ─────────────────────────────────────────────
public class Transaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }          // Positive = income, Negative = expense
    public string CategoryName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
    public TransactionType Type { get; set; }

    // Navigation
    public virtual AppUser User { get; set; } = null!;
    public virtual Category Category { get; set; } = null!;
}

// ─────────────────────────────────────────────
// Recurring transaction — knows how to project
// future instances (e.g. monthly rent)
// ─────────────────────────────────────────────
public class RecurringTransaction : Transaction
{
    public RecurrenceInterval Interval { get; set; }
    public DateTime NextOccurrence { get; set; }
    public DateTime? EndDate { get; set; }       // null = indefinite

    /// <summary>
    /// Projects the next N occurrences from today.
    /// Used by the dashboard forecast panel.
    /// </summary>
    public IEnumerable<DateTime> ProjectOccurrences(int count)
    {
        var next = NextOccurrence;
        for (int i = 0; i < count; i++)
        {
            yield return next;
            next = Interval switch
            {
                RecurrenceInterval.Weekly  => next.AddDays(7),
                RecurrenceInterval.Monthly => next.AddMonths(1),
                RecurrenceInterval.Yearly  => next.AddYears(1),
                _ => next.AddMonths(1)
            };
        }
    }
}

// ─────────────────────────────────────────────
// Category
// ─────────────────────────────────────────────
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "💳";    // Emoji icon for UI
    public string Colour { get; set; } = "#6c757d";
    public bool IsSystem { get; set; }           // System categories can't be deleted

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

// ─────────────────────────────────────────────
// Budget — ties a rule type to a category + user
// ─────────────────────────────────────────────
public class Budget
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal LimitAmount { get; set; }
    public BudgetRuleType RuleType { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual AppUser User { get; set; } = null!;
}

// ─────────────────────────────────────────────
// AppUser — extends IdentityUser
// ─────────────────────────────────────────────
public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string PreferredCurrency { get; set; } = "GBP";

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public virtual ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum TransactionType  { Income, Expense }
public enum RecurrenceInterval { Weekly, Monthly, Yearly }
public enum BudgetRuleType   { HardCap, RollingAverage, Percentage, Velocity }
