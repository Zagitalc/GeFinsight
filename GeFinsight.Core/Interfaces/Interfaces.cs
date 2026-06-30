using GeFinsight.Core.Domain;
using GeFinsight.Core.Rules;

namespace GeFinsight.Core.Interfaces;

// ─────────────────────────────────────────────
// Repository interfaces — Infrastructure implements these,
// Web and Core depend only on the interface (DIP)
// ─────────────────────────────────────────────

public interface ITransactionRepository
{
    Task<IEnumerable<Transaction>> GetByUserAsync(string userId);
    Task<IEnumerable<Transaction>> GetByUserAndMonthAsync(string userId, int year, int month);
    Task<IEnumerable<Transaction>> GetByUserAndCategoryAsync(string userId, string category);
    Task<Transaction?> GetByIdAsync(int id);
    Task<Transaction?> GetByIdForUserAsync(int id, string userId);
    Task AddAsync(Transaction transaction);
    Task UpdateAsync(Transaction transaction);
    Task DeleteAsync(int id, string userId);

    // Aggregation queries — used by reports and rule engine
    Task<decimal> GetTotalByUserAndCategoryAsync(string userId, string category, DateTime from, DateTime to);
    Task<decimal> GetMonthlyIncomeAsync(string userId, int year, int month);
}

public interface IBudgetRepository
{
    Task<IEnumerable<Budget>> GetByUserAsync(string userId);
    Task<Budget?> GetByIdAsync(int id);
    Task<Budget?> GetByIdForUserAsync(int id, string userId);
    Task AddAsync(Budget budget);
    Task UpdateAsync(Budget budget);
    Task DeleteAsync(int id, string userId);
}

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetAllAsync();
    Task<Category?> GetByNameAsync(string name);
    Task AddAsync(Category category);
}

// ─────────────────────────────────────────────
// Budget rule interface — the heart of the OOP design.
// Every rule type implements this contract identically;
// the engine calls Evaluate() without knowing which rule it has.
// ─────────────────────────────────────────────

public interface IBudgetRule
{
    string RuleName { get; }
    string CategoryName { get; }
    decimal LimitAmount { get; }

    /// <summary>
    /// Evaluate the rule against a set of transactions.
    /// Returns a result regardless of rule type — polymorphism in action.
    /// </summary>
    RuleResult Evaluate(IEnumerable<Transaction> transactions);
}

// ─────────────────────────────────────────────
// Service interfaces
// ─────────────────────────────────────────────

public interface IBudgetRuleEngine
{
    /// <summary>
    /// Builds rule objects from stored Budget entities and evaluates all of them.
    /// Returns one RuleResult per active budget.
    /// </summary>
    Task<IEnumerable<RuleResult>> EvaluateAllAsync(string userId);
}

public interface IClaudeService
{
    /// <summary>
    /// Sends a spending summary + rule results to Claude and returns
    /// a plain-English analysis paragraph for the dashboard.
    /// </summary>
    Task<string> GetSpendingInsightAsync(SpendingSummary summary, IEnumerable<RuleResult> ruleResults);
}

public interface IExportStrategy
{
    string ContentType { get; }
    string FileExtension { get; }
    Task<byte[]> ExportAsync(IEnumerable<Transaction> transactions);
}

// ─────────────────────────────────────────────
// Supporting data shapes
// ─────────────────────────────────────────────

public record SpendingSummary(
    string UserId,
    int Year,
    int Month,
    decimal TotalIncome,
    decimal TotalExpenses,
    Dictionary<string, decimal> ByCategory
);
