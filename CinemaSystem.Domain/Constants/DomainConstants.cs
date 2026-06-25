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
        public const string PendingRefund = "PENDING_REFUND";
        public const string Refunded = "REFUNDED";
        public const string Ended = "ENDED";
        public const string Locked = "LOCKED";
        public const string Open = "OPEN";
        public const string Closed = "CLOSED";
        public const string Completed = "COMPLETED";
        public const string ProcessingUnstable = "PROCESSING_UNSTABLE";
        public const string Archived = "ARCHIVED";
        public const string Showing = "SHOWING";
        public const string ComingSoon = "COMING_SOON";
        public const string Booked = "BOOKED";
    }
}
