using FinSight.Core.Domain;
using FinSight.Core.Interfaces;

namespace FinSight.Core.Rules;

// ─────────────────────────────────────────────
// RuleResult — returned by every rule regardless of type
// ─────────────────────────────────────────────

public class RuleResult
{
    public string RuleName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal LimitAmount { get; init; }
    public decimal ActualAmount { get; init; }
    public bool IsBreached { get; init; }
    public RuleSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;

    public decimal PercentUsed => LimitAmount > 0
        ? Math.Round((ActualAmount / LimitAmount) * 100, 1)
        : 0;
}

public enum RuleSeverity { Ok, Warning, Breached }

// ─────────────────────────────────────────────
// Abstract base — shared constructor logic only.
// Concrete rules must implement Evaluate().
// ─────────────────────────────────────────────

public abstract class BudgetRuleBase : IBudgetRule
{
    public string RuleName { get; }
    public string CategoryName { get; }
    public decimal LimitAmount { get; }

    protected BudgetRuleBase(string ruleName, string categoryName, decimal limitAmount)
    {
        RuleName = ruleName;
        CategoryName = categoryName;
        LimitAmount = limitAmount;
    }

    public abstract RuleResult Evaluate(IEnumerable<Transaction> transactions);

    protected IEnumerable<Transaction> FilterByCategory(IEnumerable<Transaction> transactions)
        => transactions.Where(t =>
            t.CategoryName.Equals(CategoryName, StringComparison.OrdinalIgnoreCase) &&
            t.Type == TransactionType.Expense);
}

// ─────────────────────────────────────────────
// Rule 1: HardCapRule
// "Total spending in this category must not exceed £X this month."
// ─────────────────────────────────────────────

public class HardCapRule : BudgetRuleBase
{
    public HardCapRule(string categoryName, decimal limit)
        : base("Hard Cap", categoryName, limit) { }

    public override RuleResult Evaluate(IEnumerable<Transaction> transactions)
    {
        var total = FilterByCategory(transactions).Sum(t => Math.Abs(t.Amount));
        var breached = total > LimitAmount;
        var pct = LimitAmount > 0 ? (total / LimitAmount) * 100 : 0;

        return new RuleResult
        {
            RuleName = RuleName,
            CategoryName = CategoryName,
            LimitAmount = LimitAmount,
            ActualAmount = total,
            IsBreached = breached,
            Severity = breached ? RuleSeverity.Breached
                     : pct >= 80 ? RuleSeverity.Warning
                     : RuleSeverity.Ok,
            Message = breached
                ? $"Over budget on {CategoryName}: spent £{total:F2} of £{LimitAmount:F2} limit."
                : $"{CategoryName}: £{total:F2} of £{LimitAmount:F2} used ({pct:F0}%)."
        };
    }
}

// ─────────────────────────────────────────────
// Rule 2: RollingAverageRule
// "Flag if 30-day rolling average exceeds £X per day."
// ─────────────────────────────────────────────

public class RollingAverageRule : BudgetRuleBase
{
    private readonly int _windowDays;

    public RollingAverageRule(string categoryName, decimal dailyLimit, int windowDays = 30)
        : base("Rolling Average", categoryName, dailyLimit)
    {
        _windowDays = windowDays;
    }

    public override RuleResult Evaluate(IEnumerable<Transaction> transactions)
    {
        var cutoff = DateTime.Today.AddDays(-_windowDays);
        var windowTransactions = FilterByCategory(transactions)
            .Where(t => t.Date >= cutoff);

        var total = windowTransactions.Sum(t => Math.Abs(t.Amount));
        var dailyAvg = total / _windowDays;
        var breached = dailyAvg > LimitAmount;

        return new RuleResult
        {
            RuleName = RuleName,
            CategoryName = CategoryName,
            LimitAmount = LimitAmount,
            ActualAmount = dailyAvg,
            IsBreached = breached,
            Severity = breached ? RuleSeverity.Breached
                     : dailyAvg >= LimitAmount * 0.85m ? RuleSeverity.Warning
                     : RuleSeverity.Ok,
            Message = breached
                ? $"{CategoryName}: daily average £{dailyAvg:F2} exceeds £{LimitAmount:F2}/day limit over {_windowDays} days."
                : $"{CategoryName}: averaging £{dailyAvg:F2}/day over last {_windowDays} days (limit £{LimitAmount:F2})."
        };
    }
}

// ─────────────────────────────────────────────
// Rule 3: PercentageRule
// "This category should not exceed X% of monthly income."
// ─────────────────────────────────────────────

public class PercentageRule : BudgetRuleBase
{
    private readonly decimal _monthlyIncome;

    public PercentageRule(string categoryName, decimal maxPercentage, decimal monthlyIncome)
        : base("Percentage of Income", categoryName, maxPercentage)
    {
        _monthlyIncome = monthlyIncome;
    }

    public override RuleResult Evaluate(IEnumerable<Transaction> transactions)
    {
        var total = FilterByCategory(transactions).Sum(t => Math.Abs(t.Amount));
        var actualPct = _monthlyIncome > 0 ? (total / _monthlyIncome) * 100 : 0;
        var breached = actualPct > LimitAmount;

        return new RuleResult
        {
            RuleName = RuleName,
            CategoryName = CategoryName,
            LimitAmount = LimitAmount,
            ActualAmount = actualPct,
            IsBreached = breached,
            Severity = breached ? RuleSeverity.Breached
                     : actualPct >= LimitAmount * 0.85m ? RuleSeverity.Warning
                     : RuleSeverity.Ok,
            Message = breached
                ? $"{CategoryName} is {actualPct:F1}% of income — exceeds {LimitAmount:F0}% limit."
                : $"{CategoryName} is {actualPct:F1}% of income (max {LimitAmount:F0}%)."
        };
    }
}

// ─────────────────────────────────────────────
// Rule 4: VelocityRule
// "Spending is happening too fast — flag if X% of budget
// is used in the first Y% of the month."
// ─────────────────────────────────────────────

public class VelocityRule : BudgetRuleBase
{
    public VelocityRule(string categoryName, decimal monthlyLimit)
        : base("Velocity", categoryName, monthlyLimit) { }

    public override RuleResult Evaluate(IEnumerable<Transaction> transactions)
    {
        var today = DateTime.Today;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var daysPassed = today.Day;
        var monthProgress = (decimal)daysPassed / daysInMonth;   // e.g. 0.33 = 1/3 through month

        // What would be "on pace"?
        var expectedByNow = LimitAmount * monthProgress;

        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var total = FilterByCategory(transactions)
            .Where(t => t.Date >= startOfMonth)
            .Sum(t => Math.Abs(t.Amount));

        // Breached if spending is more than 40% ahead of pace
        var breached = total > expectedByNow * 1.4m;
        var warning  = total > expectedByNow * 1.2m;

        return new RuleResult
        {
            RuleName = RuleName,
            CategoryName = CategoryName,
            LimitAmount = LimitAmount,
            ActualAmount = total,
            IsBreached = breached,
            Severity = breached ? RuleSeverity.Breached
                     : warning  ? RuleSeverity.Warning
                     : RuleSeverity.Ok,
            Message = breached
                ? $"{CategoryName}: spending velocity too high — £{total:F2} spent in first {daysPassed} days (on pace for £{total / monthProgress:F2} this month)."
                : $"{CategoryName}: on track — £{total:F2} spent through day {daysPassed} of {daysInMonth}."
        };
    }
}

// ─────────────────────────────────────────────
// Rule factory — builds the right concrete rule
// from a Budget entity without callers needing
// to know which class to instantiate
// ─────────────────────────────────────────────

public static class BudgetRuleFactory
{
    public static IBudgetRule Create(Budget budget, decimal monthlyIncome = 0)
        => budget.RuleType switch
        {
            BudgetRuleType.HardCap        => new HardCapRule(budget.CategoryName, budget.LimitAmount),
            BudgetRuleType.RollingAverage => new RollingAverageRule(budget.CategoryName, budget.LimitAmount),
            BudgetRuleType.Percentage     => new PercentageRule(budget.CategoryName, budget.LimitAmount, monthlyIncome),
            BudgetRuleType.Velocity       => new VelocityRule(budget.CategoryName, budget.LimitAmount),
            _ => throw new ArgumentOutOfRangeException(nameof(budget.RuleType))
        };
}
