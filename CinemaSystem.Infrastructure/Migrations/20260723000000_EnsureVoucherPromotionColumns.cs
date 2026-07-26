using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260723000000_EnsureVoucherPromotionColumns")]
public partial class EnsureVoucherPromotionColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF to_regclass('"VOUCHER"') IS NULL THEN
                    RAISE EXCEPTION 'Voucher table is missing. Restore the PostgreSQL staging backup before applying migrations.';
                END IF;
            END $$;

            ALTER TABLE "VOUCHER" ADD COLUMN IF NOT EXISTS "category" varchar(50);
            ALTER TABLE "VOUCHER" ADD COLUMN IF NOT EXISTS "applicableScope" varchar(50);
            ALTER TABLE "VOUCHER" ADD COLUMN IF NOT EXISTS "targetType" varchar(50);
            ALTER TABLE "VOUCHER" ADD COLUMN IF NOT EXISTS "targetCustomerIds" text;
            ALTER TABLE "VOUCHER" ADD COLUMN IF NOT EXISTS "specificFbItemIds" text;
            ALTER TABLE "VOUCHER" ADD COLUMN IF NOT EXISTS "isPrivate" boolean NOT NULL DEFAULT false;
            ALTER TABLE "VOUCHER" ADD COLUMN IF NOT EXISTS "requiredTicketCount" integer;

            UPDATE "VOUCHER" SET "category" = 'EVENT' WHERE "category" IS NULL;
            UPDATE "VOUCHER" SET "applicableScope" = 'TOTAL_ORDER' WHERE "applicableScope" IS NULL;
            UPDATE "VOUCHER" SET "targetType" = 'ALL_CUSTOMERS' WHERE "targetType" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This forward-only repair migration preserves existing voucher configuration.
    }
}
