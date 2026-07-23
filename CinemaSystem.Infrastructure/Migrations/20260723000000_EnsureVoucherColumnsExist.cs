using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[Migration("20260723000000_EnsureVoucherColumnsExist")]
public partial class EnsureVoucherColumnsExist : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[VOUCHER]'))
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'category')
                    ALTER TABLE [VOUCHER] ADD [category] NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'applicableScope')
                    ALTER TABLE [VOUCHER] ADD [applicableScope] NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'targetType')
                    ALTER TABLE [VOUCHER] ADD [targetType] NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'targetCustomerIds')
                    ALTER TABLE [VOUCHER] ADD [targetCustomerIds] NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'specificFbItemIds')
                    ALTER TABLE [VOUCHER] ADD [specificFbItemIds] NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'isPrivate')
                    ALTER TABLE [VOUCHER] ADD [isPrivate] BIT NOT NULL CONSTRAINT [DF_VOUCHER_isPrivate_Auto] DEFAULT 0;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'requiredTicketCount')
                    ALTER TABLE [VOUCHER] ADD [requiredTicketCount] INT NULL;

                UPDATE [VOUCHER] SET [category] = 'EVENT' WHERE [category] IS NULL;
                UPDATE [VOUCHER] SET [applicableScope] = 'TOTAL_ORDER' WHERE [applicableScope] IS NULL;
                UPDATE [VOUCHER] SET [targetType] = 'ALL_CUSTOMERS' WHERE [targetType] IS NULL;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
