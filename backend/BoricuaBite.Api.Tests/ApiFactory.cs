using BoricuaBite.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BoricuaBite.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Email"] = "admin@example.com",
                ["MarketplacePricing:TaxRate"] = "0.10",
                ["MarketplacePricing:CustomerServiceFeeRate"] = "0.05",
                ["MarketplacePricing:CustomerServiceFeeFlat"] = "1.00",
                ["MarketplacePricing:RestaurantCommissionRate"] = "0.10"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BoricuaBiteDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BoricuaBiteDbContext>>();
            connection.Open();
            services.AddDbContext<BoricuaBiteDbContext>(options => options.UseSqlite(connection));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }

    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<BoricuaBiteDbContext>().Database.EnsureCreated();
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}
