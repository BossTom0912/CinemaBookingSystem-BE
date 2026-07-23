using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Services;

public class GeminiAiEmailService : IAiEmailService
{
    private readonly IEmailService _emailService;
    private readonly RefundSettings _refundSettings;

    public GeminiAiEmailService(
        IOptions<GeminiSettings> settings, 
        IEmailService emailService,
        IOptions<RefundSettings>? refundSettings = null)
    {
        _emailService = emailService;
        _refundSettings = refundSettings?.Value ?? new RefundSettings();
    }

    public async Task SendAiApologyEmailAsync(
        string toEmail, 
        string subject, 
        string reason, 
        string details, 
        CancellationToken cancellationToken,
        string? customerName = null)
    {
        var formattedSubject = subject.StartsWith("[CinemaSystem]") ? subject : $"[CinemaSystem] Thông báo dịch vụ - {subject}";
        var displayName = string.IsNullOrWhiteSpace(customerName) ? "Quý khách" : customerName.Trim();

        var bodyHtml = $"""
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
                        <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Thông báo hỗ trợ & Xin lỗi dịch vụ</p>
                        <p style='color: #64748b; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>Service Notification & Apology</p>
                    </div>

                    <!-- CONTENT -->
                    <div style='padding: 30px;'>
                        
                        <!-- TIẾNG VIỆT -->
                        <div style='margin-bottom: 25px;'>
                            <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi {displayName},</p>
                            
                            <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                                Lời đầu tiên, CinemaSystem xin gửi lời cảm ơn chân thành vì Quý khách đã luôn tin tưởng và sử dụng dịch vụ của chúng tôi.
                            </p>

                            <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                                Chúng tôi xin trân trọng thông báo về sự cố <strong>{reason}</strong> liên quan đến đơn hàng xem phim của Quý khách.
                            </p>

                            <div style='background-color: #eff6ff; border-left: 4px solid #3b82f6; padding: 16px; border-radius: 0 8px 8px 0; margin-bottom: 20px;'>
                                <p style='margin: 0; font-size: 13px; color: #1e40af;'>
                                    <strong>Chi tiết sự cố:</strong> {details}
                                </p>
                            </div>

                            <p style='font-size: 14px; color: #334155; font-weight: bold;'>Phương án hỗ trợ từ CinemaSystem:</p>
                            <ul style='font-size: 13px; color: #334155; padding-left: 20px; line-height: 1.8; margin-top: 5px;'>
                                <li><strong>Tiếp tục suất chiếu:</strong> Vé của Quý khách vẫn hoàn toàn hợp lệ cho phòng chiếu / suất chiếu điều chỉnh. Quý khách không cần thực hiện thêm bất kỳ thao tác nào.</li>
                                <li><strong>Đổi suất chiếu / Hoàn tiền:</strong> Nếu khung giờ mới không phù hợp, Quý khách có thể liên hệ CSKH để đổi suất chiếu khác hoặc nhận hoàn tiền 100% trước giờ chiếu.</li>
                            </ul>

                            <p style='font-size: 13px; color: #334155; margin-top: 15px;'>
                                Một lần nữa, chúng tôi chân thành xin lỗi vì sự bất tiện này và rất mong nhận được sự thông cảm từ Quý khách.
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
                            <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear {displayName},</p>
                            
                            <p style='font-size: 13px; color: #64748b; margin-bottom: 12px;'>
                                First and foremost, thank you for choosing CinemaSystem. We sincerely apologize for the inconvenience regarding <strong>{reason}</strong> for your movie booking.
                            </p>

                            <div style='background-color: #f8fafc; border-left: 4px solid #94a3b8; padding: 14px; border-radius: 0 8px 8px 0; margin-bottom: 15px;'>
                                <p style='margin: 0; font-size: 12px; color: #475569;'>
                                    <strong>Details:</strong> {details}
                                </p>
                            </div>

                            <p style='font-size: 13px; color: #64748b; font-weight: bold;'>Support Options:</p>
                            <ul style='font-size: 12px; color: #64748b; padding-left: 20px; line-height: 1.8; margin-top: 5px;'>
                                <li><strong>Keep Showtime:</strong> Your ticket remains fully valid. No further action is required from your side.</li>
                                <li><strong>Change Showtime / 100% Refund:</strong> If the updated time is inconvenient, you may contact CSKH to reschedule or receive a full refund.</li>
                            </ul>

                            <p style='font-size: 12px; color: #64748b; margin-top: 15px;'>
                                We apologize for any inconvenience caused and appreciate your understanding.
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

        await _emailService.SendEmailAsync(toEmail, formattedSubject, bodyHtml, cancellationToken);
    }

    public async Task SendAiTimeChangeEmailAsync(
        string toEmail,
        string subject,
        string movieTitle,
        string oldTime,
        string newTime,
        string cutoffTime,
        string bookingId,
        string token,
        CancellationToken cancellationToken,
        string? compensationVoucherCode = null,
        string? compensationNote = null,
        string? targetSeatType = null,
        string? customerName = null)
    {
        var formattedSubject = $"[CinemaSystem] Thông báo điều chỉnh giờ chiếu phim \"{movieTitle}\" (Mã vé: #{bookingId})";
        var displayName = string.IsNullOrWhiteSpace(customerName) ? "Quý khách" : customerName.Trim();

        var baseUrl = string.IsNullOrWhiteSpace(_refundSettings.FrontendBaseUrl)
            ? "http://localhost:5173"
            : _refundSettings.FrontendBaseUrl.TrimEnd('/');

        var confirmAcceptUrl = $"{baseUrl}/booking/confirm-time-change?bookingId={bookingId}&accept=true&token={token}";
        var confirmRefundUrl = $"{baseUrl}/booking/confirm-time-change?bookingId={bookingId}&accept=false&token={token}";

        var compVoucherDisplayVi = !string.IsNullOrWhiteSpace(compensationVoucherCode)
            ? $"[{compensationVoucherCode.Trim()}]"
            : "—";

        if (!string.IsNullOrWhiteSpace(targetSeatType))
        {
            compVoucherDisplayVi += $" (Ưu tiên nâng hạng ghế: {targetSeatType.Trim()})";
        }
        if (!string.IsNullOrWhiteSpace(compensationNote))
        {
            compVoucherDisplayVi += $" ({compensationNote.Trim()})";
        }

        var bodyHtml = $"""
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
                        <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Thông báo điều chỉnh giờ chiếu phim</p>
                        <p style='color: #64748b; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>Showtime Schedule Change Notice</p>
                    </div>

                    <!-- CONTENT -->
                    <div style='padding: 30px;'>
                        
                        <!-- TIẾNG VIỆT -->
                        <div style='margin-bottom: 25px;'>
                            <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi {displayName},</p>

                            <p style='font-size: 14px; color: #334155; margin-bottom: 12px;'>
                                Lời đầu tiên, CinemaSystem xin gửi lời cảm ơn chân thành tới Quý khách vì đã luôn tin tưởng và ủng hộ dịch vụ của chúng tôi.
                            </p>

                            <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                                Do có sự điều chỉnh trong lịch chiếu, chúng tôi xin thông báo về thay đổi giờ chiếu cho bộ phim <strong>{movieTitle}</strong> trong đơn hàng của Quý khách như sau:
                            </p>

                            <!-- BẢNG DỮ LIỆU TIẾNG VIỆT -->
                            <div style='margin: 20px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 13px; text-align: left;'>
                                    <thead>
                                        <tr style='background-color: #f1f5f9; color: #0f172a;'>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1; width: 35%;'>Thông tin</th>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1;'>Giờ cũ</th>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1;'>Giờ mới</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Bộ phim</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0;'>{movieTitle}</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0;'>{movieTitle}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Thời gian chiếu</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #dc2626; text-decoration: line-through;'>{oldTime}</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{newTime}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Mã đặt vé</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-family: monospace; font-weight: bold;'>#{bookingId}</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-family: monospace; font-weight: bold;'>#{bookingId}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Voucher đền bù</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #94a3b8;'>—</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #d97706; font-weight: bold;'>{compVoucherDisplayVi}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <!-- LƯU Ý QUAN TRỌNG -->
                            <div style='background-color: #fffbe6; border: 1px solid #ffe58f; border-left: 4px solid #d97706; padding: 14px 16px; border-radius: 6px; margin: 20px 0;'>
                                <p style='margin: 0; font-size: 13px; color: #856404; font-weight: bold;'>
                                    Lưu ý quan trọng: Vui lòng đưa ra lựa chọn trước {cutoffTime}.
                                </p>
                            </div>

                            <p style='font-size: 14px; color: #0f172a; font-weight: bold; margin-top: 20px;'>Quý khách vui lòng chọn 1 trong 2 phương án bên dưới:</p>
                            
                            <!-- PHƯƠNG ÁN 1 -->
                            <div style='margin: 15px 0; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px;'>
                                <p style='margin: 0 0 6px 0; font-size: 14px; font-weight: bold; color: #0f172a;'>Phương án 1: Đồng ý xem suất chiếu mới</p>
                                <p style='margin: 0 0 14px 0; font-size: 13px; color: #475569;'>Hệ thống sẽ tự động cập nhật vé theo giờ chiếu mới và gửi kèm ưu đãi đền bù tới tài khoản của Quý khách.</p>
                                <a href='{confirmAcceptUrl}' style='display: inline-block; padding: 12px 24px; background-color: #ffffff; color: #16a34a; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 13px; border: 2px solid #000000;'>[ XÁC NHẬN XEM SUẤT CHIẾU MỚI ]</a>
                            </div>

                            <!-- PHƯƠNG ÁN 2 -->
                            <div style='margin: 15px 0; background-color: #fef2f2; border: 1px solid #fca5a5; border-radius: 8px; padding: 16px;'>
                                <p style='margin: 0 0 6px 0; font-size: 14px; font-weight: bold; color: #b91c1c;'>Phương án 2: Yêu cầu hoàn tiền 100%</p>
                                <p style='margin: 0 0 14px 0; font-size: 13px; color: #7f1d1d;'>Nếu khung giờ mới không phù hợp với lịch trình, Quý khách có thể yêu cầu hoàn trả 100% giá trị tiền vé.</p>
                                <a href='{confirmRefundUrl}' style='display: inline-block; padding: 12px 24px; background-color: #ef4444; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 13px; border: 2px solid #991b1b;'>[ YÊU CẦU HOÀN TIỀN 100% ]</a>
                            </div>

                            <p style='font-size: 13px; color: #334155; margin-top: 20px;'>
                                Mọi thắc mắc cần hỗ trợ gấp, Quý khách vui lòng liên hệ Bộ phận Chăm sóc Khách hàng qua Hotline/Email của CinemaSystem. Chân thành xin lỗi Quý khách vì sự bất tiện này.
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
                            <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear {displayName},</p>
                            <p style='font-size: 13px; color: #64748b; margin-bottom: 12px;'>
                                Thank you for choosing CinemaSystem. We are writing to inform you of a showtime adjustment for your upcoming movie booking.
                            </p>
                            <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                                Here are the updated details for your booking:
                            </p>

                            <!-- ENGLISH TABLE -->
                            <div style='margin: 15px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 12px; text-align: left;'>
                                    <thead>
                                        <tr style='background-color: #f8fafc; color: #475569;'>
                                            <th style='padding: 8px 12px; border-bottom: 1px solid #cbd5e1; width: 35%;'>Detail</th>
                                            <th style='padding: 8px 12px; border-bottom: 1px solid #cbd5e1;'>Previous Time</th>
                                            <th style='padding: 8px 12px; border-bottom: 1px solid #cbd5e1;'>New Time</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Movie Title</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0;'>{movieTitle}</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0;'>{movieTitle}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Showtime</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #dc2626; text-decoration: line-through;'>{oldTime}</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{newTime}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Booking ID</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-family: monospace;'>#{bookingId}</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-family: monospace;'>#{bookingId}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Compensation Voucher</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #94a3b8;'>—</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #d97706; font-weight: bold;'>{compVoucherDisplayVi}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <!-- IMPORTANT NOTE BOX EN -->
                            <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid #d97706; padding: 12px; border-radius: 6px; margin: 15px 0;'>
                                <p style='margin: 0; font-size: 12px; color: #856404; font-weight: bold;'>
                                    Important Note: Please select your preference before {cutoffTime}.
                                </p>
                            </div>

                            <p style='font-size: 13px; color: #475569; font-weight: bold; margin-top: 15px;'>Please select your option below:</p>

                            <!-- OPTION 1 EN -->
                            <div style='margin: 12px 0; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 14px;'>
                                <p style='margin: 0 0 4px 0; font-size: 13px; font-weight: bold; color: #0f172a;'>Option 1: Accept New Showtime</p>
                                <p style='margin: 0 0 10px 0; font-size: 12px; color: #475569;'>Your ticket will be updated to the new showtime, and the compensation voucher will be automatically applied.</p>
                                <a href='{confirmAcceptUrl}' style='display: inline-block; padding: 10px 20px; background-color: #ffffff; color: #16a34a; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 12px; border: 2px solid #000000;'>[ CONFIRM NEW SHOWTIME ]</a>
                            </div>

                            <!-- OPTION 2 EN -->
                            <div style='margin: 12px 0; background-color: #fef2f2; border: 1px solid #fca5a5; border-radius: 8px; padding: 14px;'>
                                <p style='margin: 0 0 4px 0; font-size: 13px; font-weight: bold; color: #b91c1c;'>Option 2: Request 100% Refund</p>
                                <p style='margin: 0 0 10px 0; font-size: 12px; color: #7f1d1d;'>If the new time is inconvenient, you can request a full refund for your booking.</p>
                                <a href='{confirmRefundUrl}' style='display: inline-block; padding: 10px 20px; background-color: #ef4444; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 12px; border: 2px solid #991b1b;'>[ REQUEST 100% REFUND ]</a>
                            </div>

                            <p style='font-size: 12px; color: #64748b; margin-top: 15px;'>
                                We sincerely apologize for any inconvenience caused and thank you for your understanding.
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

        await _emailService.SendEmailAsync(toEmail, formattedSubject, bodyHtml, cancellationToken);
    }

    public async Task SendAiRoomChangeEmailAsync(
        string toEmail,
        string subject,
        string movieTitle,
        string oldRoomName,
        string newRoomName,
        string timeStr,
        string bookingId,
        CancellationToken cancellationToken,
        string? compensationVoucherCode = null,
        string? compensationNote = null,
        string? targetSeatType = null,
        string? customerName = null)
    {
        var formattedSubject = $"[CinemaSystem] Thông báo điều chỉnh phòng chiếu - Mã vé: #{bookingId}";
        var displayName = string.IsNullOrWhiteSpace(customerName) ? "Quý khách" : customerName.Trim();

        var compParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(compensationVoucherCode)) compParts.Add($"Voucher: [{compensationVoucherCode.Trim()}]");
        if (!string.IsNullOrWhiteSpace(targetSeatType)) compParts.Add($"Nâng hạng ghế: {targetSeatType.Trim()}");
        if (!string.IsNullOrWhiteSpace(compensationNote)) compParts.Add(compensationNote.Trim());
        var compensationText = compParts.Count > 0 ? string.Join(" | ", compParts) : "Không có";

        var bodyHtml = $"""
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
                        <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Thông báo thay đổi phòng chiếu</p>
                        <p style='color: #64748b; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>Auditorium Change Notification</p>
                    </div>

                    <!-- CONTENT -->
                    <div style='padding: 30px;'>
                        
                        <!-- TIẾNG VIỆT -->
                        <div style='margin-bottom: 25px;'>
                            <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi {displayName},</p>

                            <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                                Lời đầu tiên, CinemaSystem xin gửi lời cảm ơn chân thành vì Quý khách đã luôn tin tưởng và sử dụng dịch vụ của chúng tôi. Chúng tôi xin thông báo về việc thay đổi phòng chiếu cho suất phim của Quý khách.
                            </p>

                            <div style='background-color: #eff6ff; border-left: 4px solid #3b82f6; padding: 16px; border-radius: 0 8px 8px 0; margin-bottom: 20px;'>
                                <p style='margin: 0; font-size: 13px; color: #1e40af;'>
                                    <strong>Lưu ý quan trọng:</strong> Thời gian chiếu phim vẫn được <strong>GIỮ NGUYÊN ({timeStr})</strong>. Quý khách không cần thực hiện thêm thao tác nào; vé điện tử và mã QR đã được tự động cập nhật cho phòng chiếu mới.
                                </p>
                            </div>

                            <!-- BẢNG CHI TIẾT TIẾNG VIỆT -->
                            <div style='margin: 20px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 13px; text-align: left;'>
                                    <thead>
                                        <tr style='background-color: #f1f5f9; color: #0f172a;'>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1; width: 40%;'>Thông tin</th>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1;'>Chi tiết</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Bộ phim</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0;'>{movieTitle}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Thời gian chiếu</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{timeStr} (Không đổi)</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Phòng chiếu cũ</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #dc2626; text-decoration: line-through;'>{oldRoomName}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Phòng chiếu mới</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #2563eb; font-weight: bold;'>{newRoomName}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Quyền lợi đền bù</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #d97706; font-weight: bold;'>{compensationText}</td>
                                        </tr>
                                    </tbody>
                                </table>
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
                            <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear {displayName},</p>
                            <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                                We regret to inform you that your screening room for <strong>{movieTitle}</strong> has been changed from {oldRoomName} to {newRoomName}.
                            </p>

                            <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid #3b82f6; padding: 12px; border-radius: 6px; margin: 15px 0;'>
                                <p style='margin: 0; font-size: 12px; color: #475569;'>
                                    <strong>Important Note:</strong> Your showtime remains unchanged at <strong>{timeStr}</strong>. Your ticket and QR code have been automatically updated for the new auditorium.
                                </p>
                            </div>

                            <!-- BẢNG CHI TIẾT TIẾNG ANH -->
                            <div style='margin: 20px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 12px; text-align: left;'>
                                    <thead>
                                        <tr style='background-color: #f8fafc; color: #475569;'>
                                            <th style='padding: 8px 12px; border-bottom: 1px solid #cbd5e1; width: 40%;'>Information</th>
                                            <th style='padding: 8px 12px; border-bottom: 1px solid #cbd5e1;'>Details</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Movie Title</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0;'>{movieTitle}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Showtime</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{timeStr} (Unchanged)</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Previous Auditorium</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #dc2626; text-decoration: line-through;'>{oldRoomName}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>New Auditorium</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #2563eb; font-weight: bold;'>{newRoomName}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Compensation Benefit</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #d97706; font-weight: bold;'>{compensationText}</td>
                                        </tr>
                                    </tbody>
                                </table>
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

        await _emailService.SendEmailAsync(toEmail, formattedSubject, bodyHtml, cancellationToken);
    }

    public async Task SendVoucherGiftEmailAsync(
        string toEmail,
        string customerName,
        string voucherTitle,
        string voucherCode,
        string discountText,
        string validityText,
        string? description,
        string? category,
        CancellationToken cancellationToken)
    {
        var formattedSubject = $"[CinemaSystem] Bạn vừa nhận được Voucher ưu đãi đặc biệt: [{voucherCode}]";
        var baseUrl = string.IsNullOrWhiteSpace(_refundSettings.FrontendBaseUrl)
            ? "http://localhost:5173"
            : _refundSettings.FrontendBaseUrl.TrimEnd('/');

        var categoryDisplayVi = string.IsNullOrWhiteSpace(category) ? "ƯU ĐÃI ĐẶC BIỆT" : category.Trim().ToUpperInvariant();
        var categoryDisplayEn = string.IsNullOrWhiteSpace(category) ? "SPECIAL OFFER" : category.Trim().ToUpperInvariant();
        var displayName = string.IsNullOrWhiteSpace(customerName) ? "Quý khách" : customerName.Trim();

        var descriptionRowVi = !string.IsNullOrWhiteSpace(description)
            ? $"""
                <tr>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold; background-color: #f8fafc;'>Điều kiện áp dụng</td>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0;'>{description}</td>
                </tr>
              """
            : "";

        var descriptionRowEn = !string.IsNullOrWhiteSpace(description)
            ? $"""
                <tr>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold; background-color: #f8fafc; color: #475569;'>Terms & Conditions</td>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #475569;'>{description}</td>
                </tr>
              """
            : "";

        var bodyHtml = $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            </head>
            <body style='font-family: Arial, Helvetica, sans-serif; line-height: 1.6; color: #1e293b; background-color: #f8fafc; margin: 0; padding: 20px;'>
                <div style='max-width: 650px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
                    
                    <!-- HEADER -->
                    <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 25px 30px; text-align: center; border-bottom: 3px solid #f59e0b;'>
                        <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: bold; letter-spacing: 1px;'>CINEMASYSTEM</h1>
                        <p style='color: #fbbf24; margin: 4px 0 0 0; font-size: 14px; font-weight: bold;'>THÔNG BÁO TẶNG VOUCHER ƯU ĐÃI</p>
                        <p style='color: #94a3b8; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>Exclusive Voucher Gift Notification</p>
                    </div>

                    <!-- CONTENT -->
                    <div style='padding: 30px;'>
                        
                        <!-- TIẾNG VIỆT -->
                        <div style='margin-bottom: 25px;'>
                            <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi {displayName},</p>
                            
                            <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                                Chúc mừng! CinemaSystem xin trân trọng gửi tặng Quý khách một voucher ưu đãi đặc biệt dành riêng cho tài khoản của Quý khách.
                            </p>

                            <!-- VOUCHER CARD BOX (VI) -->
                            <div style='background: linear-gradient(135deg, #fffbe6 0%, #fef3c7 100%); border: 2px dashed #f59e0b; border-radius: 10px; padding: 20px; text-align: center; margin: 20px 0;'>
                                <span style='display: inline-block; background-color: #d97706; color: #ffffff; font-size: 11px; font-weight: bold; padding: 3px 10px; border-radius: 12px; text-transform: uppercase; margin-bottom: 8px;'>{categoryDisplayVi}</span>
                                <h3 style='margin: 4px 0 10px 0; font-size: 18px; color: #78350f; font-weight: bold;'>{voucherTitle}</h3>
                                
                                <div style='margin: 12px 0; padding: 10px; background-color: #ffffff; border: 1px solid #fde68a; border-radius: 8px; display: inline-block;'>
                                    <span style='font-size: 11px; color: #92400e; display: block; margin-bottom: 4px; font-weight: bold; letter-spacing: 1px;'>MÃ VOUCHER</span>
                                    <span style='font-family: monospace; font-size: 24px; font-weight: bold; color: #b45309; letter-spacing: 3px;'>{voucherCode}</span>
                                </div>

                                <p style='margin: 8px 0 0 0; font-size: 15px; font-weight: bold; color: #b45309;'>{discountText}</p>
                                <p style='margin: 4px 0 0 0; font-size: 12px; color: #78350f;'>Hạn sử dụng: <strong>{validityText}</strong></p>
                            </div>

                            <!-- CHI TIẾT BẢNG (VI) -->
                            <div style='margin: 15px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 13px; text-align: left;'>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold; width: 35%; background-color: #f8fafc;'>Mức ưu đãi</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{discountText}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold; background-color: #f8fafc;'>Thời hạn áp dụng</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0;'>{validityText}</td>
                                        </tr>
                                        {descriptionRowVi}
                                    </tbody>
                                </table>
                            </div>

                            <!-- BUTTON CTA (VI) -->
                            <div style='text-align: center; margin: 25px 0 15px 0;'>
                                <a href='{baseUrl}' style='display: inline-block; padding: 12px 28px; background-color: #0f172a; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 13px; border: 2px solid #000000;'>[ DÙNG VOUCHER NGAY ]</a>
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
                            <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear {displayName},</p>
                            
                            <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                                Congratulations! CinemaSystem is pleased to present you with an exclusive discount voucher for your account.
                            </p>

                            <!-- VOUCHER CARD BOX (EN) -->
                            <div style='background: linear-gradient(135deg, #fffbe6 0%, #fef3c7 100%); border: 2px dashed #f59e0b; border-radius: 10px; padding: 20px; text-align: center; margin: 20px 0;'>
                                <span style='display: inline-block; background-color: #d97706; color: #ffffff; font-size: 11px; font-weight: bold; padding: 3px 10px; border-radius: 12px; text-transform: uppercase; margin-bottom: 8px;'>{categoryDisplayEn}</span>
                                <h3 style='margin: 4px 0 10px 0; font-size: 18px; color: #78350f; font-weight: bold;'>{voucherTitle}</h3>
                                
                                <div style='margin: 12px 0; padding: 10px; background-color: #ffffff; border: 1px solid #fde68a; border-radius: 8px; display: inline-block;'>
                                    <span style='font-size: 11px; color: #92400e; display: block; margin-bottom: 4px; font-weight: bold; letter-spacing: 1px;'>VOUCHER CODE</span>
                                    <span style='font-family: monospace; font-size: 24px; font-weight: bold; color: #b45309; letter-spacing: 3px;'>{voucherCode}</span>
                                </div>

                                <p style='margin: 8px 0 0 0; font-size: 15px; font-weight: bold; color: #b45309;'>{discountText}</p>
                                <p style='margin: 4px 0 0 0; font-size: 12px; color: #78350f;'>Valid until: <strong>{validityText}</strong></p>
                            </div>

                            <!-- CHI TIẾT BẢNG (EN) -->
                            <div style='margin: 15px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 12px; text-align: left;'>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold; width: 35%; background-color: #f8fafc; color: #475569;'>Discount Benefit</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{discountText}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold; background-color: #f8fafc; color: #475569;'>Validity Period</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #475569;'>{validityText}</td>
                                        </tr>
                                        {descriptionRowEn}
                                    </tbody>
                                </table>
                            </div>

                            <!-- BUTTON CTA (EN) -->
                            <div style='text-align: center; margin: 25px 0 15px 0;'>
                                <a href='{baseUrl}' style='display: inline-block; padding: 12px 28px; background-color: #0f172a; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 12px; border: 2px solid #000000;'>[ USE VOUCHER NOW ]</a>
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

        await _emailService.SendEmailAsync(toEmail, formattedSubject, bodyHtml, cancellationToken);
    }
}
