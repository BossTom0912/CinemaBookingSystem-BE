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
            <div style='max-width: 650px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
                
                <!-- HEADER -->
                <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 25px 30px; text-align: center; border-bottom: 3px solid #3b82f6;'>
                    <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: bold; letter-spacing: 1px;'>CINEMASYSTEM</h1>
                    <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Mã xác thực tài khoản</p>
                    <p style='color: #64748b; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>Account Verification Code</p>
                </div>

                <!-- CONTENT -->
                <div style='padding: 30px;'>
                    
                    <!-- TIẾNG VIỆT -->
                    <div style='margin-bottom: 25px;'>
                        <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý khách hàng,</p>
                        <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                            Lời đầu tiên, CinemaSystem xin gửi lời cảm ơn chân thành vì Quý khách đã luôn tin tưởng và sử dụng dịch vụ của chúng tôi. Vui lòng sử dụng mã xác thực (OTP) bên dưới để hoàn tất đăng ký tài khoản:
                        </p>

                        <div style='background-color: #f1f5f9; border: 2px dashed #cbd5e1; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center;'>
                            <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #0f172a;'>{0}</span>
                        </div>

                        <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                            Mã xác thực có hiệu lực đến <strong>{1:HH:mm - dd/MM/yyyy}</strong>. Vì lý do an toàn, vui lòng không chia sẻ mã này cho bất kỳ ai.
                        </p>

                        <p style='font-size: 13px; color: #334155; margin-top: 15px;'>
                            Trân trọng,<br>
                            <strong>Ban Quản trị CinemaSystem</strong>
                        </p>
                    </div>

                    <!-- DIVIDER -->
                    <hr style='border: none; border-top: 1px dashed #cbd5e1; margin: 25px 0;' />

                    <!-- TIẾNG ANH -->
                    <div>
                        <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear Valued Customer,</p>
                        <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                            Thank you for choosing CinemaSystem. Please use the verification code (OTP) below to complete your account registration:
                        </p>

                        <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; padding: 15px; border-radius: 8px; margin: 15px 0; text-align: center;'>
                            <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 26px; font-weight: bold; letter-spacing: 6px; color: #334155;'>{0}</span>
                        </div>

                        <p style='font-size: 12px; color: #64748b; margin-bottom: 15px;'>
                            This code is valid until <strong>{1:HH:mm - dd/MM/yyyy}</strong>. For security reasons, please do not share this code with anyone.
                        </p>

                        <p style='font-size: 12px; color: #64748b; margin-top: 15px;'>
                            Sincerely,<br>
                            <strong>CinemaSystem Management Team</strong>
                        </p>
                    </div>

                </div>

                <!-- FOOTER -->
                <div style='background-color: #f1f5f9; padding: 20px 30px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                    <p style='margin: 0 0 4px 0; font-weight: bold; color: #0f172a;'>Trung tâm Chăm sóc Khách hàng CinemaSystem</p>
                    <p style='margin: 0 0 4px 0;'>Hotline: <strong>1900 6868</strong> | Email: <strong>cskh@cinemasystem.vn</strong></p>
                    <p style='margin: 0;'>Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
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
            <div style='max-width: 650px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
                
                <!-- HEADER -->
                <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 25px 30px; text-align: center; border-bottom: 3px solid #ef4444;'>
                    <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: bold; letter-spacing: 1px;'>CINEMASYSTEM</h1>
                    <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Mã khôi phục mật khẩu</p>
                    <p style='color: #64748b; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>Password Reset Code</p>
                </div>

                <!-- CONTENT -->
                <div style='padding: 30px;'>
                    
                    <!-- TIẾNG VIỆT -->
                    <div style='margin-bottom: 25px;'>
                        <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý khách hàng,</p>
                        <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                            CinemaSystem đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng sử dụng mã xác thực (OTP) dưới đây để tiến hành thiết lập lại mật khẩu:
                        </p>

                        <div style='background-color: #f1f5f9; border: 2px dashed #cbd5e1; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center;'>
                            <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #dc2626;'>{0}</span>
                        </div>

                        <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                            Mã xác thực có hiệu lực đến <strong>{1:HH:mm - dd/MM/yyyy}</strong>. Tuyệt đối không chia sẻ mã này cho bất kỳ ai.
                        </p>

                        <div style='background-color: #fffbe6; border: 1px solid #ffe58f; padding: 14px 16px; border-radius: 8px; margin-bottom: 20px;'>
                            <p style='margin: 0; font-size: 12px; color: #856404;'>
                                <strong>Cảnh báo an toàn:</strong> Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng liên hệ bộ phận Chăm sóc Khách hàng ngay lập tức để bảo vệ tài khoản.
                            </p>
                        </div>

                        <p style='font-size: 13px; color: #334155; margin-top: 15px;'>
                            Trân trọng,<br>
                            <strong>Ban Quản trị CinemaSystem</strong>
                        </p>
                    </div>

                    <!-- DIVIDER -->
                    <hr style='border: none; border-top: 1px dashed #cbd5e1; margin: 25px 0;' />

                    <!-- TIẾNG ANH -->
                    <div>
                        <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear Valued Customer,</p>
                        <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                            CinemaSystem received a request to reset your account password. Please use the verification code (OTP) below to proceed:
                        </p>

                        <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; padding: 15px; border-radius: 8px; margin: 15px 0; text-align: center;'>
                            <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 26px; font-weight: bold; letter-spacing: 6px; color: #dc2626;'>{0}</span>
                        </div>

                        <p style='font-size: 12px; color: #64748b; margin-bottom: 15px;'>
                            This code is valid until <strong>{1:HH:mm - dd/MM/yyyy}</strong>. Do not share this code.
                        </p>

                        <div style='background-color: #fef2f2; border: 1px solid #fca5a5; padding: 12px 14px; border-radius: 8px; margin-bottom: 15px;'>
                            <p style='margin: 0; font-size: 11px; color: #991b1b;'>
                                <strong>Security Notice:</strong> If you did not request a password reset, please contact Customer Support immediately to secure your account.
                            </p>
                        </div>

                        <p style='font-size: 12px; color: #64748b; margin-top: 15px;'>
                            Sincerely,<br>
                            <strong>CinemaSystem Management Team</strong>
                        </p>
                    </div>

                </div>

                <!-- FOOTER -->
                <div style='background-color: #f1f5f9; padding: 20px 30px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                    <p style='margin: 0 0 4px 0; font-weight: bold; color: #0f172a;'>Trung tâm Chăm sóc Khách hàng CinemaSystem</p>
                    <p style='margin: 0 0 4px 0;'>Hotline: <strong>1900 6868</strong> | Email: <strong>cskh@cinemasystem.vn</strong></p>
                    <p style='margin: 0;'>Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
                </div>
            </div>
        </body>
        </html>
        """;

    public string EmailUpdateSubject { get; set; } = "[CinemaSystem] Xác thực thay đổi địa chỉ Email";
    public string EmailUpdateBody { get; set; } = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        </head>
        <body style='font-family: Arial, Helvetica, sans-serif; line-height: 1.6; color: #1e293b; background-color: #f8fafc; margin: 0; padding: 20px;'>
            <div style='max-width: 650px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
                
                <!-- HEADER -->
                <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 25px 30px; text-align: center; border-bottom: 3px solid #3b82f6;'>
                    <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: bold; letter-spacing: 1px;'>CINEMASYSTEM</h1>
                    <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Mã xác thực thay đổi Email</p>
                    <p style='color: #64748b; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>Email Update Verification Code</p>
                </div>

                <!-- CONTENT -->
                <div style='padding: 30px;'>
                    
                    <!-- TIẾNG VIỆT -->
                    <div style='margin-bottom: 25px;'>
                        <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý khách hàng,</p>
                        <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                            Bạn đã yêu cầu cập nhật địa chỉ email cho tài khoản CinemaSystem. Vui lòng sử dụng mã xác thực (OTP) dưới đây để hoàn tất:
                        </p>

                        <div style='background-color: #f1f5f9; border: 2px dashed #cbd5e1; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center;'>
                            <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #0f172a;'>{0}</span>
                        </div>

                        <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                            Mã này có hiệu lực trong vòng <strong>{1} phút</strong>.
                        </p>

                        <p style='font-size: 13px; color: #334155; margin-top: 15px;'>
                            Trân trọng,<br>
                            <strong>Ban Quản trị CinemaSystem</strong>
                        </p>
                    </div>

                    <!-- DIVIDER -->
                    <hr style='border: none; border-top: 1px dashed #cbd5e1; margin: 25px 0;' />

                    <!-- TIẾNG ANH -->
                    <div>
                        <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear Valued Customer,</p>
                        <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                            You have requested to update your email address. Please use the verification code (OTP) below to proceed:
                        </p>

                        <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; padding: 15px; border-radius: 8px; margin: 15px 0; text-align: center;'>
                            <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 26px; font-weight: bold; letter-spacing: 6px; color: #334155;'>{0}</span>
                        </div>

                        <p style='font-size: 12px; color: #64748b; margin-bottom: 15px;'>
                            This code expires in <strong>{1} minutes</strong>.
                        </p>

                        <p style='font-size: 12px; color: #64748b; margin-top: 15px;'>
                            Sincerely,<br>
                            <strong>CinemaSystem Management Team</strong>
                        </p>
                    </div>

                </div>

                <!-- FOOTER -->
                <div style='background-color: #f1f5f9; padding: 20px 30px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                    <p style='margin: 0 0 4px 0; font-weight: bold; color: #0f172a;'>Trung tâm Chăm sóc Khách hàng CinemaSystem</p>
                    <p style='margin: 0 0 4px 0;'>Hotline: <strong>1900 6868</strong> | Email: <strong>cskh@cinemasystem.vn</strong></p>
                    <p style='margin: 0;'>Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
                </div>
            </div>
        </body>
        </html>
        """;

    public string AccountInvitationSubject { get; set; } = "[CinemaSystem] Thư mời khởi tạo tài khoản hệ thống";
    public string AccountInvitationBody { get; set; } = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        </head>
        <body style='font-family: Arial, Helvetica, sans-serif; line-height: 1.6; color: #1e293b; background-color: #f8fafc; margin: 0; padding: 20px;'>
            <div style='max-width: 650px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
                
                <!-- HEADER -->
                <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 25px 30px; text-align: center; border-bottom: 3px solid #10b981;'>
                    <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: bold; letter-spacing: 1px;'>CINEMASYSTEM</h1>
                    <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Thư mời khởi tạo tài khoản</p>
                    <p style='color: #64748b; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>System Account Invitation</p>
                </div>

                <!-- CONTENT -->
                <div style='padding: 30px;'>
                    
                    <!-- TIẾNG VIỆT -->
                    <div style='margin-bottom: 25px;'>
                        <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý nhân viên / Đối tác,</p>
                        <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                            Bạn đã nhận được thư mời khởi tạo tài khoản làm việc tại hệ thống <strong>CinemaSystem</strong>. Vui lòng sử dụng mã xác nhận bên dưới để thiết lập mật khẩu cá nhân:
                        </p>

                        <div style='background-color: #f1f5f9; border: 2px dashed #cbd5e1; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center;'>
                            <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 28px; font-weight: bold; letter-spacing: 6px; color: #059669;'>{0}</span>
                        </div>

                        <p style='font-size: 13px; color: #334155; margin-top: 15px;'>
                            Trân trọng,<br>
                            <strong>Ban Quản trị CinemaSystem</strong>
                        </p>
                    </div>

                    <!-- DIVIDER -->
                    <hr style='border: none; border-top: 1px dashed #cbd5e1; margin: 25px 0;' />

                    <!-- TIẾNG ANH -->
                    <div>
                        <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear Team Member / Partner,</p>
                        <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                            You have been invited to join the CinemaSystem management platform. Use your invitation code below to complete account activation:
                        </p>

                        <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; padding: 15px; border-radius: 8px; margin: 15px 0; text-align: center;'>
                            <span style='font-family: Consolas, Monaco, "Courier New", monospace; font-size: 24px; font-weight: bold; letter-spacing: 4px; color: #059669;'>{0}</span>
                        </div>

                        <p style='font-size: 12px; color: #64748b; margin-top: 15px;'>
                            Sincerely,<br>
                            <strong>CinemaSystem Management Team</strong>
                        </p>
                    </div>

                </div>

                <!-- FOOTER -->
                <div style='background-color: #f1f5f9; padding: 20px 30px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                    <p style='margin: 0 0 4px 0; font-weight: bold; color: #0f172a;'>Trung tâm Chăm sóc Khách hàng CinemaSystem</p>
                    <p style='margin: 0 0 4px 0;'>Hotline: <strong>1900 6868</strong> | Email: <strong>cskh@cinemasystem.vn</strong></p>
                    <p style='margin: 0;'>Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
                </div>
            </div>
        </body>
        </html>
        """;

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

    public string RefundCustomerConfirmationSubject { get; set; } =
        "Cinema Booking - Confirm Manual Refund Details";
    public string RefundCustomerConfirmationBody { get; set; } =
        "Confirm manual refund details before {9:O}: refund {0:N0}; movie {1}; cinema {2}; room {3}; showtime {4:O}; seats {5}; bank {6}; account {7}; account holder {8}. Confirm here: {10}";
}
