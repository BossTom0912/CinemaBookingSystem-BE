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

    public GeminiAiEmailService(IOptions<GeminiSettings> settings, IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task SendAiApologyEmailAsync(
        string toEmail, 
        string subject, 
        string reason, 
        string details, 
        CancellationToken cancellationToken)
    {
        var formattedSubject = subject.StartsWith("[CinemaSystem]") ? subject : $"[CinemaSystem] Thông báo dịch vụ - {subject}";
        
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
                            <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý khách hàng,</p>
                            
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
                            <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear Valued Customer,</p>
                            
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
        string? targetSeatType = null)
    {
        var formattedSubject = $"[CinemaSystem] Điều chỉnh giờ chiếu phim - Mã vé: #{bookingId}";

        var confirmAcceptUrl = $"http://localhost:5173/booking/confirm-time-change?bookingId={bookingId}&accept=true&token={token}";
        var confirmRefundUrl = $"http://localhost:5173/booking/confirm-time-change?bookingId={bookingId}&accept=false&token={token}";

        var compRows = "";
        if (!string.IsNullOrWhiteSpace(compensationVoucherCode))
        {
            compRows += $"""
                <tr>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Mã Voucher Đền Bù</td>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #d97706; font-family: monospace; font-weight: bold;' colspan='2'>[{compensationVoucherCode.Trim()}]</td>
                </tr>
                """;
        }
        if (!string.IsNullOrWhiteSpace(targetSeatType))
        {
            compRows += $"""
                <tr>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Nâng Hạng Ghế Miễn Phí</td>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #2563eb; font-weight: bold;' colspan='2'>Ưu tiên nâng hạng lên loại ghế [{targetSeatType.Trim()}]</td>
                </tr>
                """;
        }
        if (!string.IsNullOrWhiteSpace(compensationNote))
        {
            compRows += $"""
                <tr>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Quyền Lợi Đền Bù Kèm Theo</td>
                    <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #059669; font-weight: bold;' colspan='2'>{compensationNote.Trim()}</td>
                </tr>
                """;
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
                            <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý khách hàng,</p>

                            <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                                Lời đầu tiên, CinemaSystem xin gửi lời cảm ơn chân thành vì Quý khách đã luôn tin tưởng và ủng hộ chúng tôi. Chúng tôi xin thông báo về việc điều chỉnh giờ chiếu cho bộ phim <strong>{movieTitle}</strong> trong đơn hàng của Quý khách.
                            </p>

                            <!-- BẢNG CHI TIẾT TIẾNG VIỆT -->
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
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0;' colspan='2'>{movieTitle}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Thời gian chiếu</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #dc2626; text-decoration: line-through;'>{oldTime}</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{newTime}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Mã đặt vé</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-family: monospace; font-weight: bold;' colspan='2'>#{bookingId}</td>
                                        </tr>
                                        {compRows}
                                    </tbody>
                                </table>
                            </div>

                            <p style='font-size: 14px; color: #0f172a; font-weight: bold; margin-top: 20px;'>Vui lòng lựa chọn phương án trước {cutoffTime}:</p>
                            
                            <div style='margin: 15px 0; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 15px;'>
                                <p style='margin: 0 0 8px 0; font-size: 13px; font-weight: bold; color: #0f172a;'>Phương án 1: Đồng ý xem suất chiếu mới</p>
                                <p style='margin: 0 0 12px 0; font-size: 13px; color: #475569;'>Hệ thống sẽ giữ nguyên vé và tự động áp dụng ưu đãi đền bù (nếu có).</p>
                                <a href='{confirmAcceptUrl}' style='display: inline-block; padding: 12px 22px; background-color: #ffffff; color: #16a34a; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 13px; border: 2px solid #000000;'>XÁC NHẬN XEM SUẤT CHIẾU MỚI</a>
                            </div>

                            <div style='margin: 15px 0; background-color: #fef2f2; border: 1px solid #fca5a5; border-radius: 8px; padding: 15px;'>
                                <p style='margin: 0 0 8px 0; font-size: 13px; font-weight: bold; color: #b91c1c;'>Phương án 2: Yêu cầu hoàn tiền 100%</p>
                                <p style='margin: 0 0 12px 0; font-size: 13px; color: #7f1d1d;'>Nếu khung giờ mới không phù hợp, Quý khách có thể yêu cầu hoàn 100% tiền vé.</p>
                                <a href='{confirmRefundUrl}' style='display: inline-block; padding: 12px 22px; background-color: #ef4444; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 13px; border: 2px solid #991b1b;'>YÊU CẦU HOÀN TIỀN 100%</a>
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
                                We are writing to inform you about a showtime schedule change for <strong>{movieTitle}</strong> (Booking ID: #{bookingId}).
                            </p>

                            <!-- ENGLISH TABLE -->
                            <div style='margin: 15px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 12px; text-align: left;'>
                                    <thead>
                                        <tr style='background-color: #f8fafc; color: #475569;'>
                                            <th style='padding: 8px 12px; border-bottom: 1px solid #cbd5e1; width: 35%;'>Details</th>
                                            <th style='padding: 8px 12px; border-bottom: 1px solid #cbd5e1;'>Previous Time</th>
                                            <th style='padding: 8px 12px; border-bottom: 1px solid #cbd5e1;'>New Time</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Showtime</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #dc2626; text-decoration: line-through;'>{oldTime}</td>
                                            <td style='padding: 8px 12px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{newTime}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <p style='font-size: 12px; color: #64748b; margin-bottom: 10px;'>
                                Please select your preference before <strong>{cutoffTime}</strong> using the buttons above.
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
        string? targetSeatType = null)
    {
        var formattedSubject = $"[CinemaSystem] Thông báo điều chỉnh phòng chiếu - Mã vé: #{bookingId}";

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
                            <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý khách hàng,</p>

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
                            <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear Valued Customer,</p>
                            <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                                We regret to inform you that your screening room for <strong>{movieTitle}</strong> has been changed from {oldRoomName} to {newRoomName}.
                            </p>

                            <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; padding: 12px; border-radius: 8px; margin: 15px 0;'>
                                <p style='margin: 0; font-size: 12px; color: #475569;'>
                                    <strong>Important Note:</strong> Your showtime remains unchanged at <strong>{timeStr}</strong>. Your ticket and QR code have been automatically updated for the new auditorium.
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

        await _emailService.SendEmailAsync(toEmail, formattedSubject, bodyHtml, cancellationToken);
    }
}
