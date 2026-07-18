namespace CinemaSystem.Application.Settings;

public sealed class EmailTemplatesSettings
{
    public const string SectionName = "EmailTemplates";

    public string VerificationSubject { get; set; } = "Cinema Booking - Email Verification";
    public string VerificationBody { get; set; } =
        "Your Cinema Booking email verification OTP is {0}. It expires at {1:yyyy-MM-dd HH:mm:ss} UTC.";

    public string PasswordResetSubject { get; set; } = "Cinema Booking - Password Reset";
    public string PasswordResetBody { get; set; } =
        "Your Cinema Booking password reset OTP is {0}. It expires at {1:yyyy-MM-dd HH:mm:ss} UTC.";

    public string EmailUpdateSubject { get; set; } = "Cinema Booking - Email Update Verification";
    public string EmailUpdateBody { get; set; } =
        "Your email-update OTP is {0}. It expires in {1} minutes.";

    public string StaffInvitationSubject { get; set; } = "Cinema Booking - Staff Invitation";
    public string StaffInvitationBody { get; set; } =
        "You have been invited to Cinema Booking. Use invitation code {0} to set your password.";

    public string SeatMaintenanceSubject { get; set; } = "Cinema Booking - Seat Maintenance";
    public string SeatMaintenanceBody { get; set; } =
        "Seat {0} for the showtime at {1} is unavailable. Booking: {2}. Confirmation token: {3}.";

    public string ShowtimeTimeChangeSubject { get; set; } = "Cinema Booking - Showtime Changed";
    public string ShowtimeTimeChangeBody { get; set; } =
        "The showtime for {0} has moved to {1}. Booking: {2}. Confirmation token: {3}.";

    public string ShowtimeTimeChangeNoticeSubject { get; set; } = "Cinema Booking - Showtime Update";
    public string ShowtimeTimeChangeNoticeBody { get; set; } =
        "The showtime for {0} has moved to {1}. Reason: {2}.";

    public string ShowtimeRoomChangeSubject { get; set; } = "Cinema Booking - Room Changed";
    public string ShowtimeRoomChangeBody { get; set; } =
        "Your screening room has changed to {0}.";

    public string ShowtimeCancellationSubject { get; set; } = "Cinema Booking - Showtime Cancelled";
    public string ShowtimeCancellationBody { get; set; } =
        "Your showtime was cancelled. Reason: {0}. Please wait for refund processing.";

    public string RefundClaimSubject { get; set; } =
        "Cinema Booking - Refund Information Required";
    public string RefundClaimBody { get; set; } =
        "The showtime for {0} was cancelled. Submit your refund information before {1:O}: {2}";

    public string ShowtimeCancelledNoRefundSubject { get; set; } =
        "Cinema Booking - Showtime Cancelled";
    public string ShowtimeCancelledNoRefundBody { get; set; } =
        "Showtime {0} at {1:O} has been cancelled. Booking status: {2}.";

    public string ShowtimeCancelledRefundSubject { get; set; } =
        "Cinema Booking - Showtime Cancelled, Refund Information Required";
    public string ShowtimeCancelledRefundBody { get; set; } =
        "Showtime {0} at {1:O} was cancelled. Expected refund: {2:N0}. Submit bank information before {3:O}: {4}";

    public string ShowtimeCancelledCompensationSubject { get; set; } =
        "Cinema Booking - Showtime Cancelled, Compensation Issued";
    public string ShowtimeCancelledCompensationBody { get; set; } =
        "Showtime {0} at {1:O} was cancelled. We issued {2} unrestricted ticket voucher(s) and one medium popcorn + medium soft drink voucher. They expire at {3:O}. Ticket codes: {4}. Combo code: {5}.";

    public string RefundCompletedSubject { get; set; } =
        "Cinema Booking - Refund Completed";
    public string RefundCompletedBody { get; set; } =
        "Your refund of {0:N0} for {1} was completed.";

    public string RefundManualRequiredSubject { get; set; } =
        "Cinema Booking - Refund Requires Manual Processing";
    public string RefundManualRequiredBody { get; set; } =
        "Your refund for {0} is being handled manually.";

    public string ManualRefundCompletedSubject { get; set; } =
        "Cinema Booking - Refund Transfer Completed";
    public string ManualRefundCompletedBody { get; set; } =
        "Your refund of {0:N0} for {1} has been transferred successfully.";
}
