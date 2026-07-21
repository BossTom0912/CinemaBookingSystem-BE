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
            IF OBJECT_ID(N'dbo.VOUCHER_USAGE', N'U') IS NULL
               OR OBJECT_ID(N'dbo.CUSTOMER_VOUCHER', N'U') IS NULL
                THROW 52100, 'Voucher usage or customer voucher table is missing. Apply the canonical database upgrade first.', 1;

            IF COL_LENGTH(N'dbo.VOUCHER_USAGE', N'customerVoucherId') IS NULL
                ALTER TABLE dbo.[VOUCHER_USAGE]
                    ADD [customerVoucherId] NVARCHAR(50) NULL;
            """);

        // SQL Server resolves column references when a command batch is
        // compiled, so statements that use the new column must be a later
        // migration command. EF still wraps both commands in one transaction.
        migrationBuilder.Sql(
            """
            ;WITH exactClaim AS
            (
                SELECT
                    usage.[voucherUsageId],
                    MIN(claim.[customerVoucherId]) AS [customerVoucherId]
                FROM dbo.[VOUCHER_USAGE] AS usage
                INNER JOIN dbo.[CUSTOMER_VOUCHER] AS claim
                    ON claim.[voucherId] = usage.[voucherId]
                   AND claim.[customerProfileId] = usage.[customerProfileId]
                   AND claim.[isUsed] = 1
                WHERE usage.[customerVoucherId] IS NULL
                  AND usage.[usageStatus] IN (N'APPLIED', N'CONFIRMED')
                GROUP BY usage.[voucherUsageId]
                HAVING COUNT_BIG(*) = 1
            )
            UPDATE usage
            SET [customerVoucherId] = exactClaim.[customerVoucherId]
            FROM dbo.[VOUCHER_USAGE] AS usage
            INNER JOIN exactClaim
                ON exactClaim.[voucherUsageId] = usage.[voucherUsageId];

            UPDATE claim
            SET [isUsed] = 0,
                [usedAt] = NULL
            FROM dbo.[CUSTOMER_VOUCHER] AS claim
            INNER JOIN dbo.[VOUCHER_USAGE] AS usage
                ON usage.[customerVoucherId] = claim.[customerVoucherId]
            WHERE usage.[usageStatus] = N'APPLIED'
              AND claim.[isUsed] = 1;

            IF EXISTS
            (
                SELECT usage.[customerVoucherId]
                FROM dbo.[VOUCHER_USAGE] AS usage
                WHERE usage.[customerVoucherId] IS NOT NULL
                  AND usage.[usageStatus] <> N'CANCELLED'
                GROUP BY usage.[customerVoucherId]
                HAVING COUNT_BIG(*) > 1
            )
                THROW 52101, 'A customer voucher is linked to multiple non-cancelled usages. Reconcile those rows, then re-run.', 1;

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.foreign_keys
                WHERE parent_object_id = OBJECT_ID(N'dbo.VOUCHER_USAGE')
                  AND name = N'FK_VOUCHER_USAGE_CUSTOMER_VOUCHER'
            )
                ALTER TABLE dbo.[VOUCHER_USAGE] WITH CHECK
                    ADD CONSTRAINT [FK_VOUCHER_USAGE_CUSTOMER_VOUCHER]
                    FOREIGN KEY ([customerVoucherId])
                    REFERENCES dbo.[CUSTOMER_VOUCHER]([customerVoucherId]);

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.VOUCHER_USAGE')
                  AND name = N'UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER'
            )
                CREATE UNIQUE INDEX [UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER]
                    ON dbo.[VOUCHER_USAGE]([customerVoucherId])
                    WHERE [customerVoucherId] IS NOT NULL
                      AND [usageStatus] <> N'CANCELLED';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.VOUCHER_USAGE')
                  AND name = N'UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER'
            )
                DROP INDEX [UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER]
                    ON dbo.[VOUCHER_USAGE];

            IF EXISTS
            (
                SELECT 1 FROM sys.foreign_keys
                WHERE parent_object_id = OBJECT_ID(N'dbo.VOUCHER_USAGE')
                  AND name = N'FK_VOUCHER_USAGE_CUSTOMER_VOUCHER'
            )
                ALTER TABLE dbo.[VOUCHER_USAGE]
                    DROP CONSTRAINT [FK_VOUCHER_USAGE_CUSTOMER_VOUCHER];

            IF COL_LENGTH(N'dbo.VOUCHER_USAGE', N'customerVoucherId') IS NOT NULL
                ALTER TABLE dbo.[VOUCHER_USAGE]
                    DROP COLUMN [customerVoucherId];
            """);
    }
}
