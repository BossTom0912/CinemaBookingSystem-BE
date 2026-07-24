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
            IF OBJECT_ID(N'dbo.VOUCHER', N'U') IS NULL
                THROW 52102, 'Voucher table is missing. Apply the canonical database schema first.', 1;

            IF COL_LENGTH(N'dbo.VOUCHER', N'category') IS NULL
                ALTER TABLE dbo.[VOUCHER] ADD [category] NVARCHAR(50) NULL;

            IF COL_LENGTH(N'dbo.VOUCHER', N'applicableScope') IS NULL
                ALTER TABLE dbo.[VOUCHER] ADD [applicableScope] NVARCHAR(50) NULL;

            IF COL_LENGTH(N'dbo.VOUCHER', N'targetType') IS NULL
                ALTER TABLE dbo.[VOUCHER] ADD [targetType] NVARCHAR(50) NULL;

            IF COL_LENGTH(N'dbo.VOUCHER', N'targetCustomerIds') IS NULL
                ALTER TABLE dbo.[VOUCHER] ADD [targetCustomerIds] NVARCHAR(MAX) NULL;

            IF COL_LENGTH(N'dbo.VOUCHER', N'specificFbItemIds') IS NULL
                ALTER TABLE dbo.[VOUCHER] ADD [specificFbItemIds] NVARCHAR(MAX) NULL;

            IF COL_LENGTH(N'dbo.VOUCHER', N'isPrivate') IS NULL
                ALTER TABLE dbo.[VOUCHER]
                    ADD [isPrivate] BIT NOT NULL
                    CONSTRAINT [DF_VOUCHER_isPrivate] DEFAULT 0;

            IF COL_LENGTH(N'dbo.VOUCHER', N'requiredTicketCount') IS NULL
                ALTER TABLE dbo.[VOUCHER] ADD [requiredTicketCount] INT NULL;
            """);

        // SQL Server compiles a command batch before executing its ALTER TABLE
        // statements, so the backfill must be issued after the new columns.
        migrationBuilder.Sql(
            """
            UPDATE dbo.[VOUCHER]
            SET [category] = N'EVENT'
            WHERE [category] IS NULL;

            UPDATE dbo.[VOUCHER]
            SET [applicableScope] = N'TOTAL_ORDER'
            WHERE [applicableScope] IS NULL;

            UPDATE dbo.[VOUCHER]
            SET [targetType] = N'ALL_CUSTOMERS'
            WHERE [targetType] IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is a forward-only repair migration. Dropping populated promotion
        // columns during rollback would destroy existing voucher configuration.
    }
}
