namespace CinemaSystem.Domain.Constants;

public static class DomainConstants
{
    public static class EntityStatus
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Cancelled = "CANCELLED";
        public const string Maintenance = "MAINTENANCE";
        public const string Available = "AVAILABLE";
        public const string Paid = "PAID";
        public const string PendingPayment = "PENDING_PAYMENT";
        public const string Failed = "FAILED";
        public const string PendingRefund = "REFUND_PENDING";
        public const string Refunded = "REFUNDED";
        public const string Ended = "ENDED";
        public const string Locked = "LOCKED";
        public const string Open = "OPEN";
        public const string Closed = "CLOSED";
        public const string Completed = "COMPLETED";
        public const string ProcessingUnstable = "PROCESSING_UNSTABLE";
        public const string Archived = "ARCHIVED"; // Consider using INACTIVE for MOVIE
        public const string Suspended = "SUSPENDED";
        public const string NowShowing = "NOW_SHOWING";
        public const string ComingSoon = "COMING_SOON";
        public const string Booked = "BOOKED";
    }

    public static class RefundStatus
    {
        public const string Pending = "PENDING";
        public const string Processing = "PROCESSING";
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string Requested = "REQUESTED";
    }
}
