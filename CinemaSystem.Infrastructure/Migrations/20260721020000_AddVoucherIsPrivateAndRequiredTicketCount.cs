using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[Migration("20260721020000_AddVoucherIsPrivateAndRequiredTicketCount")]
public partial class AddVoucherIsPrivateAndRequiredTicketCount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.VOUCHER', 'isPrivate') IS NULL
            BEGIN
                ALTER TABLE dbo.[VOUCHER] ADD [isPrivate] BIT NOT NULL CONSTRAINT [DF_VOUCHER_isPrivate] DEFAULT 0;
            END;

            IF COL_LENGTH('dbo.VOUCHER', 'requiredTicketCount') IS NULL
            BEGIN
                ALTER TABLE dbo.[VOUCHER] ADD [requiredTicketCount] INT NULL;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.VOUCHER', 'isPrivate') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.[VOUCHER] DROP CONSTRAINT [DF_VOUCHER_isPrivate];
                ALTER TABLE dbo.[VOUCHER] DROP COLUMN [isPrivate];
            END;

            IF COL_LENGTH('dbo.VOUCHER', 'requiredTicketCount') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.[VOUCHER] DROP COLUMN [requiredTicketCount];
            END;
            """);
    }
}
