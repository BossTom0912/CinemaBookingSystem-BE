using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260721010000_AddRefundCustomerConfirmation")]
public partial class AddRefundCustomerConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "REFUND_CUSTOMER_CONFIRMATION"
            (
                "refundCustomerConfirmationId" varchar(50) PRIMARY KEY,
                "manualRefundProcessId" varchar(50) NOT NULL,
                "tokenHash" char(64) NOT NULL,
                "status" varchar(30) NOT NULL,
                "expiresAt" timestamp with time zone NOT NULL,
                "confirmedAt" timestamp with time zone,
                "createdAt" timestamp with time zone NOT NULL,
                "revokedAt" timestamp with time zone,
                CONSTRAINT "UQ_REFUND_CUSTOMER_CONFIRMATION_PROCESS" UNIQUE ("manualRefundProcessId"),
                CONSTRAINT "UQ_REFUND_CUSTOMER_CONFIRMATION_TOKEN" UNIQUE ("tokenHash"),
                CONSTRAINT "CK_REFUND_CUSTOMER_CONFIRMATION_STATUS"
                    CHECK ("status" IN ('AWAITING_CUSTOMER', 'CONFIRMED_BY_CUSTOMER', 'EXPIRED', 'REVOKED')),
                CONSTRAINT "FK_REFUND_CUSTOMER_CONFIRMATION_PROCESS"
                    FOREIGN KEY ("manualRefundProcessId") REFERENCES "MANUAL_REFUND_PROCESS" ("manualRefundProcessId")
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_REFUND_CUSTOMER_CONFIRMATION_STATUS"
                ON "REFUND_CUSTOMER_CONFIRMATION" ("status", "expiresAt");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("DROP TABLE IF EXISTS \"REFUND_CUSTOMER_CONFIRMATION\";");
}
