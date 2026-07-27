using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcilePostgresData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "ROLE" ("roleId", "roleName", description)
                VALUES
                    ('ROLE_CUSTOMER', 'CUSTOMER', 'Customer account'),
                    ('ROLE_STAFF', 'STAFF', 'Cinema staff account'),
                    ('ROLE_MANAGER', 'MANAGER', 'Cinema manager account'),
                    ('ROLE_ADMIN', 'ADMIN', 'System administrator account')
                ON CONFLICT ("roleId") DO UPDATE SET
                    "roleName" = EXCLUDED."roleName",
                    description = EXCLUDED.description;

                INSERT INTO "ROLE_PROVISIONING_POLICY"
                    ("roleId", "profileKind", "requiresCinema", "defaultStaffPosition", "isActive", "isPublicRegistrationAllowed")
                VALUES
                    ('ROLE_CUSTOMER', 'CUSTOMER', false, NULL, true, true),
                    ('ROLE_STAFF', 'STAFF', true, 'Staff', true, false),
                    ('ROLE_MANAGER', 'STAFF', true, 'Manager', true, false),
                    ('ROLE_ADMIN', 'NONE', false, NULL, true, false)
                ON CONFLICT ("roleId") DO UPDATE SET
                    "profileKind" = EXCLUDED."profileKind",
                    "requiresCinema" = EXCLUDED."requiresCinema",
                    "defaultStaffPosition" = EXCLUDED."defaultStaffPosition",
                    "isActive" = EXCLUDED."isActive",
                    "isPublicRegistrationAllowed" = EXCLUDED."isPublicRegistrationAllowed";

                INSERT INTO "ROLE_ASSIGNMENT_RULE" ("grantorRoleId", "granteeRoleId", "isActive")
                VALUES
                    ('ROLE_ADMIN', 'ROLE_CUSTOMER', true),
                    ('ROLE_ADMIN', 'ROLE_STAFF', true),
                    ('ROLE_ADMIN', 'ROLE_MANAGER', true)
                ON CONFLICT ("grantorRoleId", "granteeRoleId") DO UPDATE SET
                    "isActive" = EXCLUDED."isActive";

                UPDATE "VOUCHER" SET category = 'EVENT' WHERE category IS NULL;
                UPDATE "VOUCHER" SET "applicableScope" = 'TOTAL_ORDER' WHERE "applicableScope" IS NULL;
                UPDATE "VOUCHER" SET "targetType" = 'ALL_CUSTOMERS' WHERE "targetType" IS NULL;

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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only data reconciliation. Removing policy rows or unlinking
            // voucher usage during rollback could corrupt retained production data.
        }
    }
}
