using BoricuaBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoricuaBite.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BoricuaBiteDbContext))]
[Migration("20260912013000_StripeWebhookIdempotency")]
public partial class StripeWebhookIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StripeWebhookEvents",
            columns: table => new
            {
                EventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                EventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StripeWebhookEvents", x => x.EventId);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StripeWebhookEvents");
    }
}
