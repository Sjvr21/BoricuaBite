using BoricuaBite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.DataProtection;

namespace BoricuaBite.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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
