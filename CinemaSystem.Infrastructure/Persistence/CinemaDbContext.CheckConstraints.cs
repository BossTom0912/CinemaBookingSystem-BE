using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Persistence;

public partial class CinemaDbContext
{
    private static readonly IReadOnlyList<PostgresCheckConstraint> PostgresCheckConstraints =
    [
        new("CINEMA", "CK_CINEMA_STATUS", "\"cinemaStatus\" IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE')"),
        new("SEAT_TYPE", "CK_SEAT_TYPE_EXTRA_FEE", "\"extraFee\" >= 0"),
        new("PAYMENT_PROVIDER", "CK_PAYMENT_PROVIDER_STATUS", "\"providerStatus\" IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE')"),
        new("FB_ITEM", "CK_FB_ITEM_PRICE", "price >= 0"),
        new("FB_ITEM", "CK_FB_ITEM_STATUS", "\"itemStatus\" IN ('AVAILABLE', 'UNAVAILABLE', 'INACTIVE')"),
        new("MOVIE", "CK_MOVIE_DURATION", "\"durationMinutes\" > 0"),
        new("MOVIE", "CK_MOVIE_HIGHLIGHT", "highlight IS NULL OR highlight IN ('POPULAR', 'COMING_SOON', 'NEW', 'HOT', 'TRENDING')"),
        new("MOVIE", "CK_MOVIE_STATUS", "\"movieStatus\" IN ('COMING_SOON', 'NOW_SHOWING', 'ENDED', 'INACTIVE', 'ARCHIVED')"),
        new("VOUCHER", "CK_VOUCHER_DISCOUNT_TYPE", "\"discountType\" IN ('AMOUNT', 'PERCENT')"),
        new("VOUCHER", "CK_VOUCHER_DISCOUNT_VALUE", "\"discountValue\" > 0"),
        new("VOUCHER", "CK_VOUCHER_MIN_ORDER_AMOUNT", "\"minOrderAmount\" IS NULL OR \"minOrderAmount\" >= 0"),
        new("VOUCHER", "CK_VOUCHER_MAX_DISCOUNT_AMOUNT", "\"maxDiscountAmount\" IS NULL OR \"maxDiscountAmount\" > 0"),
        new("VOUCHER", "CK_VOUCHER_USAGE_LIMIT", "\"usageLimit\" >= 0"),
        new("VOUCHER", "CK_VOUCHER_PER_CUSTOMER_LIMIT", "\"perCustomerLimit\" IS NULL OR \"perCustomerLimit\" > 0"),
        new("VOUCHER", "CK_VOUCHER_USED_COUNT", "\"usedCount\" >= 0"),
        new("VOUCHER", "CK_VOUCHER_DATE_RANGE", "\"endDate\" > \"startDate\""),
        new("VOUCHER", "CK_VOUCHER_STATUS", "\"voucherStatus\" IN ('ACTIVE', 'INACTIVE', 'EXPIRED')"),
        new("USER", "CK_USER_STATUS", "status IN ('PENDING_VERIFICATION', 'ACTIVE', 'INACTIVE', 'BANNED')"),
        new("EMAIL_VERIFICATION_TOKEN", "CK_EMAIL_VERIFICATION_TOKEN_PURPOSE", "purpose IN ('EMAIL_VERIFICATION', 'PASSWORD_RESET', 'EMAIL_UPDATE', 'PHONE_UPDATE', 'REGISTER', 'FORGOT_PASSWORD', 'CHANGE_EMAIL', 'UPDATE_EMAIL')"),
        new("EMAIL_VERIFICATION_TOKEN", "CK_EMAIL_VERIFICATION_TOKEN_ATTEMPT_COUNT", "\"attemptCount\" >= 0"),
        new("EMAIL_VERIFICATION_TOKEN", "CK_EMAIL_VERIFICATION_EXPIRED_AT", "\"expiredAt\" > \"createdAt\""),
        new("REFRESH_TOKEN", "CK_REFRESH_TOKEN_EXPIRES_AT", "\"expiresAt\" > \"issuedAt\""),
        new("CUSTOMER_PROFILE", "CK_CUSTOMER_PROFILE_MEMBER_LEVEL", "\"memberLevel\" IN ('STANDARD', 'SILVER', 'GOLD', 'PLATINUM')"),
        new("CUSTOMER_PROFILE", "CK_CUSTOMER_PROFILE_REWARD_POINTS", "\"rewardPoints\" >= 0"),
        new("STAFF_PROFILE", "CK_STAFF_PROFILE_EMPLOYMENT_STATUS", "\"employmentStatus\" IN ('ACTIVE', 'INACTIVE', 'SUSPENDED')"),
        new("ROOM", "CK_ROOM_CAPACITY", "capacity > 0"),
        new("ROOM", "CK_ROOM_STATUS", "\"roomStatus\" IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE')"),
        new("SEAT", "CK_SEAT_NUMBER", "\"seatNumber\" > 0"),
        new("SHOWTIME", "CK_SHOWTIME_TIME_RANGE", "\"endTime\" > \"startTime\""),
        new("SHOWTIME", "CK_SHOWTIME_BASE_PRICE", "\"basePrice\" >= 0"),
        new("SHOWTIME", "CK_SHOWTIME_STATUS", "status IN ('OPEN', 'CLOSED', 'CANCELLED', 'COMPLETED', 'SUSPENDED', 'PROCESSING_UNSTABLE')"),
        new("SHOWTIME_SEAT", "CK_SHOWTIME_SEAT_STATUS", "\"seatStatus\" IN ('AVAILABLE', 'LOCKED', 'BOOKED', 'RELEASED', 'UNAVAILABLE')"),
        new("BOOKING", "CK_BOOKING_STATUS", "\"bookingStatus\" IN ('CREATED', 'PENDING_PAYMENT', 'PAID', 'CANCELLED', 'REFUND_PENDING', 'REFUNDED', 'COMPLETED', 'PROCESSING_UNSTABLE')"),
        new("BOOKING", "CK_BOOKING_CHANNEL", "\"bookingChannel\" IN ('ONLINE', 'COUNTER')"),
        new("BOOKING", "CK_BOOKING_FB_FULFILLMENT_STATUS", "\"fbFulfillmentStatus\" IN ('NOT_REQUIRED', 'PENDING', 'PREPARING', 'FULFILLED', 'CANCELLED')"),
        new("BOOKING", "CK_BOOKING_ONLINE_CUSTOMER_REQUIRED", "\"bookingChannel\" <> 'ONLINE' OR \"customerProfileId\" IS NOT NULL"),
        new("BOOKING", "CK_BOOKING_TOTAL_AMOUNT", "\"totalAmount\" >= 0"),
        new("BOOKING", "CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT", "\"compensationDiscountAmount\" >= 0"),
        new("BOOKING_SEAT", "CK_BOOKING_SEAT_PRICE", "\"seatPrice\" >= 0"),
        new("TICKET", "CK_TICKET_STATUS", "\"ticketStatus\" IN ('GENERATED', 'UNUSED', 'CHECKED_IN', 'CANCELLED', 'REFUNDED')"),
        new("CHECKIN_LOG", "CK_CHECKIN_LOG_RESULT", "result IN ('SUCCESS', 'FAILED')"),
        new("PAYMENT", "CK_PAYMENT_AMOUNT", "amount >= 0"),
        new("PAYMENT", "CK_PAYMENT_STATUS", "\"paymentStatus\" IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED', 'EXPIRED')"),
        new("ROLE_PROVISIONING_POLICY", "CK_ROLE_PROVISIONING_POLICY_PROFILE", "\"profileKind\" IN ('CUSTOMER', 'STAFF', 'NONE')"),
        new("ROLE_PROVISIONING_POLICY", "CK_ROLE_PROVISIONING_POLICY_PROFILE_RULE", "(\"profileKind\" = 'STAFF' AND \"requiresCinema\" = TRUE AND \"defaultStaffPosition\" IS NOT NULL) OR (\"profileKind\" = 'CUSTOMER' AND \"requiresCinema\" = FALSE AND \"defaultStaffPosition\" IS NULL) OR (\"profileKind\" = 'NONE' AND \"requiresCinema\" = FALSE AND \"defaultStaffPosition\" IS NULL)"),
        new("ROLE_PROVISIONING_POLICY", "CK_ROLE_PROVISIONING_POLICY_PUBLIC_REGISTER", "\"isPublicRegistrationAllowed\" = FALSE OR \"profileKind\" = 'CUSTOMER'"),
        new("ROLE_ASSIGNMENT_RULE", "CK_ROLE_ASSIGNMENT_RULE_DIFFERENT_ROLES", "\"grantorRoleId\" <> \"granteeRoleId\""),
        new("CANCELLATION_COMPENSATION", "CK_CANCELLATION_COMPENSATION_STATUS", "status IN ('ISSUED', 'PARTIALLY_USED', 'USED', 'EXPIRED', 'VOIDED')"),
        new("CANCELLATION_COMPENSATION", "CK_CANCELLATION_COMPENSATION_EXPIRY", "\"expiresAt\" > \"issuedAt\""),
        new("COMPENSATION_TICKET", "CK_COMPENSATION_TICKET_STATUS", "status IN ('ISSUED', 'RESERVED', 'REDEEMED', 'EXPIRED', 'VOIDED')"),
        new("COMPENSATION_COMBO", "CK_COMPENSATION_COMBO_STATUS", "status IN ('ISSUED', 'REDEEMED', 'EXPIRED', 'VOIDED')"),
        new("REFUND", "CK_REFUND_AMOUNT", "\"refundAmount\" > 0"),
        new("REFUND", "CK_REFUND_STATUS", "\"refundStatus\" IN ('PENDING', 'PROCESSING', 'SUCCESS', 'FAILED', 'REQUESTED', 'MANUAL_REQUIRED')"),
        new("REFUND_CLAIM", "CK_REFUND_CLAIM_STATUS", "\"claimStatus\" IN ('PENDING_INFO', 'VERIFIED', 'SUBMITTED', 'PROCESSING', 'COMPLETED', 'EXPIRED', 'MANUAL_REQUIRED', 'REVOKED')"),
        new("REFUND_CLAIM", "CK_REFUND_CLAIM_ACCOUNT_VALIDATION_STATUS", "\"accountValidationStatus\" IN ('NOT_STARTED', 'VERIFIED', 'FAILED', 'UNAVAILABLE')"),
        new("CUSTOMER_REFUND_REQUEST", "CK_CUSTOMER_REFUND_REQUEST_STATUS", "\"requestStatus\" IN ('PENDING', 'FULFILLED', 'REJECTED')"),
        new("MANUAL_REFUND_PROCESS", "CK_MANUAL_REFUND_PROCESS_STATUS", "\"processStatus\" IN ('OPEN', 'IN_PROGRESS', 'CONFIRMED', 'REJECTED')"),
        new("MANUAL_REFUND_PROCESS", "CK_MANUAL_REFUND_TRANSFERRED_AMOUNT", "\"transferredAmount\" IS NULL OR \"transferredAmount\" > 0"),
        new("REFUND_CUSTOMER_CONFIRMATION", "CK_REFUND_CUSTOMER_CONFIRMATION_STATUS", "status IN ('AWAITING_CUSTOMER', 'CONFIRMED_BY_CUSTOMER', 'EXPIRED', 'REVOKED')"),
        new("CUSTOMER_VOUCHER", "CK_CUSTOMER_VOUCHER_USAGE_STATE", "(\"isUsed\" = FALSE AND \"usedAt\" IS NULL) OR (\"isUsed\" = TRUE AND \"usedAt\" IS NOT NULL)"),
        new("VOUCHER_USAGE", "CK_VOUCHER_USAGE_DISCOUNT_AMOUNT", "\"discountAmount\" >= 0"),
        new("VOUCHER_USAGE", "CK_VOUCHER_USAGE_STATUS", "\"usageStatus\" IN ('APPLIED', 'CONFIRMED', 'CANCELLED')"),
        new("BOOKING_FB_ITEM", "CK_BOOKING_FB_ITEM_QUANTITY", "quantity > 0"),
        new("BOOKING_FB_ITEM", "CK_BOOKING_FB_ITEM_UNIT_PRICE", "\"unitPrice\" >= 0"),
        new("BOOKING_FB_ITEM", "CK_BOOKING_FB_ITEM_SUBTOTAL", "subtotal >= 0"),
        new("CINEMA_FB_INVENTORY", "CK_CINEMA_FB_INVENTORY_QUANTITY", "quantity >= 0"),
        new("REWARD_POINT_TRANSACTION", "CK_REWARD_POINT_TRANSACTION_TYPE", "\"transactionType\" IN ('EARN', 'REDEEM', 'REVERT', 'ADJUST')"),
        new("REWARD_POINT_TRANSACTION", "CK_REWARD_POINT_TRANSACTION_POINTS", "points <> 0"),
        new("REVIEW", "CK_REVIEW_RATING", "rating BETWEEN 0 AND 5"),
        new("REVIEW", "CK_REVIEW_STATUS", "status IN ('PENDING', 'APPROVED', 'REJECTED', 'FLAGGED')")
    ];

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        foreach (var tableConstraints in PostgresCheckConstraints.GroupBy(item => item.TableName))
        {
            var entityType = modelBuilder.Model.GetEntityTypes()
                .SingleOrDefault(item => item.GetTableName() == tableConstraints.Key)
                ?? throw new InvalidOperationException(
                    $"No EF entity is mapped to table '{tableConstraints.Key}' for its check constraints.");

            modelBuilder.Entity(entityType.Name).ToTable(
                tableConstraints.Key,
                tableBuilder =>
                {
                    foreach (var constraint in tableConstraints)
                    {
                        tableBuilder.HasCheckConstraint(constraint.Name, constraint.Sql);
                    }
                });
        }
    }

    private sealed record PostgresCheckConstraint(string TableName, string Name, string Sql);
}
