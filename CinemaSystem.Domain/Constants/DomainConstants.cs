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

    public static class TicketStatus
    {
        public const string Generated = "GENERATED";
        public const string Unused = "UNUSED";
        public const string CheckedIn = "CHECKED_IN";
        public const string Cancelled = "CANCELLED";
        public const string Refunded = "REFUNDED";
    }

    public static class PaymentStatus
    {
        public const string Pending = "PENDING";
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string Cancelled = "CANCELLED";
        public const string Expired = "EXPIRED";
    }

    public static class AgeRating
    {
        public const string P = "P";
        public const string K = "K";
        public const string T13 = "T13";
        public const string T16 = "T16";
        public const string T18 = "T18";
        public const string C = "C";

        public static readonly string[] ValidRatings = { P, K, T13, T16, T18, C };
    }
    
    public static class MovieHighlight
    {
        public const string Popular = "POPULAR";
        public const string Hot = "HOT";
        public const string Trending = "TRENDING";
        public const string ComingSoon = "COMING_SOON";
        public const string New = "NEW";
    }

    public static class Language
    {
        public const string VN = "VN"; // Tiếng Việt
        public const string EN_SUB_VN = "EN_SUB_VN"; // Tiếng Anh phụ đề tiếng Việt
        public const string EN_DUB_VN = "EN_DUB_VN"; // Tiếng Anh lồng tiếng Việt
        public const string KR_SUB_VN = "KR_SUB_VN"; // Tiếng Hàn phụ đề tiếng Việt
        public const string JP_SUB_VN = "JP_SUB_VN"; // Tiếng Nhật phụ đề tiếng Việt
        public const string TH_SUB_VN = "TH_SUB_VN"; // Tiếng Thái phụ đề tiếng Việt
        public const string CN_SUB_VN = "CN_SUB_VN"; // Tiếng Trung phụ đề tiếng Việt

        public static readonly string[] ValidLanguages = { VN, EN_SUB_VN, EN_DUB_VN, KR_SUB_VN, JP_SUB_VN, TH_SUB_VN, CN_SUB_VN };
    }

    public static class Action
    {
        public const string Create = "CREATE";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
    }

    public static class ApprovalStatus
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";
    }

    public static class DashboardPeriod
    {
        public const string Week = "week";
        public const string Month = "month";
    }

    public static class Staff
    {
        public const string Unknown = "STAFF_UNKNOWN";
    }
}
