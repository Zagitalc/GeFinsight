using FinSight.Core.Interfaces;
using FinSight.Core.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinSight.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ITransactionRepository _transactions;
    private readonly IBudgetRuleEngine _ruleEngine;
    private readonly IClaudeService _claude;

    public HomeController(
        ITransactionRepository transactions,
        IBudgetRuleEngine ruleEngine,
        IClaudeService claude)
    {
        _transactions = transactions;
        _ruleEngine   = ruleEngine;
        _claude       = claude;
    }

    // GET /
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var today  = DateTime.Today;

        // Fetch this month's transactions
        var txns = (await _transactions.GetByUserAndMonthAsync(userId, today.Year, today.Month)).ToList();

        // Build monthly report using ReportGenerator hierarchy
        var report = new MonthlySummaryReport(txns, userId, today.Year, today.Month).Generate();

        // Run rule engine — evaluates all active budget rules polymorphically
        var ruleResults = (await _ruleEngine.EvaluateAllAsync(userId)).ToList();

        // Build spending summary for Claude
        var summary = new SpendingSummary(
            userId,
            today.Year,
            today.Month,
            report.TotalIncome,
            report.TotalExpenses,
            report.ByCategory
        );

        // Get AI insight (cached per session in production — keep costs low)
        string insight;
        try   { insight = await _claude.GetSpendingInsightAsync(summary, ruleResults); }
        catch { insight = string.Empty; }  // Graceful fallback if API unavailable

        ViewBag.Report      = report;
        ViewBag.RuleResults = ruleResults;
        ViewBag.Insight     = insight;

        return View();
    }
}

[Authorize]
public class TransactionsController : Controller
{
    private readonly ITransactionRepository _transactions;
    private readonly ICategoryRepository _categories;

    public TransactionsController(ITransactionRepository transactions, ICategoryRepository categories)
    {
        _transactions = transactions;
        _categories   = categories;
    }

    // GET /Transactions
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var y = year  ?? DateTime.Today.Year;
        var m = month ?? DateTime.Today.Month;

        var txns = await _transactions.GetByUserAndMonthAsync(userId, y, m);
        ViewBag.Year  = y;
        ViewBag.Month = m;
        return View(txns);
    }

    // GET /Transactions/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _categories.GetAllAsync();
        return View();
    }

    // POST /Transactions/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Core.Domain.Transaction model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _categories.GetAllAsync();
            return View(model);
        }

        model.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _transactions.AddAsync(model);
        return RedirectToAction(nameof(Index));
    }

    // POST /Transactions/Delete/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _transactions.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    // GET /Transactions/Export?format=csv
    public async Task<IActionResult> Export(
        [FromServices] IExportStrategy export, string format = "csv")
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var txns   = await _transactions.GetByUserAsync(userId);
        var bytes  = await export.ExportAsync(txns);
        return File(bytes, export.ContentType, $"finsight-export.{export.FileExtension}");
    }
}

[Authorize]
public class BudgetController : Controller
{
    private readonly IBudgetRepository _budgets;

    public BudgetController(IBudgetRepository budgets) => _budgets = budgets;

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await _budgets.GetByUserAsync(userId));
    }
}
