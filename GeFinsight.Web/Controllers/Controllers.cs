using GeFinsight.Core.Domain;
using GeFinsight.Core.Interfaces;
using GeFinsight.Core.Reports;
using GeFinsight.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace GeFinsight.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ITransactionRepository _transactions;
    private readonly IBudgetRuleEngine _ruleEngine;
    private readonly IInsightService _insights;
    private readonly InsightDisplayOptions _insightDisplay;

    public HomeController(
        ITransactionRepository transactions,
        IBudgetRuleEngine ruleEngine,
        IInsightService insights,
        InsightDisplayOptions insightDisplay)
    {
        _transactions   = transactions;
        _ruleEngine     = ruleEngine;
        _insights       = insights;
        _insightDisplay = insightDisplay;
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

        var summary = new SpendingSummary(
            userId,
            today.Year,
            today.Month,
            report.TotalIncome,
            report.TotalExpenses,
            report.ByCategory
        );

        var insight = await _insights.GenerateInsightAsync(
            new InsightContext(report, summary, ruleResults),
            HttpContext.RequestAborted);

        ViewBag.Report      = report;
        ViewBag.RuleResults = ruleResults;
        ViewBag.Insight     = insight;
        ViewBag.InsightHeading = _insightDisplay.Heading;

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
        return View(new TransactionFormViewModel
        {
            Categories = await BuildCategoryOptionsAsync()
        });
    }

    // POST /Transactions/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Categories = await BuildCategoryOptionsAsync();
            return View(model);
        }

        var signedAmount = Math.Abs(model.Amount);
        if (model.Type == TransactionType.Expense)
            signedAmount *= -1;

        await _transactions.AddAsync(new Core.Domain.Transaction
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            Date = model.Date,
            CategoryName = model.CategoryName,
            Type = model.Type,
            Amount = signedAmount,
            Note = model.Note ?? string.Empty
        });

        return RedirectToAction(nameof(Index));
    }

    // POST /Transactions/Delete/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _transactions.DeleteAsync(id, userId);
        return RedirectToAction(nameof(Index));
    }

    // GET /Transactions/Export?format=csv
    public async Task<IActionResult> Export(
        [FromServices] IExportStrategy export, string format = "csv")
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var txns   = await _transactions.GetByUserAsync(userId);
        var bytes  = await export.ExportAsync(txns);
        return File(bytes, export.ContentType, $"gefinsight-export.{export.FileExtension}");
    }

    private async Task<IEnumerable<SelectListItem>> BuildCategoryOptionsAsync()
        => (await _categories.GetAllAsync())
            .Select(c => new SelectListItem($"{c.Icon} {c.Name}", c.Name));
}

[Authorize]
public class BudgetController : Controller
{
    private readonly IBudgetRepository _budgets;
    private readonly ICategoryRepository _categories;

    public BudgetController(IBudgetRepository budgets, ICategoryRepository categories)
    {
        _budgets = budgets;
        _categories = categories;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await _budgets.GetByUserAsync(userId));
    }

    public async Task<IActionResult> Create()
        => View("Form", new BudgetFormViewModel
        {
            Categories = await BuildCategoryOptionsAsync()
        });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BudgetFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Categories = await BuildCategoryOptionsAsync();
            return View("Form", model);
        }

        await _budgets.AddAsync(new Budget
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            CategoryName = model.CategoryName,
            RuleType = model.RuleType,
            LimitAmount = model.LimitAmount,
            IsActive = model.IsActive
        });

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var budget = await _budgets.GetByIdForUserAsync(id, userId);
        if (budget is null) return NotFound();

        return View("Form", new BudgetFormViewModel
        {
            Id = budget.Id,
            CategoryName = budget.CategoryName,
            RuleType = budget.RuleType,
            LimitAmount = budget.LimitAmount,
            IsActive = budget.IsActive,
            Categories = await BuildCategoryOptionsAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BudgetFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.Categories = await BuildCategoryOptionsAsync();
            return View("Form", model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var budget = await _budgets.GetByIdForUserAsync(id, userId);
        if (budget is null) return NotFound();

        budget.CategoryName = model.CategoryName;
        budget.RuleType = model.RuleType;
        budget.LimitAmount = model.LimitAmount;
        budget.IsActive = model.IsActive;
        await _budgets.UpdateAsync(budget);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _budgets.DeleteAsync(id, userId);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<SelectListItem>> BuildCategoryOptionsAsync()
        => (await _categories.GetAllAsync())
            .Where(c => c.Name != "Income")
            .Select(c => new SelectListItem($"{c.Icon} {c.Name}", c.Name));
}
