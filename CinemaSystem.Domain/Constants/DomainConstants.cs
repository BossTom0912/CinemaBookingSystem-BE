namespace CinemaSystem.Domain.Constants;

public static class DomainConstants
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
        public const string ProcessingUnstable = "PROCESSING_UNSTABLE";
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
        public const string Suspended = "SUSPENDED";
        public const string ProcessingUnstable = "PROCESSING_UNSTABLE";
    }

    public static class ShowtimeCancellationReason
    {
        public const string AdministrativeUpdate =
            "Showtime cancelled due to an administrative update or deletion.";
    }

    public static class ShowtimeSeatStatus
    {
        public const string Available = "AVAILABLE";
        public const string Locked = "LOCKED";
        public const string Booked = "BOOKED";
        public const string Released = "RELEASED";
        public const string Unavailable = "UNAVAILABLE";
    }

    public static class CinemaStatus
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Maintenance = "MAINTENANCE";
    }

    public static class RoomStatus
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Maintenance = "MAINTENANCE";
    }

    public static class MovieStatus
    {
        public const string ComingSoon = "COMING_SOON";
        public const string NowShowing = "NOW_SHOWING";
        public const string Ended = "ENDED";
        public const string Inactive = "INACTIVE";
        public const string Archived = "ARCHIVED";
    }

    public static class StaffEmploymentStatus
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Suspended = "SUSPENDED";
    }

    public static class StaffPosition
    {
        public const string Staff = "Staff";
    }

    public static class MemberLevel
    {
        public const string Standard = "STANDARD";
        public const string Silver = "SILVER";
        public const string Gold = "GOLD";
        public const string Platinum = "PLATINUM";
    }

    public static class SeatType
    {
        public const string StandardId = "SEAT_TYPE_STANDARD";
        public const string StandardName = "STANDARD";
    }

    public static class ResourceStatus
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Available = "AVAILABLE";
        public const string Unavailable = "UNAVAILABLE";
    }

    public static class VoucherStatus
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Expired = "EXPIRED";
    }

    public static class VoucherUsageStatus
    {
        public const string Applied = "APPLIED";
        public const string Confirmed = "CONFIRMED";
        public const string Cancelled = "CANCELLED";
    }

    public static class DiscountType
    {
        public const string Amount = "AMOUNT";
        public const string Percent = "PERCENT";
    }

    public static class ReviewStatus
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";
        public const string Flagged = "FLAGGED";
    }

    public static class PaymentProvider
    {
        public const string SepayId = "PP_SEPAY";
        public const string SepayName = "SEPAY";
    }

    public static class PaymentTransactionCode
    {
        public const string Pattern = @"T[A-Z0-9]{10}";
        public const char Prefix = 'T';
        public const int RandomPartLength = 10;
        public const string AllowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    }

    public static class TicketQrCode
    {
        public const string Prefix = "G2C";
        public const char Separator = '|';
    }

    public static class EntityIdPrefix
    {
        public const string User = "USR";
        public const string CustomerProfile = "CUS";
        public const string StaffProfile = "STF";
        public const string EmailVerificationToken = "EVT";
        public const string RefreshToken = "RFT";
        public const string Room = "ROOM";
        public const string Seat = "SEAT";
        public const string Showtime = "SHW";
        public const string ShowtimeSeat = "STS";
        public const string ShowtimeCancellation = "STC";
        public const string Booking = "BOK";
        public const string BookingSeat = "BKS";
        public const string BookingFoodItem = "BFI";
        public const string Payment = "PAY";
        public const string Refund = "REF";
        public const string RefundClaim = "RFC";
        public const string RefundClaimToken = "RFT";
        public const string Notification = "NOT";
        public const string AuditLog = "AUD";
        public const string RewardPointTransaction = "RPT";
        public const string ManualRefundProcess = "MRP";
        public const string CustomerRefundRequest = "CRR";
        public const string Ticket = "TCK";
        public const string CheckInLog = "CIL";
        public const string CinemaInventory = "CFI";
        public const string Movie = "MOV";
        public const string Review = "REV";
        public const string ChatHistory = "CHT";
    }

    public static class VerificationTokenPurpose
    {
        public const string EmailVerification = "EMAIL_VERIFICATION";
        public const string PasswordReset = "PASSWORD_RESET";
        public const string EmailUpdate = "EMAIL_UPDATE";
        public const string PhoneUpdate = "PHONE_UPDATE";
    }

    public static class EntityStatus
    {
        public const string Active = CinemaStatus.Active;
        public const string Inactive = CinemaStatus.Inactive;
        public const string Cancelled = BookingStatus.Cancelled;
        public const string Maintenance = CinemaStatus.Maintenance;
        public const string Available = ShowtimeSeatStatus.Available;
        public const string Paid = BookingStatus.Paid;
        public const string PendingPayment = BookingStatus.PendingPayment;
        public const string Failed = "FAILED";
        public const string PendingRefund = BookingStatus.RefundPending;
        public const string Refunded = BookingStatus.Refunded;
        public const string Ended = MovieStatus.Ended;
        public const string Locked = ShowtimeSeatStatus.Locked;
        public const string Open = ShowtimeStatus.Open;
        public const string Closed = ShowtimeStatus.Closed;
        public const string Completed = BookingStatus.Completed;
        public const string ProcessingUnstable = BookingStatus.ProcessingUnstable;
        public const string Archived = MovieStatus.Archived;
        public const string Suspended = ShowtimeStatus.Suspended;
        public const string NowShowing = MovieStatus.NowShowing;
        public const string ComingSoon = MovieStatus.ComingSoon;
        public const string Booked = ShowtimeSeatStatus.Booked;
    }

    public static class RefundStatus
    {
        public const string Pending = "PENDING";
        public const string Processing = "PROCESSING";
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string Requested = "REQUESTED";
        public const string ManualRequired = "MANUAL_REQUIRED";
    }

    public static class RefundWorkflowStatus
    {
        public const string AwaitingCustomerInfo = "AWAITING_CUSTOMER_INFO";
    }

    public static class RefundClaimStatus
    {
        public const string PendingInfo = "PENDING_INFO";
        public const string Submitted = "SUBMITTED";
        public const string Completed = "COMPLETED";
        public const string Expired = "EXPIRED";
        public const string ManualRequired = RefundStatus.ManualRequired;
        public const string Revoked = "REVOKED";
    }

    public static class AccountValidationStatus
    {
        public const string NotStarted = "NOT_STARTED";
        public const string Unavailable = "UNAVAILABLE";
    }

    public static class ManualRefundProcessStatus
    {
        public const string Open = "OPEN";
        public const string InProgress = "IN_PROGRESS";
        public const string Confirmed = "CONFIRMED";
        public const string Rejected = "REJECTED";
    }

    public static class CustomerRefundRequestStatus
    {
        public const string Pending = "PENDING";
        public const string Fulfilled = "FULFILLED";
        public const string Rejected = "REJECTED";
    }

    public static class RewardPointTransactionType
    {
        public const string Earn = "EARN";
        public const string Redeem = "REDEEM";
        public const string Revert = "REVERT";
        public const string Adjust = "ADJUST";
    }

    public static class RefundPolicy
    {
        public const int ClaimTokenEntropyBytes = 32;
    }

    public static class RefundErrorCode
    {
        public const string InvalidRefundStatus = "INVALID_REFUND_STATUS";
        public const string InvalidDateRange = "INVALID_DATE_RANGE";
        public const string RefundIdRequired = "REFUND_ID_REQUIRED";
        public const string RefundNotFound = "REFUND_NOT_FOUND";
        public const string RefundNotProcessable = "REFUND_NOT_PROCESSABLE";
        public const string RefundAlreadyCompleted = "REFUND_ALREADY_COMPLETED";
        public const string RefundAmountMismatch = "REFUND_AMOUNT_MISMATCH";
        public const string RefundNotManualRequired = "REFUND_NOT_MANUAL_REQUIRED";
        public const string RefundTotalExceedsPayment = "REFUND_TOTAL_EXCEEDS_PAYMENT";
        public const string RefundTransactionCodeDuplicate = "REFUND_TRANSACTION_CODE_DUPLICATE";
        public const string InvalidRefundProofUrl = "INVALID_REFUND_PROOF_URL";
        public const string ManualRefundNotFound = "MANUAL_REFUND_NOT_FOUND";
        public const string ManualRefundAlreadyAssigned = "MANUAL_REFUND_ALREADY_ASSIGNED";
        public const string ManualRefundNotAssignedToUser = "MANUAL_REFUND_NOT_ASSIGNED_TO_USER";
        public const string RefundClaimNotFound = "REFUND_CLAIM_NOT_FOUND";
        public const string RefundClaimForbidden = "REFUND_CLAIM_FORBIDDEN";
        public const string RefundClaimTokenUsed = "REFUND_CLAIM_TOKEN_USED";
        public const string RefundClaimExpired = "REFUND_CLAIM_EXPIRED";
        public const string RefundClaimNotEditable = "REFUND_CLAIM_NOT_EDITABLE";
        public const string RefundClaimNotReissuable = "REFUND_CLAIM_NOT_REISSUABLE";
        public const string RefundRequestTicketForbidden = "REFUND_REQUEST_TICKET_FORBIDDEN";
        public const string BankNotSupported = "BANK_NOT_SUPPORTED";
        public const string BankAccountRequired = "BANK_ACCOUNT_REQUIRED";
        public const string ShowtimeIdRequired = "SHOWTIME_ID_REQUIRED";
        public const string ShowtimeNotFound = "SHOWTIME_NOT_FOUND";
        public const string ShowtimeAlreadyCancelled = "SHOWTIME_ALREADY_CANCELLED";
        public const string ShowtimeAlreadyStarted = "SHOWTIME_ALREADY_STARTED";
        public const string CancellationReasonRequired = "CANCEL_REASON_REQUIRED";
        public const string CancellationReasonTooLong = "CANCEL_REASON_TOO_LONG";
        public const string PaidBookingPaymentNotFound = "PAID_BOOKING_PAYMENT_NOT_FOUND";
        public const string UserRequired = "USER_REQUIRED";
        public const string UserNotFound = "USER_NOT_FOUND";
    }

    public static class ManagerDashboard
    {
        public const string AllCinemasLabel = "All cinemas";
        public const decimal PercentageMultiplier = 100m;
        public const int OccupancyRateDecimalPlaces = 2;
    }

    public static class TicketStatus
    {
        public const string Generated = "GENERATED";
        public const string Unused = "UNUSED";
        public const string CheckedIn = "CHECKED_IN";
        public const string Cancelled = "CANCELLED";
        public const string Refunded = "REFUNDED";
    }

    public static class CheckInResult
    {
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
    }

    public static class TicketScanErrorCode
    {
        public const string InvalidQrCode = "INVALID_QR_CODE";
        public const string TicketNotFound = "TICKET_NOT_FOUND";
        public const string TicketWrongCinema = "TICKET_WRONG_CINEMA";
        public const string TicketWrongRoom = "TICKET_WRONG_ROOM";
        public const string TicketAlreadyCheckedIn = "TICKET_ALREADY_CHECKED_IN";
        public const string TicketCancelled = "TICKET_CANCELLED";
        public const string TicketRefunded = "TICKET_REFUNDED";
        public const string TicketNotUsable = "TICKET_NOT_USABLE";
        public const string BookingNotEligibleForCheckIn = "BOOKING_NOT_ELIGIBLE_FOR_CHECKIN";
        public const string ShowtimeCancelled = "SHOWTIME_CANCELLED";
        public const string CheckInTooEarly = "CHECKIN_TOO_EARLY";
        public const string CheckInWindowClosed = "CHECKIN_WINDOW_CLOSED";
        public const string TicketScanConflict = "TICKET_SCAN_CONFLICT";
        public const string ScanActorNotFound = "SCAN_ACTOR_NOT_FOUND";
        public const string ScanActorRoleForbidden = "SCAN_ACTOR_ROLE_FORBIDDEN";
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

    public static class AuditAction
    {
        public const string CancelShowtime = "CANCEL_SHOWTIME";
        public const string ProcessRefund = "PROCESS_REFUND";
        public const string SubmitRefundClaim = "SUBMIT_REFUND_CLAIM";
        public const string ReissueRefundClaimLink = "REISSUE_REFUND_CLAIM_LINK";
        public const string AssignManualRefund = "ASSIGN_MANUAL_REFUND";
        public const string ConfirmManualRefund = "CONFIRM_MANUAL_REFUND";
    }

    public static class AuditEntity
    {
        public const string Showtime = "SHOWTIME";
        public const string Refund = "REFUND";
        public const string RefundClaim = "REFUND_CLAIM";
    }
}
