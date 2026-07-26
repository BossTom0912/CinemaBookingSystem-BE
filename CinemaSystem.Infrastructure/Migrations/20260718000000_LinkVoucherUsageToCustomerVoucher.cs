using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260718000000_LinkVoucherUsageToCustomerVoucher")]
public partial class LinkVoucherUsageToCustomerVoucher : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF to_regclass('"VOUCHER_USAGE"') IS NULL
                   OR to_regclass('"CUSTOMER_VOUCHER"') IS NULL THEN
                    RAISE EXCEPTION 'Voucher usage or customer voucher table is missing. Restore the PostgreSQL staging backup before applying migrations.';
                END IF;
            END $$;

            ALTER TABLE "VOUCHER_USAGE"
                ADD COLUMN IF NOT EXISTS "customerVoucherId" varchar(50);

            WITH exact_claim AS
            (
                SELECT
                    usage."voucherUsageId",
                    min(claim."customerVoucherId") AS "customerVoucherId"
                FROM "VOUCHER_USAGE" AS usage
                INNER JOIN "CUSTOMER_VOUCHER" AS claim
                    ON claim."voucherId" = usage."voucherId"
                   AND claim."customerProfileId" = usage."customerProfileId"
                   AND claim."isUsed" = true
                WHERE usage."customerVoucherId" IS NULL
                  AND usage."usageStatus" IN ('APPLIED', 'CONFIRMED')
                GROUP BY usage."voucherUsageId"
                HAVING count(*) = 1
            )
            UPDATE "VOUCHER_USAGE" AS usage
            SET "customerVoucherId" = exact_claim."customerVoucherId"
            FROM exact_claim
            WHERE exact_claim."voucherUsageId" = usage."voucherUsageId";

            UPDATE "CUSTOMER_VOUCHER" AS claim
            SET "isUsed" = false,
                "usedAt" = NULL
            FROM "VOUCHER_USAGE" AS usage
            WHERE usage."customerVoucherId" = claim."customerVoucherId"
              AND usage."usageStatus" = 'APPLIED'
              AND claim."isUsed" = true;

            DO $$
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM "VOUCHER_USAGE"
                    WHERE "customerVoucherId" IS NOT NULL
                      AND "usageStatus" <> 'CANCELLED'
                    GROUP BY "customerVoucherId"
                    HAVING count(*) > 1
                ) THEN
                    RAISE EXCEPTION 'A customer voucher is linked to multiple non-cancelled usages. Reconcile the staging data before continuing.';
                END IF;

                IF NOT EXISTS
                (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_VOUCHER_USAGE_CUSTOMER_VOUCHER'
                ) THEN
                    ALTER TABLE "VOUCHER_USAGE"
                        ADD CONSTRAINT "FK_VOUCHER_USAGE_CUSTOMER_VOUCHER"
                        FOREIGN KEY ("customerVoucherId")
                        REFERENCES "CUSTOMER_VOUCHER" ("customerVoucherId");
                END IF;
            END $$;

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER"
                ON "VOUCHER_USAGE" ("customerVoucherId")
                WHERE "customerVoucherId" IS NOT NULL
                  AND "usageStatus" <> 'CANCELLED';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS "UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER";
            ALTER TABLE "VOUCHER_USAGE"
                DROP CONSTRAINT IF EXISTS "FK_VOUCHER_USAGE_CUSTOMER_VOUCHER";
            ALTER TABLE "VOUCHER_USAGE"
                DROP COLUMN IF EXISTS "customerVoucherId";
            """);
    }
}
