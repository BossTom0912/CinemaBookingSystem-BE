namespace CinemaSystem.Application.Common;

using CinemaSystem.Domain.Constants;

public static class BookingConstants
{
    public static class BookingStatus
    {
        public const string Created = DomainConstants.BookingStatus.Created;
        public const string PendingPayment = DomainConstants.BookingStatus.PendingPayment;
        public const string Paid = DomainConstants.BookingStatus.Paid;
        public const string Cancelled = DomainConstants.BookingStatus.Cancelled;
        public const string RefundPending = DomainConstants.BookingStatus.RefundPending;
        public const string Refunded = DomainConstants.BookingStatus.Refunded;
        public const string Completed = DomainConstants.BookingStatus.Completed;
    }

    public static class BookingChannel
    {
        public const string Online = DomainConstants.BookingChannel.Online;
        public const string Counter = DomainConstants.BookingChannel.Counter;
    }

    public static class ShowtimeStatus
    {
        public const string Open = DomainConstants.ShowtimeStatus.Open;
        public const string Closed = DomainConstants.ShowtimeStatus.Closed;
        public const string Cancelled = DomainConstants.ShowtimeStatus.Cancelled;
        public const string Completed = DomainConstants.ShowtimeStatus.Completed;
    }

    public static class ShowtimeSeatStatus
    {
        public const string Locked = DomainConstants.ShowtimeSeatStatus.Locked;
        public const string Available = DomainConstants.ShowtimeSeatStatus.Available;
        public const string Booked = DomainConstants.ShowtimeSeatStatus.Booked;
        public const string Released = DomainConstants.ShowtimeSeatStatus.Released;
        public const string Unavailable = DomainConstants.ShowtimeSeatStatus.Unavailable;
    }

    public static class PaymentStatus
    {
        public const string Pending = DomainConstants.PaymentStatus.Pending;
        public const string Success = DomainConstants.PaymentStatus.Success;
        public const string Failed = DomainConstants.PaymentStatus.Failed;
        public const string Cancelled = DomainConstants.PaymentStatus.Cancelled;
        public const string Expired = DomainConstants.PaymentStatus.Expired;
    }

    public static class RefundStatus
    {
        public const string Pending = DomainConstants.RefundStatus.Pending;
        public const string Success = DomainConstants.RefundStatus.Success;
        public const string Failed = DomainConstants.RefundStatus.Failed;
        public const string ManualRequired = DomainConstants.RefundStatus.ManualRequired;
    }

    public static class RefundWorkflowStatus
    {
        public const string Pending = RefundStatus.Pending;
        public const string Success = RefundStatus.Success;
        public const string ManualRequired = RefundStatus.ManualRequired;
        public const string AwaitingCustomerInfo = DomainConstants.RefundWorkflowStatus.AwaitingCustomerInfo;
    }

    public static class RefundPolicy
    {
        public const int ClaimTokenEntropyBytes = DomainConstants.RefundPolicy.ClaimTokenEntropyBytes;
    }

    public static class ManagerDashboard
    {
        public const string AllCinemasLabel = DomainConstants.ManagerDashboard.AllCinemasLabel;
        public const decimal PercentageMultiplier = DomainConstants.ManagerDashboard.PercentageMultiplier;
        public const int OccupancyRateDecimalPlaces = DomainConstants.ManagerDashboard.OccupancyRateDecimalPlaces;
    }

    public static class EntityIdPrefix
    {
        public const string RefundClaim = DomainConstants.EntityIdPrefix.RefundClaim;
        public const string RefundClaimToken = DomainConstants.EntityIdPrefix.RefundClaimToken;
        public const string ShowtimeCancellation = DomainConstants.EntityIdPrefix.ShowtimeCancellation;
        public const string Refund = DomainConstants.EntityIdPrefix.Refund;
        public const string Notification = DomainConstants.EntityIdPrefix.Notification;
        public const string AuditLog = DomainConstants.EntityIdPrefix.AuditLog;
        public const string RewardPointTransaction = DomainConstants.EntityIdPrefix.RewardPointTransaction;
        public const string ManualRefundProcess = DomainConstants.EntityIdPrefix.ManualRefundProcess;
        public const string CustomerRefundRequest = DomainConstants.EntityIdPrefix.CustomerRefundRequest;
    }

    public static class TicketStatus
    {
        public const string Generated = DomainConstants.TicketStatus.Generated;
        public const string Unused = DomainConstants.TicketStatus.Unused;
        public const string CheckedIn = DomainConstants.TicketStatus.CheckedIn;
        public const string Cancelled = DomainConstants.TicketStatus.Cancelled;
        public const string Refunded = DomainConstants.TicketStatus.Refunded;
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

    public static class RefundClaimStatus
    {
        public const string PendingInfo = DomainConstants.RefundClaimStatus.PendingInfo;
        public const string Submitted = DomainConstants.RefundClaimStatus.Submitted;
        public const string Completed = DomainConstants.RefundClaimStatus.Completed;
        public const string Expired = DomainConstants.RefundClaimStatus.Expired;
        public const string ManualRequired = DomainConstants.RefundClaimStatus.ManualRequired;
        public const string Revoked = DomainConstants.RefundClaimStatus.Revoked;
    }

    public static class AccountValidationStatus
    {
        public const string NotStarted = DomainConstants.AccountValidationStatus.NotStarted;
        public const string Unavailable = DomainConstants.AccountValidationStatus.Unavailable;
    }

    public static class ManualRefundProcessStatus
    {
        public const string Open = DomainConstants.ManualRefundProcessStatus.Open;
        public const string InProgress = DomainConstants.ManualRefundProcessStatus.InProgress;
        public const string Confirmed = DomainConstants.ManualRefundProcessStatus.Confirmed;
        public const string Rejected = DomainConstants.ManualRefundProcessStatus.Rejected;
    }

    public static class CustomerRefundRequestStatus
    {
        public const string Pending = DomainConstants.CustomerRefundRequestStatus.Pending;
        public const string Fulfilled = DomainConstants.CustomerRefundRequestStatus.Fulfilled;
        public const string Rejected = DomainConstants.CustomerRefundRequestStatus.Rejected;
    }

    public static class RewardPointTransactionType
    {
        public const string Earn = DomainConstants.RewardPointTransactionType.Earn;
        public const string Redeem = DomainConstants.RewardPointTransactionType.Redeem;
        public const string Revert = DomainConstants.RewardPointTransactionType.Revert;
        public const string Adjust = DomainConstants.RewardPointTransactionType.Adjust;
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
