using BoricuaBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BoricuaBite.Api.Tests;

public class PostgresMigrationTests
{
    [Fact]
    public void PostgresMigrations_MatchMarketplaceModelAndGenerateIdempotentSql()
    {
        // SQL generation does not open a connection to this placeholder database.
        var options = new DbContextOptionsBuilder<BoricuaBiteDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_validation;Username=unused")
            .Options;
        using var db = new BoricuaBiteDbContext(options);

        Assert.True(db.Database.GetMigrations().Count() >= 2);
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE \"AspNetUsers\"", sql);
        Assert.Contains("CREATE TABLE \"Restaurants\"", sql);
        Assert.Contains("CREATE TABLE \"MenuItems\"", sql);
        Assert.Contains("CREATE TABLE \"MarketplaceOrders\"", sql);
        Assert.Contains("CREATE TABLE \"MarketplaceOrderItems\"", sql);
        Assert.Contains("CREATE TABLE \"RestaurantReviews\"", sql);
        Assert.Contains("StripePaymentIntentId", sql);
        Assert.Contains("CommissionAmount", sql);
        Assert.Contains("REFERENCES \"AspNetUsers\"", sql);
        Assert.Contains("numeric(12,2)", sql);
    }
}
