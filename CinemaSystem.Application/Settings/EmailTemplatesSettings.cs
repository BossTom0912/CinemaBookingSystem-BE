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
}
