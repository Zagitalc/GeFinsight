using GeFinsight.Core.Interfaces;
using GeFinsight.Core.Reports;
using GeFinsight.Core.Rules;
using GeFinsight.Infrastructure.Services;
using Xunit;

namespace GeFinsight.Infrastructure.Tests;

public class LocalInsightServiceTests
{
    private readonly LocalInsightService _service = new();

    [Fact]
    public async Task GenerateInsightAsync_ReturnsNoSpendingMessage()
    {
        var insight = await _service.GenerateInsightAsync(Context(new ReportData()));

        Assert.Contains("No spending has been recorded", insight);
    }

    [Fact]
    public async Task GenerateInsightAsync_ReturnsWarningMessage()
    {
        var context = Context(
            new ReportData { NetAmount = 100m },
            Rule("Housing", "Percentage", RuleSeverity.Warning, "Housing is 38.0% of income (max 40%)."));

        var insight = await _service.GenerateInsightAsync(context);

        Assert.Contains("Housing is close to its budget", insight);
    }

    [Fact]
    public async Task GenerateInsightAsync_ReturnsBreachedMessage()
    {
        var context = Context(
            new ReportData { NetAmount = -20m },
            Rule("Eating Out", "Hard Cap", RuleSeverity.Breached, "Eating Out is over budget."));

        var insight = await _service.GenerateInsightAsync(context);

        Assert.Contains("Eating Out needs attention", insight);
    }

    [Fact]
    public async Task GenerateInsightAsync_ReturnsLargestCategoryMessage()
    {
        var report = new ReportData
        {
            NetAmount = 500m,
            ByCategory = new Dictionary<string, decimal>
            {
                ["Groceries"] = 120m,
                ["Transport"] = 40m
            }
        };

        var insight = await _service.GenerateInsightAsync(Context(report));

        Assert.Contains("Groceries is the largest expense category", insight);
    }

    [Fact]
    public async Task GenerateInsightAsync_IsDeterministic()
    {
        var context = Context(new ReportData
        {
            NetAmount = 500m,
            ByCategory = new Dictionary<string, decimal> { ["Groceries"] = 120m }
        });

        var first = await _service.GenerateInsightAsync(context);
        var second = await _service.GenerateInsightAsync(context);

        Assert.Equal(first, second);
    }

    private static InsightContext Context(ReportData report, params RuleResult[] rules)
        => new(
            report,
            new SpendingSummary("user-1", 2026, 6, report.TotalIncome, report.TotalExpenses, report.ByCategory),
            rules);

    private static RuleResult Rule(string category, string ruleName, RuleSeverity severity, string message)
        => new()
        {
            CategoryName = category,
            RuleName = ruleName,
            Severity = severity,
            Message = message
        };
}
