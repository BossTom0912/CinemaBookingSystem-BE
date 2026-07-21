namespace CinemaSystem.Application.Settings;

public sealed class EmailTemplatesSettings
{
    public const string SectionName = "EmailTemplates";

    public string VerificationSubject { get; set; } = "[CinemaSystem] Mã xác thực OTP của bạn là: {0}";
    public string VerificationBody { get; set; } = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        </head>
        <body style='font-family: Arial, Helvetica, sans-serif; line-height: 1.6; color: #1e293b; background-color: #f8fafc; margin: 0; padding: 20px;'>
            <div style='max-width: 550px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
                <!-- HEADER -->
                <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 20px 25px; text-align: center; border-bottom: 3px solid #3b82f6;'>
                    <h1 style='color: #ffffff; margin: 0; font-size: 20px; font-weight: bold; letter-spacing: 1px;'>CINEMASYSTEM</h1>
                    <p style='color: #94a3b8; margin: 3px 0 0 0; font-size: 12px;'>Mã xác thực tài khoản (OTP)</p>
                </div>

                <!-- BODY CONTENT -->
                <div style='padding: 25px; text-align: center;'>
                    <p style='font-size: 14px; color: #334155; margin-top: 0; text-align: left;'>Kính gửi Quý khách hàng,</p>
                    <p style='font-size: 14px; color: #334155; text-align: left;'>
                        <strong>CinemaSystem</strong> đã nhận được yêu cầu xác thực tài khoản liên kết với địa chỉ email này. Vui lòng sử dụng mã xác thực (OTP) dưới đây để hoàn tất giao dịch:
                    </p>

                    <!-- OTP CODE BOX -->
                    <div style='background-color: #f1f5f9; border: 2px dashed #cbd5e1; padding: 18px; border-radius: 10px; margin: 20px 0;'>
                        <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #0f172a;'>{0}</span>
                    </div>

                    <p style='font-size: 13px; color: #64748b; margin-bottom: 20px;'>
                        Mã xác thực có hiệu lực đến <strong>{1:HH:mm - dd/MM/yyyy}</strong> (không chia sẻ mã này cho bất kỳ ai).
                    </p>

                    <!-- SAFETY WARNING -->
                    <div style='background-color: #fffbe6; border: 1px solid #ffe58f; padding: 12px 15px; border-radius: 8px; text-align: left; margin-bottom: 20px;'>
                        <p style='margin: 0; font-size: 12px; color: #856404;'>
                            <strong>Lưu ý an toàn:</strong> Nhân viên của CinemaSystem sẽ không bao giờ yêu cầu Quý khách cung cấp mã OTP này qua điện thoại, email hay tin nhắn. Nếu Quý khách không thực hiện yêu cầu này, vui lòng bỏ qua email hoặc đổi mật khẩu tài khoản ngay lập tức.
                        </p>
                    </div>

                    <p style='font-size: 13px; color: #475569; text-align: left;'>Nếu cần hỗ trợ thêm, Quý khách vui lòng liên hệ bộ phận CSKH qua Email <strong>cskh@cinemasystem.vn</strong> hoặc Hotline <strong>1900 6868</strong>.</p>
                </div>

                <!-- FOOTER -->
                <div style='background-color: #f1f5f9; padding: 15px 25px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                    <p style='margin: 0 0 4px 0; font-weight: bold; color: #0f172a;'>Đội ngũ Vận hành CinemaSystem</p>
                    <p style='margin: 0;'>Hotline: 1900 6868 | Email: cskh@cinemasystem.vn | Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
                </div>
            </div>
        </body>
        </html>
        """;

    public string PasswordResetSubject { get; set; } = "[CinemaSystem] Mã khôi phục mật khẩu OTP của bạn là: {0}";
    public string PasswordResetBody { get; set; } = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        </head>
        <body style='font-family: Arial, Helvetica, sans-serif; line-height: 1.6; color: #1e293b; background-color: #f8fafc; margin: 0; padding: 20px;'>
            <div style='max-width: 550px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
                <!-- HEADER -->
                <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 20px 25px; text-align: center; border-bottom: 3px solid #ef4444;'>
                    <h1 style='color: #ffffff; margin: 0; font-size: 20px; font-weight: bold; letter-spacing: 1px;'>CINEMASYSTEM</h1>
                    <p style='color: #94a3b8; margin: 3px 0 0 0; font-size: 12px;'>Mã khôi phục mật khẩu (OTP)</p>
                </div>

                <!-- BODY CONTENT -->
                <div style='padding: 25px; text-align: center;'>
                    <p style='font-size: 14px; color: #334155; margin-top: 0; text-align: left;'>Kính gửi Quý khách hàng,</p>
                    <p style='font-size: 14px; color: #334155; text-align: left;'>
                        <strong>CinemaSystem</strong> đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản liên kết với địa chỉ email này. Vui lòng sử dụng mã xác thực (OTP) dưới đây để tiến hành đặt lại mật khẩu:
                    </p>

                    <!-- OTP CODE BOX -->
                    <div style='background-color: #f1f5f9; border: 2px dashed #cbd5e1; padding: 18px; border-radius: 10px; margin: 20px 0;'>
                        <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #dc2626;'>{0}</span>
                    </div>

                    <p style='font-size: 13px; color: #64748b; margin-bottom: 20px;'>
                        Mã xác thực có hiệu lực đến <strong>{1:HH:mm - dd/MM/yyyy}</strong> (không chia sẻ mã này cho bất kỳ ai).
                    </p>

                    <!-- SAFETY WARNING -->
                    <div style='background-color: #fffbe6; border: 1px solid #ffe58f; padding: 12px 15px; border-radius: 8px; text-align: left; margin-bottom: 20px;'>
                        <p style='margin: 0; font-size: 12px; color: #856404;'>
                            <strong>Lưu ý an toàn:</strong> Nhân viên của CinemaSystem sẽ không bao giờ yêu cầu Quý khách cung cấp mã OTP này. Nếu Quý khách không thực hiện yêu cầu đặt lại mật khẩu, vui lòng liên hệ CSKH ngay lập tức.
                        </p>
                    </div>

                    <p style='font-size: 13px; color: #475569; text-align: left;'>Nếu cần hỗ trợ thêm, Quý khách vui lòng liên hệ CSKH qua Email <strong>cskh@cinemasystem.vn</strong> hoặc Hotline <strong>1900 6868</strong>.</p>
                </div>

                <!-- FOOTER -->
                <div style='background-color: #f1f5f9; padding: 15px 25px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                    <p style='margin: 0 0 4px 0; font-weight: bold; color: #0f172a;'>Đội ngũ Vận hành CinemaSystem</p>
                    <p style='margin: 0;'>Hotline: 1900 6868 | Email: cskh@cinemasystem.vn | Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
                </div>
            </div>
        </body>
        </html>
        """;

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
        "Your refund of {0:N0} for {1} has been transferred successfully. Bank transaction code: {2}.";

    public string RefundCustomerConfirmationSubject { get; set; } =
        "Cinema Booking - Confirm Manual Refund Details";
    public string RefundCustomerConfirmationBody { get; set; } =
        "Confirm manual refund details before {9:O}: refund {0:N0}; movie {1}; cinema {2}; room {3}; showtime {4:O}; seats {5}; bank {6}; account {7}; account holder {8}. Confirm here: {10}";
}
