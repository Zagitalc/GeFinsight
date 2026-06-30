using GeFinsight.Core.Domain;
using GeFinsight.Infrastructure.Data;
using GeFinsight.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GeFinsight.Infrastructure.Tests;

public class RepositoryTests
{
    [Fact]
    public async Task TransactionRepository_FiltersTransactionsByUserAndMonth()
    {
        await using var fixture = await DbFixture.CreateAsync();
        fixture.Db.Transactions.AddRange(
            Txn("user-1", -20m, DateTime.Today),
            Txn("user-1", -30m, DateTime.Today.AddMonths(-1)),
            Txn("user-2", -40m, DateTime.Today));
        await fixture.Db.SaveChangesAsync();

        var repo = new TransactionRepository(fixture.Db);
        var results = await repo.GetByUserAndMonthAsync("user-1", DateTime.Today.Year, DateTime.Today.Month);

        var only = Assert.Single(results);
        Assert.Equal("user-1", only.UserId);
        Assert.Equal(-20m, only.Amount);
    }

    [Fact]
    public async Task BudgetRepository_ReturnsOnlyRequestedUsersBudgets()
    {
        await using var fixture = await DbFixture.CreateAsync();
        fixture.Db.Budgets.AddRange(
            Budget("user-1", "Groceries"),
            Budget("user-2", "Transport"));
        await fixture.Db.SaveChangesAsync();

        var repo = new BudgetRepository(fixture.Db);
        var results = await repo.GetByUserAsync("user-1");

        var only = Assert.Single(results);
        Assert.Equal("Groceries", only.CategoryName);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotDeleteAnotherUsersTransaction()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var otherUsersTransaction = Txn("user-2", -20m, DateTime.Today);
        fixture.Db.Transactions.Add(otherUsersTransaction);
        await fixture.Db.SaveChangesAsync();

        var repo = new TransactionRepository(fixture.Db);
        await repo.DeleteAsync(otherUsersTransaction.Id, "user-1");

        Assert.NotNull(await fixture.Db.Transactions.FindAsync(otherUsersTransaction.Id));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotDeleteAnotherUsersBudget()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var otherUsersBudget = Budget("user-2", "Transport");
        fixture.Db.Budgets.Add(otherUsersBudget);
        await fixture.Db.SaveChangesAsync();

        var repo = new BudgetRepository(fixture.Db);
        await repo.DeleteAsync(otherUsersBudget.Id, "user-1");

        Assert.NotNull(await fixture.Db.Budgets.FindAsync(otherUsersBudget.Id));
    }

    private static Transaction Txn(string userId, decimal amount, DateTime date)
        => new()
        {
            UserId = userId,
            Amount = amount,
            CategoryName = "Groceries",
            Date = date,
            Type = TransactionType.Expense,
            Note = "Test"
        };

    private static Budget Budget(string userId, string category)
        => new()
        {
            UserId = userId,
            CategoryName = category,
            LimitAmount = 100m,
            RuleType = BudgetRuleType.HardCap,
            IsActive = true
        };

    private sealed class DbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Db { get; }

        private DbFixture(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<DbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(
                new AppUser { Id = "user-1", UserName = "user1@test.local", Email = "user1@test.local", DisplayName = "User One" },
                new AppUser { Id = "user-2", UserName = "user2@test.local", Email = "user2@test.local", DisplayName = "User Two" });
            await db.SaveChangesAsync();

            return new DbFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
