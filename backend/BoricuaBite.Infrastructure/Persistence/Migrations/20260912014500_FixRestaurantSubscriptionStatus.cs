using BoricuaBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoricuaBite.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BoricuaBiteDbContext))]
[Migration("20260912014500_FixRestaurantSubscriptionStatus")]
public partial class FixRestaurantSubscriptionStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"Restaurants\" SET \"SubscriptionStatus\" = 'Pending' WHERE \"SubscriptionStatus\" = '';");
        migrationBuilder.Sql("ALTER TABLE \"Restaurants\" ALTER COLUMN \"SubscriptionStatus\" SET DEFAULT 'Pending';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Restaurants\" ALTER COLUMN \"SubscriptionStatus\" DROP DEFAULT;");
    }
}
