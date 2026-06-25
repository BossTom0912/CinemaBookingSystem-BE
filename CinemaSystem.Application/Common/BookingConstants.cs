namespace CinemaSystem.Application.Common;

public static class BookingConstants
{
    public static class BookingStatus
    {
        public const string Created = "CREATED";
        public const string PendingPayment = "PENDING_PAYMENT";
        public const string Paid = "PAID";
        public const string Cancelled = "CANCELLED";
        public const string RefundPending = "REFUND_PENDING";
        public const string Refunded = "REFUNDED";
        public const string Completed = "COMPLETED";
    }

    public static class BookingChannel
    {
        public const string Online = "ONLINE";
        public const string Counter = "COUNTER";
    }

    public static class ShowtimeStatus
    {
        public const string Open = "OPEN";
        public const string Closed = "CLOSED";
        public const string Cancelled = "CANCELLED";
        public const string Completed = "COMPLETED";
    }

    public static class ShowtimeSeatStatus
    {
        public const string Locked = "LOCKED";
        public const string Available = "AVAILABLE";
        public const string Booked = "BOOKED";
        public const string Released = "RELEASED";
        public const string Unavailable = "UNAVAILABLE";
    }

    public static class PaymentStatus
    {
        public const string Pending = "PENDING";
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string Cancelled = "CANCELLED";
        public const string Expired = "EXPIRED";
    }

    public static class RefundStatus
    {
        public const string Pending = "PENDING";
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string ManualRequired = "MANUAL_REQUIRED";
    }

    public static class TicketStatus
    {
        public const string Generated = "GENERATED";
        public const string Unused = "UNUSED";
        public const string CheckedIn = "CHECKED_IN";
        public const string Cancelled = "CANCELLED";
        public const string Refunded = "REFUNDED";
    }

    public static class ResourceStatus
    {
        public const string Active = "ACTIVE";
        public const string Available = "AVAILABLE";
        public const string Inactive = "INACTIVE";
        public const string Ended = "ENDED";
    }

    public static class VoucherStatus
    {
        public const string Active = "ACTIVE";
    }

    public static class VoucherUsageStatus
    {
        public const string Applied = "APPLIED";
        public const string Confirmed = "CONFIRMED";
    }

    public static class DiscountType
    {
        public const string Amount = "AMOUNT";
        public const string Percent = "PERCENT";
    }

    public static class ErrorCodes
    {
        public const string ValidationError = "VALIDATION_ERROR";
        public const string InvalidSeatSelection = "INVALID_SEAT_SELECTION";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string BookingNotAllowed = "BOOKING_NOT_ALLOWED";
        public const string CustomerProfileNotFound = "CUSTOMER_PROFILE_NOT_FOUND";
        public const string ShowtimeNotFound = "SHOWTIME_NOT_FOUND";
        public const string ShowtimeSeatNotFound = "SHOWTIME_SEAT_NOT_FOUND";
        public const string FoodItemNotFound = "FB_ITEM_NOT_FOUND";
        public const string VoucherNotFound = "VOUCHER_NOT_FOUND";
        public const string ShowtimeNotOpen = "SHOWTIME_NOT_OPEN";
        public const string OnlineSaleClosed = "ONLINE_SALE_CLOSED";
        public const string SeatNotLockedByUser = "SEAT_NOT_LOCKED_BY_USER";
        public const string SeatLockExpired = "SEAT_LOCK_EXPIRED";
        public const string SeatUnavailable = "SEAT_UNAVAILABLE";
        public const string FoodItemUnavailable = "FB_ITEM_UNAVAILABLE";
        public const string InsufficientFoodStock = "INSUFFICIENT_FB_STOCK";
        public const string VoucherExpired = "VOUCHER_EXPIRED";
        public const string VoucherMinOrderNotMet = "VOUCHER_MIN_ORDER_NOT_MET";
        public const string VoucherUsageLimitReached = "VOUCHER_USAGE_LIMIT_REACHED";
        public const string VoucherCustomerLimitReached = "VOUCHER_CUSTOMER_LIMIT_REACHED";
        public const string CheckoutConcurrencyConflict = "CHECKOUT_CONCURRENCY_CONFLICT";
    }
}
