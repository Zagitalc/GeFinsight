using FinSight.Core.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Transaction>          Transactions { get; set; }
    public DbSet<RecurringTransaction>  RecurringTransactions { get; set; }
    public DbSet<Category>             Categories { get; set; }
    public DbSet<Budget>               Budgets { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Transaction
        builder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            e.Property(t => t.CategoryName).HasMaxLength(100).IsRequired();
            e.Property(t => t.Note).HasMaxLength(500);
            e.HasOne(t => t.User)
             .WithMany(u => u.Transactions)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Ignore(t => t.Category);
        });

        // RecurringTransaction (TPH inheritance — single table)
        builder.Entity<RecurringTransaction>()
            .HasBaseType<Transaction>();

        // Category
        builder.Entity<Category>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
            e.Ignore(c => c.Transactions);
        });

        // Budget
        builder.Entity<Budget>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.LimitAmount).HasColumnType("decimal(18,2)");
            e.Property(b => b.CategoryName).HasMaxLength(100).IsRequired();
            e.HasOne(b => b.User)
             .WithMany(u => u.Budgets)
             .HasForeignKey(b => b.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed system categories
        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Housing",      Icon = "🏠", Colour = "#4e79a7", IsSystem = true },
            new Category { Id = 2, Name = "Groceries",    Icon = "🛒", Colour = "#59a14f", IsSystem = true },
            new Category { Id = 3, Name = "Transport",    Icon = "🚌", Colour = "#f28e2b", IsSystem = true },
            new Category { Id = 4, Name = "Eating Out",   Icon = "🍽️", Colour = "#e15759", IsSystem = true },
            new Category { Id = 5, Name = "Entertainment",Icon = "🎬", Colour = "#76b7b2", IsSystem = true },
            new Category { Id = 6, Name = "Health",       Icon = "💊", Colour = "#edc948", IsSystem = true },
            new Category { Id = 7, Name = "Savings",      Icon = "💰", Colour = "#b07aa1", IsSystem = true },
            new Category { Id = 8, Name = "Income",       Icon = "📥", Colour = "#59a14f", IsSystem = true },
            new Category { Id = 9, Name = "Other",        Icon = "📦", Colour = "#9c755f", IsSystem = true }
        );
    }
}
