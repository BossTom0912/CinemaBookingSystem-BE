using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurePostgresCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_USAGE_DISCOUNT_AMOUNT",
                table: "VOUCHER_USAGE",
                sql: "\"discountAmount\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_USAGE_STATUS",
                table: "VOUCHER_USAGE",
                sql: "\"usageStatus\" IN ('APPLIED', 'CONFIRMED', 'CANCELLED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_DATE_RANGE",
                table: "VOUCHER",
                sql: "\"endDate\" > \"startDate\"");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_DISCOUNT_TYPE",
                table: "VOUCHER",
                sql: "\"discountType\" IN ('AMOUNT', 'PERCENT')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_DISCOUNT_VALUE",
                table: "VOUCHER",
                sql: "\"discountValue\" > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_MAX_DISCOUNT_AMOUNT",
                table: "VOUCHER",
                sql: "\"maxDiscountAmount\" IS NULL OR \"maxDiscountAmount\" > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_MIN_ORDER_AMOUNT",
                table: "VOUCHER",
                sql: "\"minOrderAmount\" IS NULL OR \"minOrderAmount\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_PER_CUSTOMER_LIMIT",
                table: "VOUCHER",
                sql: "\"perCustomerLimit\" IS NULL OR \"perCustomerLimit\" > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_STATUS",
                table: "VOUCHER",
                sql: "\"voucherStatus\" IN ('ACTIVE', 'INACTIVE', 'EXPIRED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_USAGE_LIMIT",
                table: "VOUCHER",
                sql: "\"usageLimit\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_VOUCHER_USED_COUNT",
                table: "VOUCHER",
                sql: "\"usedCount\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_USER_STATUS",
                table: "USER",
                sql: "status IN ('PENDING_VERIFICATION', 'ACTIVE', 'INACTIVE', 'BANNED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_TICKET_STATUS",
                table: "TICKET",
                sql: "\"ticketStatus\" IN ('GENERATED', 'UNUSED', 'CHECKED_IN', 'CANCELLED', 'REFUNDED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_STAFF_PROFILE_EMPLOYMENT_STATUS",
                table: "STAFF_PROFILE",
                sql: "\"employmentStatus\" IN ('ACTIVE', 'INACTIVE', 'SUSPENDED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_SHOWTIME_SEAT_STATUS",
                table: "SHOWTIME_SEAT",
                sql: "\"seatStatus\" IN ('AVAILABLE', 'LOCKED', 'BOOKED', 'RELEASED', 'UNAVAILABLE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_SHOWTIME_BASE_PRICE",
                table: "SHOWTIME",
                sql: "\"basePrice\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_SHOWTIME_STATUS",
                table: "SHOWTIME",
                sql: "status IN ('OPEN', 'CLOSED', 'CANCELLED', 'COMPLETED', 'SUSPENDED', 'PROCESSING_UNSTABLE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_SHOWTIME_TIME_RANGE",
                table: "SHOWTIME",
                sql: "\"endTime\" > \"startTime\"");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_SEAT_TYPE_EXTRA_FEE",
                table: "SEAT_TYPE",
                sql: "\"extraFee\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_SEAT_NUMBER",
                table: "SEAT",
                sql: "\"seatNumber\" > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_ROOM_CAPACITY",
                table: "ROOM",
                sql: "capacity > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_ROOM_STATUS",
                table: "ROOM",
                sql: "\"roomStatus\" IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_ROLE_PROVISIONING_POLICY_PROFILE",
                table: "ROLE_PROVISIONING_POLICY",
                sql: "\"profileKind\" IN ('CUSTOMER', 'STAFF', 'NONE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_ROLE_PROVISIONING_POLICY_PROFILE_RULE",
                table: "ROLE_PROVISIONING_POLICY",
                sql: "(\"profileKind\" = 'STAFF' AND \"requiresCinema\" = TRUE AND \"defaultStaffPosition\" IS NOT NULL) OR (\"profileKind\" = 'CUSTOMER' AND \"requiresCinema\" = FALSE AND \"defaultStaffPosition\" IS NULL) OR (\"profileKind\" = 'NONE' AND \"requiresCinema\" = FALSE AND \"defaultStaffPosition\" IS NULL)");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_ROLE_PROVISIONING_POLICY_PUBLIC_REGISTER",
                table: "ROLE_PROVISIONING_POLICY",
                sql: "\"isPublicRegistrationAllowed\" = FALSE OR \"profileKind\" = 'CUSTOMER'");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_ROLE_ASSIGNMENT_RULE_DIFFERENT_ROLES",
                table: "ROLE_ASSIGNMENT_RULE",
                sql: "\"grantorRoleId\" <> \"granteeRoleId\"");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REWARD_POINT_TRANSACTION_POINTS",
                table: "REWARD_POINT_TRANSACTION",
                sql: "points <> 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REWARD_POINT_TRANSACTION_TYPE",
                table: "REWARD_POINT_TRANSACTION",
                sql: "\"transactionType\" IN ('EARN', 'REDEEM', 'REVERT', 'ADJUST')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REVIEW_RATING",
                table: "REVIEW",
                sql: "rating BETWEEN 0 AND 5");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REVIEW_STATUS",
                table: "REVIEW",
                sql: "status IN ('PENDING', 'APPROVED', 'REJECTED', 'FLAGGED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REFUND_CUSTOMER_CONFIRMATION_STATUS",
                table: "REFUND_CUSTOMER_CONFIRMATION",
                sql: "status IN ('AWAITING_CUSTOMER', 'CONFIRMED_BY_CUSTOMER', 'EXPIRED', 'REVOKED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REFUND_CLAIM_ACCOUNT_VALIDATION_STATUS",
                table: "REFUND_CLAIM",
                sql: "\"accountValidationStatus\" IN ('NOT_STARTED', 'VERIFIED', 'FAILED', 'UNAVAILABLE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REFUND_CLAIM_STATUS",
                table: "REFUND_CLAIM",
                sql: "\"claimStatus\" IN ('PENDING_INFO', 'VERIFIED', 'SUBMITTED', 'PROCESSING', 'COMPLETED', 'EXPIRED', 'MANUAL_REQUIRED', 'REVOKED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REFUND_AMOUNT",
                table: "REFUND",
                sql: "\"refundAmount\" > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REFUND_STATUS",
                table: "REFUND",
                sql: "\"refundStatus\" IN ('PENDING', 'PROCESSING', 'SUCCESS', 'FAILED', 'REQUESTED', 'MANUAL_REQUIRED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_REFRESH_TOKEN_EXPIRES_AT",
                table: "REFRESH_TOKEN",
                sql: "\"expiresAt\" > \"issuedAt\"");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_PAYMENT_PROVIDER_STATUS",
                table: "PAYMENT_PROVIDER",
                sql: "\"providerStatus\" IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_PAYMENT_AMOUNT",
                table: "PAYMENT",
                sql: "amount >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_PAYMENT_STATUS",
                table: "PAYMENT",
                sql: "\"paymentStatus\" IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED', 'EXPIRED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_MOVIE_DURATION",
                table: "MOVIE",
                sql: "\"durationMinutes\" > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_MOVIE_HIGHLIGHT",
                table: "MOVIE",
                sql: "highlight IS NULL OR highlight IN ('POPULAR', 'COMING_SOON', 'NEW', 'HOT', 'TRENDING')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_MOVIE_STATUS",
                table: "MOVIE",
                sql: "\"movieStatus\" IN ('COMING_SOON', 'NOW_SHOWING', 'ENDED', 'INACTIVE', 'ARCHIVED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_MANUAL_REFUND_PROCESS_STATUS",
                table: "MANUAL_REFUND_PROCESS",
                sql: "\"processStatus\" IN ('OPEN', 'IN_PROGRESS', 'CONFIRMED', 'REJECTED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_MANUAL_REFUND_TRANSFERRED_AMOUNT",
                table: "MANUAL_REFUND_PROCESS",
                sql: "\"transferredAmount\" IS NULL OR \"transferredAmount\" > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_FB_ITEM_PRICE",
                table: "FB_ITEM",
                sql: "price >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_FB_ITEM_STATUS",
                table: "FB_ITEM",
                sql: "\"itemStatus\" IN ('AVAILABLE', 'UNAVAILABLE', 'INACTIVE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_EMAIL_VERIFICATION_EXPIRED_AT",
                table: "EMAIL_VERIFICATION_TOKEN",
                sql: "\"expiredAt\" > \"createdAt\"");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_EMAIL_VERIFICATION_TOKEN_ATTEMPT_COUNT",
                table: "EMAIL_VERIFICATION_TOKEN",
                sql: "\"attemptCount\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_EMAIL_VERIFICATION_TOKEN_PURPOSE",
                table: "EMAIL_VERIFICATION_TOKEN",
                sql: "purpose IN ('EMAIL_VERIFICATION', 'PASSWORD_RESET', 'EMAIL_UPDATE', 'PHONE_UPDATE', 'REGISTER', 'FORGOT_PASSWORD', 'CHANGE_EMAIL', 'UPDATE_EMAIL')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CUSTOMER_VOUCHER_USAGE_STATE",
                table: "CUSTOMER_VOUCHER",
                sql: "(\"isUsed\" = FALSE AND \"usedAt\" IS NULL) OR (\"isUsed\" = TRUE AND \"usedAt\" IS NOT NULL)");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CUSTOMER_REFUND_REQUEST_STATUS",
                table: "CUSTOMER_REFUND_REQUEST",
                sql: "\"requestStatus\" IN ('PENDING', 'FULFILLED', 'REJECTED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CUSTOMER_PROFILE_MEMBER_LEVEL",
                table: "CUSTOMER_PROFILE",
                sql: "\"memberLevel\" IN ('STANDARD', 'SILVER', 'GOLD', 'PLATINUM')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CUSTOMER_PROFILE_REWARD_POINTS",
                table: "CUSTOMER_PROFILE",
                sql: "\"rewardPoints\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_COMPENSATION_TICKET_STATUS",
                table: "COMPENSATION_TICKET",
                sql: "status IN ('ISSUED', 'RESERVED', 'REDEEMED', 'EXPIRED', 'VOIDED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_COMPENSATION_COMBO_STATUS",
                table: "COMPENSATION_COMBO",
                sql: "status IN ('ISSUED', 'REDEEMED', 'EXPIRED', 'VOIDED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CINEMA_FB_INVENTORY_QUANTITY",
                table: "CINEMA_FB_INVENTORY",
                sql: "quantity >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CINEMA_STATUS",
                table: "CINEMA",
                sql: "\"cinemaStatus\" IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CHECKIN_LOG_RESULT",
                table: "CHECKIN_LOG",
                sql: "result IN ('SUCCESS', 'FAILED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CANCELLATION_COMPENSATION_EXPIRY",
                table: "CANCELLATION_COMPENSATION",
                sql: "\"expiresAt\" > \"issuedAt\"");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_CANCELLATION_COMPENSATION_STATUS",
                table: "CANCELLATION_COMPENSATION",
                sql: "status IN ('ISSUED', 'PARTIALLY_USED', 'USED', 'EXPIRED', 'VOIDED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_SEAT_PRICE",
                table: "BOOKING_SEAT",
                sql: "\"seatPrice\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_FB_ITEM_QUANTITY",
                table: "BOOKING_FB_ITEM",
                sql: "quantity > 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_FB_ITEM_SUBTOTAL",
                table: "BOOKING_FB_ITEM",
                sql: "subtotal >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_FB_ITEM_UNIT_PRICE",
                table: "BOOKING_FB_ITEM",
                sql: "\"unitPrice\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_CHANNEL",
                table: "BOOKING",
                sql: "\"bookingChannel\" IN ('ONLINE', 'COUNTER')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT",
                table: "BOOKING",
                sql: "\"compensationDiscountAmount\" >= 0");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_FB_FULFILLMENT_STATUS",
                table: "BOOKING",
                sql: "\"fbFulfillmentStatus\" IN ('NOT_REQUIRED', 'PENDING', 'PREPARING', 'FULFILLED', 'CANCELLED')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_ONLINE_CUSTOMER_REQUIRED",
                table: "BOOKING",
                sql: "\"bookingChannel\" <> 'ONLINE' OR \"customerProfileId\" IS NOT NULL");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_STATUS",
                table: "BOOKING",
                sql: "\"bookingStatus\" IN ('CREATED', 'PENDING_PAYMENT', 'PAID', 'CANCELLED', 'REFUND_PENDING', 'REFUNDED', 'COMPLETED', 'PROCESSING_UNSTABLE')");

            ReplaceCheckConstraint(migrationBuilder,
                name: "CK_BOOKING_TOTAL_AMOUNT",
                table: "BOOKING",
                sql: "\"totalAmount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only: these constraints may already exist in an adopted production schema.
        }

        private static void ReplaceCheckConstraint(
            MigrationBuilder migrationBuilder,
            string name,
            string table,
            string sql)
        {
            var quotedName = name.Replace("\"", "\"\"");
            var quotedTable = table.Replace("\"", "\"\"");

            migrationBuilder.Sql(
                $"""
                ALTER TABLE "{quotedTable}"
                    DROP CONSTRAINT IF EXISTS "{quotedName}";

                ALTER TABLE "{quotedTable}"
                    ADD CONSTRAINT "{quotedName}" CHECK ({sql}) NOT VALID;

                ALTER TABLE "{quotedTable}"
                    VALIDATE CONSTRAINT "{quotedName}";
                """);
        }
    }
}
