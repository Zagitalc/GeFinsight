using FinSight.Core.Domain;
using FinSight.Core.Rules;
using Xunit;

namespace FinSight.Core.Tests;

public class BudgetRuleTests
{
    [Fact]
    public void HardCapRule_Breaches_WhenSpendingExceedsLimit()
    {
        var rule = new HardCapRule("Groceries", 200m);

        var result = rule.Evaluate(new[]
        {
            Expense("Groceries", 150m),
            Expense("Groceries", 80m),
            Expense("Transport", 50m)
        });

        Assert.True(result.IsBreached);
        Assert.Equal(RuleSeverity.Breached, result.Severity);
        Assert.Equal(230m, result.ActualAmount);
    }

    [Fact]
    public void RollingAverageRule_Warns_WhenDailyAverageNearLimit()
    {
        var rule = new RollingAverageRule("Eating Out", 10m, windowDays: 10);

        var result = rule.Evaluate(new[]
        {
            Expense("Eating Out", 86m, DateTime.Today.AddDays(-2))
        });

        Assert.False(result.IsBreached);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(8.6m, result.ActualAmount);
    }

    [Fact]
    public void PercentageRule_Breaches_WhenCategoryExceedsIncomeShare()
    {
        var rule = new PercentageRule("Housing", 40m, monthlyIncome: 2500m);

        var result = rule.Evaluate(new[]
        {
            Expense("Housing", 1100m)
        });

        Assert.True(result.IsBreached);
        Assert.Equal(RuleSeverity.Breached, result.Severity);
        Assert.Equal(44m, result.ActualAmount);
    }

    [Fact]
    public void VelocityRule_Breaches_WhenSpendingRunsFarAheadOfPace()
    {
        var rule = new VelocityRule("Entertainment", 300m);
        var expectedByNow = 300m * DateTime.Today.Day / DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month);
        var tooFast = Math.Round(expectedByNow * 1.5m, 2);

        var result = rule.Evaluate(new[]
        {
            Expense("Entertainment", tooFast)
        });

        Assert.True(result.IsBreached);
        Assert.Equal(RuleSeverity.Breached, result.Severity);
    }

    private static Transaction Expense(string category, decimal amount, DateTime? date = null)
        => new()
        {
            CategoryName = category,
            Amount = -Math.Abs(amount),
            Type = TransactionType.Expense,
            Date = date ?? DateTime.Today
        };
}
