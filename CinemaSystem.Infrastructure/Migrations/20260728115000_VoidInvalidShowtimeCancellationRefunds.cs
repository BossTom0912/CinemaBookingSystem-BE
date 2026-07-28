using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260728115000_VoidInvalidShowtimeCancellationRefunds")]
public partial class VoidInvalidShowtimeCancellationRefunds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The buggy showtime-cancellation path created both compensation vouchers and a
        // pending cash-refund claim. Revoke only untouched claims from that exact path.
        migrationBuilder.Sql(
            """
            UPDATE "REFUND_CLAIM_TOKEN" AS token
            SET "revokedAt" = COALESCE(token."revokedAt", timezone('UTC', now()))
            FROM "REFUND_CLAIM" AS claim
            JOIN "REFUND" AS refund
              ON refund."refundId" = claim."refundId"
            WHERE token."refundClaimId" = claim."refundClaimId"
              AND token."usedAt" IS NULL
              AND claim."claimStatus" = 'PENDING_INFO'
              AND refund."refundStatus" = 'PENDING'
              AND refund."showtimeCancellationId" IS NOT NULL
              AND EXISTS (
                  SELECT 1
                  FROM "CANCELLATION_COMPENSATION" AS compensation
                  WHERE compensation."sourceBookingId" = refund."bookingId"
                    AND compensation."showtimeCancellationId" = refund."showtimeCancellationId");

            UPDATE "REFUND_CLAIM" AS claim
            SET "claimStatus" = 'REVOKED',
                "updatedAt" = timezone('UTC', now())
            FROM "REFUND" AS refund
            WHERE refund."refundId" = claim."refundId"
              AND claim."claimStatus" = 'PENDING_INFO'
              AND refund."refundStatus" = 'PENDING'
              AND refund."showtimeCancellationId" IS NOT NULL
              AND EXISTS (
                  SELECT 1
                  FROM "CANCELLATION_COMPENSATION" AS compensation
                  WHERE compensation."sourceBookingId" = refund."bookingId"
                    AND compensation."showtimeCancellationId" = refund."showtimeCancellationId");

            UPDATE "REFUND" AS refund
            SET "refundStatus" = 'FAILED',
                "failureReason" = 'VOIDED_DUPLICATE_SHOWTIME_CANCELLATION_REFUND_COMPENSATION_ISSUED'
            WHERE refund."refundStatus" = 'PENDING'
              AND refund."showtimeCancellationId" IS NOT NULL
              AND EXISTS (
                  SELECT 1
                  FROM "CANCELLATION_COMPENSATION" AS compensation
                  WHERE compensation."sourceBookingId" = refund."bookingId"
                    AND compensation."showtimeCancellationId" = refund."showtimeCancellationId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately irreversible: restoring invalid pending refund claims could trigger
        // a duplicate cash payout after compensation vouchers have already been issued.
    }
}
