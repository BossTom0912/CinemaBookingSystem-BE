using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[Migration("20260721010000_AddRefundCustomerConfirmation")]
public partial class AddRefundCustomerConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.REFUND_CUSTOMER_CONFIRMATION', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[REFUND_CUSTOMER_CONFIRMATION]
                (
                    [refundCustomerConfirmationId] NVARCHAR(50) NOT NULL PRIMARY KEY,
                    [manualRefundProcessId] NVARCHAR(50) NOT NULL,
                    [tokenHash] CHAR(64) NOT NULL,
                    [status] NVARCHAR(30) NOT NULL,
                    [expiresAt] DATETIME2 NOT NULL,
                    [confirmedAt] DATETIME2 NULL,
                    [createdAt] DATETIME2 NOT NULL,
                    [revokedAt] DATETIME2 NULL,
                    CONSTRAINT [UQ_REFUND_CUSTOMER_CONFIRMATION_PROCESS] UNIQUE ([manualRefundProcessId]),
                    CONSTRAINT [UQ_REFUND_CUSTOMER_CONFIRMATION_TOKEN] UNIQUE ([tokenHash]),
                    CONSTRAINT [CK_REFUND_CUSTOMER_CONFIRMATION_STATUS]
                        CHECK ([status] IN ('AWAITING_CUSTOMER', 'CONFIRMED_BY_CUSTOMER', 'EXPIRED', 'REVOKED')),
                    CONSTRAINT [FK_REFUND_CUSTOMER_CONFIRMATION_PROCESS]
                        FOREIGN KEY ([manualRefundProcessId]) REFERENCES dbo.[MANUAL_REFUND_PROCESS]([manualRefundProcessId])
                        ON DELETE CASCADE
                );
                CREATE INDEX [IX_REFUND_CUSTOMER_CONFIRMATION_STATUS]
                    ON dbo.[REFUND_CUSTOMER_CONFIRMATION]([status], [expiresAt]);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF OBJECT_ID(N'dbo.REFUND_CUSTOMER_CONFIRMATION', N'U') IS NOT NULL DROP TABLE dbo.[REFUND_CUSTOMER_CONFIRMATION];");
    }
}
