using FinSight.Core.Domain;

namespace FinSight.Core.Reports;

// ─────────────────────────────────────────────
// Abstract base — defines the template method pattern.
// All reports share BuildHeader() and BuildFooter();
// each subclass implements Generate() differently.
// ─────────────────────────────────────────────

public abstract class ReportGenerator
{
    protected readonly IEnumerable<Transaction> Transactions;
    protected readonly string UserId;

    protected ReportGenerator(IEnumerable<Transaction> transactions, string userId)
    {
        Transactions = transactions;
        UserId = userId;
    }

    public abstract ReportData Generate();

    protected string BuildHeader(string title)
        => $"{title} — Generated {DateTime.Now:dd MMM yyyy HH:mm}";

    protected Dictionary<string, decimal> GroupByCategory()
        => Transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.CategoryName)
            .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

    protected static Dictionary<string, decimal> GroupByCategory(IEnumerable<Transaction> transactions)
        => transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.CategoryName)
            .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));
}

// ─────────────────────────────────────────────
// Monthly summary — totals for a given month
// ─────────────────────────────────────────────

public class MonthlySummaryReport : ReportGenerator
{
    private readonly int _year, _month;

    public MonthlySummaryReport(IEnumerable<Transaction> transactions, string userId, int year, int month)
        : base(transactions, userId)
    {
        _year = year;
        _month = month;
    }

    public override ReportData Generate()
    {
        var filtered = Transactions.Where(t => t.Date.Year == _year && t.Date.Month == _month);
        var income   = filtered.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expenses = filtered.Where(t => t.Type == TransactionType.Expense).Sum(t => Math.Abs(t.Amount));

        return new ReportData
        {
            Title      = BuildHeader($"Monthly Summary — {new DateTime(_year, _month, 1):MMMM yyyy}"),
            TotalIncome   = income,
            TotalExpenses = expenses,
            NetAmount     = income - expenses,
            ByCategory    = GroupByCategory(filtered),
            Rows          = filtered.OrderByDescending(t => t.Date)
                                    .Select(ReportRow.From)
                                    .ToList()
        };
    }
}

// ─────────────────────────────────────────────
// Category breakdown — all-time for one category
// ─────────────────────────────────────────────

public class CategoryBreakdownReport : ReportGenerator
{
    private readonly string _category;

    public CategoryBreakdownReport(IEnumerable<Transaction> transactions, string userId, string category)
        : base(transactions, userId)
    {
        _category = category;
    }

    public override ReportData Generate()
    {
        var filtered = Transactions
            .Where(t => t.CategoryName.Equals(_category, StringComparison.OrdinalIgnoreCase));

        var total = filtered.Sum(t => Math.Abs(t.Amount));

        return new ReportData
        {
            Title         = BuildHeader($"Category Breakdown — {_category}"),
            TotalExpenses = total,
            ByCategory    = new Dictionary<string, decimal> { [_category] = total },
            Rows          = filtered.OrderByDescending(t => t.Date)
                                    .Select(ReportRow.From)
                                    .ToList()
        };
    }
}

// ─────────────────────────────────────────────
// Trend report — monthly totals over last N months
// ─────────────────────────────────────────────

public class TrendReport : ReportGenerator
{
    private readonly int _months;

    public TrendReport(IEnumerable<Transaction> transactions, string userId, int months = 6)
        : base(transactions, userId)
    {
        _months = months;
    }

    public override ReportData Generate()
    {
        var cutoff = DateTime.Today.AddMonths(-_months);
        var monthly = Transactions
            .Where(t => t.Date >= cutoff && t.Type == TransactionType.Expense)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .ToDictionary(
                g => $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMM yyyy}",
                g => g.Sum(t => Math.Abs(t.Amount))
            );

        return new ReportData
        {
            Title       = BuildHeader($"Spending Trend — Last {_months} Months"),
            MonthlyTrend = monthly,
            ByCategory   = GroupByCategory(),
            Rows         = new List<ReportRow>()
        };
    }
}

// ─────────────────────────────────────────────
// Shared output shapes
// ─────────────────────────────────────────────

public class ReportData
{
    public string Title { get; init; } = string.Empty;
    public decimal TotalIncome { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal NetAmount { get; init; }
    public Dictionary<string, decimal> ByCategory { get; init; } = new();
    public Dictionary<string, decimal> MonthlyTrend { get; init; } = new();
    public List<ReportRow> Rows { get; init; } = new();
}

public class ReportRow
{
    public DateTime Date { get; init; }
    public string Category { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Note { get; init; } = string.Empty;
    public TransactionType Type { get; init; }

    public static ReportRow From(Transaction t) => new()
    {
        Date     = t.Date,
        Category = t.CategoryName,
        Amount   = t.Amount,
        Note     = t.Note,
        Type     = t.Type
    };
}
