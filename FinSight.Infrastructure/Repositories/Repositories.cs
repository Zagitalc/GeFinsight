using FinSight.Core.Domain;
using FinSight.Core.Interfaces;
using FinSight.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _db;

    public TransactionRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Transaction>> GetByUserAsync(string userId)
        => await _db.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ToListAsync();

    public async Task<IEnumerable<Transaction>> GetByUserAndMonthAsync(string userId, int year, int month)
        => await _db.Transactions
            .Where(t => t.UserId == userId
                     && t.Date.Year  == year
                     && t.Date.Month == month)
            .OrderByDescending(t => t.Date)
            .ToListAsync();

    public async Task<IEnumerable<Transaction>> GetByUserAndCategoryAsync(string userId, string category)
        => await _db.Transactions
            .Where(t => t.UserId == userId
                     && t.CategoryName == category)
            .OrderByDescending(t => t.Date)
            .ToListAsync();

    public async Task<Transaction?> GetByIdAsync(int id)
        => await _db.Transactions.FindAsync(id);

    public async Task<Transaction?> GetByIdForUserAsync(int id, string userId)
        => await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

    public async Task AddAsync(Transaction transaction)
    {
        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Transaction transaction)
    {
        _db.Transactions.Update(transaction);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var t = await GetByIdForUserAsync(id, userId);
        if (t is not null)
        {
            _db.Transactions.Remove(t);
            await _db.SaveChangesAsync();
        }
    }

    // ── Aggregation queries used by rule engine and reports ──

    public async Task<decimal> GetTotalByUserAndCategoryAsync(
        string userId, string category, DateTime from, DateTime to)
    {
        var amounts = await _db.Transactions
            .Where(t => t.UserId      == userId
                     && t.CategoryName == category
                     && t.Type         == TransactionType.Expense
                     && t.Date         >= from
                     && t.Date         <= to)
            .Select(t => t.Amount)
            .ToListAsync();
        return amounts.Sum(a => Math.Abs(a));
    }

    public async Task<decimal> GetMonthlyIncomeAsync(string userId, int year, int month)
    {
        var amounts = await _db.Transactions
            .Where(t => t.UserId     == userId
                     && t.Type        == TransactionType.Income
                     && t.Date.Year   == year
                     && t.Date.Month  == month)
            .Select(t => t.Amount)
            .ToListAsync();
        return amounts.Sum();
    }
}

public class BudgetRepository : IBudgetRepository
{
    private readonly AppDbContext _db;

    public BudgetRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Budget>> GetByUserAsync(string userId)
        => await _db.Budgets
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.CategoryName)
            .ToListAsync();

    public async Task<Budget?> GetByIdAsync(int id)
        => await _db.Budgets.FindAsync(id);

    public async Task<Budget?> GetByIdForUserAsync(int id, string userId)
        => await _db.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

    public async Task AddAsync(Budget budget)
    {
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Budget budget)
    {
        _db.Budgets.Update(budget);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var b = await GetByIdForUserAsync(id, userId);
        if (b is not null) { _db.Budgets.Remove(b); await _db.SaveChangesAsync(); }
    }
}

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;
    public CategoryRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Category>> GetAllAsync()
        => await _db.Categories.OrderBy(c => c.Name).ToListAsync();

    public async Task<Category?> GetByNameAsync(string name)
        => await _db.Categories.FirstOrDefaultAsync(c => c.Name == name);

    public async Task AddAsync(Category category)
    {
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
    }
}
