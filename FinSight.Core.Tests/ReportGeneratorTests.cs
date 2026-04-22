using FinSight.Core.Domain;
using FinSight.Core.Reports;
using Xunit;

namespace FinSight.Core.Tests;

public class ReportGeneratorTests
{
    [Fact]
    public void MonthlySummaryReport_CalculatesIncomeExpensesAndNet()
    {
        var today = DateTime.Today;
        var report = new MonthlySummaryReport(new[]
        {
            Txn(2500m, "Income", TransactionType.Income, today),
            Txn(-60m, "Groceries", TransactionType.Expense, today),
            Txn(-40m, "Groceries", TransactionType.Expense, today),
            Txn(-99m, "Groceries", TransactionType.Expense, today.AddMonths(-1))
        }, "user-1", today.Year, today.Month).Generate();

        Assert.Equal(2500m, report.TotalIncome);
        Assert.Equal(100m, report.TotalExpenses);
        Assert.Equal(2400m, report.NetAmount);
        Assert.Equal(100m, report.ByCategory["Groceries"]);
    }

    [Fact]
    public void TrendReport_GroupsExpensesByMonth()
    {
        var today = DateTime.Today;

        var report = new TrendReport(new[]
        {
            Txn(-30m, "Transport", TransactionType.Expense, today),
            Txn(-20m, "Eating Out", TransactionType.Expense, today),
            Txn(2500m, "Income", TransactionType.Income, today)
        }, "user-1", months: 6).Generate();

        var label = new DateTime(today.Year, today.Month, 1).ToString("MMM yyyy");
        Assert.Equal(50m, report.MonthlyTrend[label]);
        Assert.Empty(report.Rows);
    }

    private static Transaction Txn(decimal amount, string category, TransactionType type, DateTime date)
        => new()
        {
            UserId = "user-1",
            Amount = amount,
            CategoryName = category,
            Type = type,
            Date = date,
            Note = category
        };
}
