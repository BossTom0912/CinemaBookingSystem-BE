using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260721020000_AddVoucherIsPrivateAndRequiredTicketCount")]
public partial class AddVoucherIsPrivateAndRequiredTicketCount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "VOUCHER"
                ADD COLUMN IF NOT EXISTS "isPrivate" boolean NOT NULL DEFAULT false;
            ALTER TABLE "VOUCHER"
                ADD COLUMN IF NOT EXISTS "requiredTicketCount" integer;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "VOUCHER" DROP COLUMN IF EXISTS "isPrivate";
            ALTER TABLE "VOUCHER" DROP COLUMN IF EXISTS "requiredTicketCount";
            """);
    }
}
