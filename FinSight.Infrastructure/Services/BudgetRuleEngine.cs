using FinSight.Core.Domain;
using FinSight.Core.Interfaces;
using FinSight.Core.Rules;

namespace FinSight.Infrastructure.Services;

// ─────────────────────────────────────────────
// BudgetRuleEngine — fetches budgets and transactions,
// builds the right rule object via the factory,
// then evaluates all rules returning a result list.
//
// Callers (controllers) never touch rule classes directly —
// they just get back a list of RuleResults. 
// ─────────────────────────────────────────────

public class BudgetRuleEngine : IBudgetRuleEngine
{
    private readonly IBudgetRepository _budgets;
    private readonly ITransactionRepository _transactions;

    public BudgetRuleEngine(IBudgetRepository budgets, ITransactionRepository transactions)
    {
        _budgets      = budgets;
        _transactions = transactions;
    }

    public async Task<IEnumerable<RuleResult>> EvaluateAllAsync(string userId)
    {
        var today    = DateTime.Today;
        var budgets  = (await _budgets.GetByUserAsync(userId)).Where(b => b.IsActive);
        var txns     = await _transactions.GetByUserAndMonthAsync(userId, today.Year, today.Month);
        var income   = await _transactions.GetMonthlyIncomeAsync(userId, today.Year, today.Month);

        var results = new List<RuleResult>();

        foreach (var budget in budgets)
        {
            // Factory builds the correct concrete rule type —
            // the engine doesn't need an if/switch here
            var rule   = BudgetRuleFactory.Create(budget, income);
            var result = rule.Evaluate(txns);
            results.Add(result);
        }

        return results.OrderByDescending(r => r.Severity);
    }
}
