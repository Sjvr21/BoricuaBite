using BoricuaBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BoricuaBite.Api.Tests;

public class PostgresMigrationTests
{
    [Fact]
    public void PostgresMigration_MatchesModelAndGeneratesIdempotentSql()
    {
        // SQL generation does not open a connection to this placeholder database.
        var options = new DbContextOptionsBuilder<BoricuaBiteDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_validation;Username=unused")
            .Options;
        using var db = new BoricuaBiteDbContext(options);

        Assert.Single(db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE \"AspNetUsers\"", sql);
        Assert.Contains("CREATE TABLE \"Restaurants\"", sql);
        Assert.Contains("CREATE TABLE \"MenuItems\"", sql);
        Assert.Contains("REFERENCES \"AspNetUsers\"", sql);
        Assert.Contains("numeric(12,2)", sql);
    }
}
