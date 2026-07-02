namespace CinemaSystem.Application.Common;

using CinemaSystem.Domain.Constants;

public static class BookingConstants
{
    public static class BookingStatus
    {
        public const string PendingPayment = DomainConstants.BookingStatus.PendingPayment;
    }

    public static class BookingChannel
    {
        public const string Online = DomainConstants.BookingChannel.Online;
    }

    public static class ShowtimeStatus
    {
        public const string Open = DomainConstants.ShowtimeStatus.Open;
    }

    public static class ShowtimeSeatStatus
    {
        public const string Locked = DomainConstants.ShowtimeSeatStatus.Locked;
        public const string Available = DomainConstants.ShowtimeSeatStatus.Available;
    }

    public static class ResourceStatus
    {
        public const string Active = DomainConstants.ResourceStatus.Active;
        public const string Available = DomainConstants.ResourceStatus.Available;
        public const string Inactive = DomainConstants.ResourceStatus.Inactive;
        public const string Ended = DomainConstants.MovieStatus.Ended;
    }

    public static class VoucherStatus
    {
        public const string Active = DomainConstants.VoucherStatus.Active;
    }

    public static class VoucherUsageStatus
    {
        public const string Applied = DomainConstants.VoucherUsageStatus.Applied;
        public const string Confirmed = DomainConstants.VoucherUsageStatus.Confirmed;
        public const string Cancelled = DomainConstants.VoucherUsageStatus.Cancelled;
    }

    public static class DiscountType
    {
        public const string Amount = DomainConstants.DiscountType.Amount;
        public const string Percent = DomainConstants.DiscountType.Percent;
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
