using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GeFinsight.Web.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_ReturnsOk_WhenDatabaseIsAvailable()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gefinsight-health-{Guid.NewGuid():N}.db");

        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath}",
                            ["Insights:Mode"] = "Local",
                            ["DemoSeed:Enabled"] = "true",
                            ["DemoSeed:Email"] = "demo@gefinsight.local",
                            ["DemoSeed:Password"] = "Demo12345!"
                        });
                    });
                });

            var client = factory.CreateClient();
            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }
}
