using GeFinsight.Core.Domain;
using GeFinsight.Infrastructure.Data;
using GeFinsight.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GeFinsight.Infrastructure.Tests;

public class DemoDataSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesDemoDataOnce()
    {
        await using var fixture = await SeederFixture.CreateAsync(seedEnabled: true);

        await DemoDataSeeder.SeedAsync(fixture.Services);
        await DemoDataSeeder.SeedAsync(fixture.Services);

        var db = fixture.Services.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "demo@gefinsight.local");

        Assert.Equal(10, await db.Transactions.CountAsync(t => t.UserId == user.Id));
        Assert.Equal(4, await db.Budgets.CountAsync(b => b.UserId == user.Id));
    }

    [Fact]
    public async Task SeedAsync_DoesNothingWhenDisabled()
    {
        await using var fixture = await SeederFixture.CreateAsync(seedEnabled: false);

        await DemoDataSeeder.SeedAsync(fixture.Services);

        var db = fixture.Services.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Empty(await db.Budgets.ToListAsync());
    }

    private sealed class SeederFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public IServiceProvider Services => _provider;

        private SeederFixture(SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
        }

        public static async Task<SeederFixture> CreateAsync(bool seedEnabled)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DemoSeed:Enabled"] = seedEnabled.ToString(),
                    ["DemoSeed:Email"] = "demo@gefinsight.local",
                    ["DemoSeed:Password"] = "Demo12345!"
                })
                .Build());
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            services
                .AddIdentityCore<AppUser>(options =>
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireNonAlphanumeric = false;
                })
                .AddEntityFrameworkStores<AppDbContext>();

            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            return new SeederFixture(connection, provider);
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
