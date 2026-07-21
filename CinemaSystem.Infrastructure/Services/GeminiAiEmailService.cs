using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Services;

public class GeminiAiEmailService : IAiEmailService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly GeminiSettings _settings;
    private readonly IEmailService _emailService;

    public GeminiAiEmailService(IOptions<GeminiSettings> settings, IEmailService emailService)
    {
        _settings = settings?.Value ?? new GeminiSettings();
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
        var fallbackBody = $"""
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
                        <p style='color: #94a3b8; margin: 5px 0 0 0; font-size: 13px;'>Hệ thống Rạp chiếu phim Hiện đại & Đẳng cấp</p>
                    </div>

                    <!-- BODY CONTENT -->
                    <div style='padding: 30px;'>
                        <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý khách hàng,</p>

                        <!-- 1. LỜI MỞ ĐẦU & TÓM TẮT SỰ CỐ -->
                        <div style='background-color: #f1f5f9; border-left: 4px solid #3b82f6; padding: 15px; border-radius: 0 8px 8px 0; margin-bottom: 25px;'>
                            <p style='margin: 0; font-size: 14px; color: #334155;'>
                                Lời đầu tiên, chúng tôi xin chân thành cảm ơn Quý khách đã lựa chọn dịch vụ của <strong>CinemaSystem</strong>. Chúng tôi rất tiếc phải thông báo về việc <strong>{reason}</strong> liên quan đến đơn hàng xem phim của Quý khách.
                            </p>
                            <p style='margin: 8px 0 0 0; font-size: 13px; color: #64748b;'>
                                Chi tiết thông báo: {details}
                            </p>
                        </div>

                        <!-- 2. PHƯƠNG ÁN XỬ LÝ & QUYỀN LỢI -->
                        <p style='font-size: 14px; color: #334155; font-weight: bold;'>Phương án hỗ trợ dành cho Quý khách:</p>
                        <ul style='font-size: 13px; color: #334155; padding-left: 20px; line-height: 1.8;'>
                            <li><strong>Phương án 1: Tiếp tục xem suất chiếu</strong> - Vé của Quý khách vẫn hoàn toàn hợp lệ cho suất chiếu/phòng chiếu mới kèm các ưu đãi đền bù (nếu có). Quý khách không cần thao tác gì thêm.</li>
                            <li><strong>Phương án 2: Đổi suất chiếu khác / Hoàn tiền 100%</strong> - Nếu khung giờ mới không phù hợp, Quý khách có thể yêu cầu đổi suất chiếu khác hoặc nhận hoàn trả 100% tiền vé trước giờ chiếu.</li>
                        </ul>

                        <!-- 3. GHI CHÚ VÀ LIÊN HỆ -->
                        <div style='background-color: #fffbe6; border: 1px solid #ffe58f; padding: 12px 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='margin: 0; font-size: 12px; color: #d4b106; font-weight: bold;'>
                                Nếu cần hỗ trợ trực tiếp, Quý khách vui lòng liên hệ bộ phận CSKH qua Hotline 1900 6868 hoặc phản hồi email này.
                            </p>
                        </div>

                        <p style='font-size: 13px; color: #475569;'>Một lần nữa, chúng tôi chân thành xin lỗi vì sự bất tiện này và rất mong nhận được sự thông cảm từ Quý khách.</p>
                    </div>

                    <!-- FOOTER -->
                    <div style='background-color: #f1f5f9; padding: 20px 30px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                        <p style='margin: 0 0 5px 0; font-weight: bold; color: #0f172a;'>Đội ngũ Vận hành & CSKH CinemaSystem</p>
                        <p style='margin: 0 0 5px 0;'>Hotline: 1900 6868 | Email: cskh@cinemasystem.vn | Fanpage: fb.com/cinemasystem</p>
                        <p style='margin: 0;'>Địa chỉ: 123 Đường Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh | Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
                    </div>
                </div>
            </body>
            </html>
            """;

        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            await _emailService.SendEmailAsync(toEmail, formattedSubject, fallbackBody, cancellationToken);
            return;
        }

        try
        {
            var prompt = $"""
                Write a formal, polite, and highly professional HTML apology email in Vietnamese for a cinema booking change.
                Reason: {reason}
                Details: {details}
                Requirements:
                1. Professional structure with Header, Greeting to Customer, Reason, Benefits, and CSKH Footer.
                2. Explicitly state the Cinema Name: CinemaSystem, Hotline: 1900 6868, Email: cskh@cinemasystem.vn.
                3. Do not include any Markdown wrappers, subject line, or plain text wrappers. Return complete HTML.
                """;

            var payload = new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={_settings.ApiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var aiReply = jsonDoc.GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString();

                if (!string.IsNullOrWhiteSpace(aiReply))
                {
                    await _emailService.SendEmailAsync(toEmail, formattedSubject, aiReply.Trim(), cancellationToken);
                    return;
                }
            }
        }
        catch
        {
            // Ignore AI exception and send fallback
        }

        await _emailService.SendEmailAsync(toEmail, formattedSubject, fallbackBody, cancellationToken);
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

        var confirmAcceptUrl = $"http://localhost:5173/api/booking/{bookingId}/confirm-time-change?accept=true&token={token}";
        var confirmRefundUrl = $"http://localhost:5173/api/booking/{bookingId}/confirm-time-change?accept=false&token={token}";

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

        var fallbackBody = $"""
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
                        <p style='color: #94a3b8; margin: 5px 0 0 0; font-size: 13px;'>Hệ thống Rạp chiếu phim Hiện đại & Đẳng cấp</p>
                    </div>

                    <!-- BODY CONTENT -->
                    <div style='padding: 30px;'>
                        <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi Quý khách hàng,</p>

                        <!-- 1. LỜI MỞ ĐẦU & TÓM TẮT SỰ CỐ -->
                        <p style='font-size: 14px; color: #334155; margin-bottom: 20px;'>
                            Lời đầu tiên, chúng tôi xin chân thành cảm ơn Quý khách đã lựa chọn dịch vụ của <strong>CinemaSystem</strong>. Chúng tôi rất tiếc phải thông báo về việc điều chỉnh lịch chiếu đối với bộ phim <strong>{movieTitle}</strong> trong đơn hàng của Quý khách.
                        </p>

                        <!-- 2. CHI TIẾT THÔNG TIN (BẢNG SO SÁNH) -->
                        <div style='margin: 20px 0;'>
                            <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 13px; text-align: left;'>
                                <thead>
                                    <tr style='background-color: #f1f5f9; color: #0f172a;'>
                                        <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1;'>Thông tin</th>
                                        <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1;'>Trạng thái Ban đầu</th>
                                        <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1;'>Trạng thái Mới</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr>
                                        <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Phim</td>
                                        <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0;' colspan='2'>{movieTitle}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Suất chiếu</td>
                                        <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #ef4444; font-weight: bold;'>{oldTime}</td>
                                        <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #10b981; font-weight: bold;'>{newTime}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Mã đặt vé</td>
                                        <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-family: monospace; font-weight: bold;' colspan='2'>#{bookingId}</td>
                                    </tr>
                                    {compRows}
                                </tbody>
                            </table>
                        </div>

                        <!-- 3. PHƯƠNG ÁN XỬ LÝ & QUYỀN LỢI KHÁCH HÀNG -->
                        <p style='font-size: 14px; color: #0f172a; font-weight: bold; margin-top: 25px;'>Để đảm bảo trải nghiệm tốt nhất, vui lòng lựa chọn phương án trước <strong>{cutoffTime}</strong> (Hạn chót):</p>
                        
                        <div style='margin: 15px 0 25px 0; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 15px;'>
                            <p style='margin: 0 0 8px 0; font-size: 13px; font-weight: bold; color: #0f172a;'>Phương án 1: Đồng ý xem suất chiếu mới</p>
                            <p style='margin: 0 0 12px 0; font-size: 13px; color: #475569;'>Bấm nút xác nhận bên dưới. Chúng tôi sẽ giữ nguyên vị trí ghế (hoặc ưu tiên nâng hạng ghế) và tặng kèm E-Voucher đền bù vào tài khoản của Quý khách.</p>
                            <a href='{confirmAcceptUrl}' style='display: inline-block; padding: 12px 22px; background-color: #ffffff; color: #16a34a; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 13px; border: 2px solid #000000;'>XÁC NHẬN XEM SUẤT CHIẾU MỚI (TẶNG VOUCHER)</a>
                        </div>

                        <div style='margin: 15px 0 25px 0; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 15px;'>
                            <p style='margin: 0 0 8px 0; font-size: 13px; font-weight: bold; color: #0f172a;'>Phương án 2: Đổi suất chiếu khác / Hoàn tiền 100%</p>
                            <p style='margin: 0 0 12px 0; font-size: 13px; color: #475569;'>Nếu khung giờ mới không thuận tiện, Quý khách có thể đổi suất chiếu khác hoặc nhận lại 100% tiền vé tự động.</p>
                            <a href='{confirmRefundUrl}' style='display: inline-block; padding: 12px 22px; background-color: #ffffff; color: #ea580c; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 13px; border: 2px solid #000000;'>ĐỔI SUẤT CHIẾU KHÁC HOẶC HOÀN TIỀN 100%</a>
                        </div>

                        <!-- 4. LƯU Ý VÀ THÔNG TIN LIÊN HỆ -->
                        <div style='background-color: #fffbe6; border: 1px solid #ffe58f; padding: 12px 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='margin: 0; font-size: 12px; color: #856404; font-weight: bold;'>
                                Lưu ý quan trọng: Nếu sau {cutoffTime} Quý khách chưa đưa ra lựa chọn, hệ thống sẽ tự động hủy vé và hoàn tiền 100% vào tài khoản của Quý khách để đảm bảo quyền lợi.
                            </p>
                        </div>

                        <p style='font-size: 13px; color: #475569;'>Nếu cần hỗ trợ thêm, Quý khách vui lòng liên hệ CSKH qua Hotline 1900 6868 hoặc phản hồi trực tiếp email này.</p>
                    </div>

                    <!-- FOOTER -->
                    <div style='background-color: #f1f5f9; padding: 20px 30px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                        <p style='margin: 0 0 5px 0; font-weight: bold; color: #0f172a;'>Đội ngũ Vận hành & CSKH CinemaSystem</p>
                        <p style='margin: 0 0 5px 0;'>Hotline: 1900 6868 | Email: cskh@cinemasystem.vn | Fanpage: fb.com/cinemasystem</p>
                        <p style='margin: 0;'>Địa chỉ: 123 Đường Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh | Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
                    </div>
                </div>
            </body>
            </html>
            """;

        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            await _emailService.SendEmailAsync(toEmail, formattedSubject, fallbackBody, cancellationToken);
            return;
        }

        try
        {
            var prompt = $"""
                Write a formal, polite, and empathetic bilingual apology email in HTML format for a cinema showtime change.
                Movie: {movieTitle}
                Booking ID: {bookingId}
                OLD Showtime: {oldTime}
                NEW Showtime: {newTime}
                Confirmation Deadline / Cut-off Time: {cutoffTime}
                Compensation Info: Voucher: {compensationVoucherCode}, Seat Upgrade: {targetSeatType}, Note: {compensationNote}
                Accept CTA URL: {confirmAcceptUrl}
                Refund CTA URL: {confirmRefundUrl}
                Requirements:
                1. Include a clean HTML Comparison Table for OLD vs NEW showtimes, including any compensation info.
                2. Provide 2 clear Call-To-Action buttons with the EXACT URLs provided above.
                   CRITICAL BUTTON STYLING REQUIREMENT:
                   - Accept / Confirm Button MUST use style: background-color: #ffffff; border: 2px solid #000000; color: #16a34a; font-weight: bold; (White background, Black border, Green text).
                   - Cancel / Refund Button MUST use style: background-color: #ffffff; border: 2px solid #000000; color: #ea580c; font-weight: bold; (White background, Black border, Orange text).
                3. Include the Fallback 100% refund rule after deadline {cutoffTime}.
                4. Cinema Name: CinemaSystem, Hotline: 1900 6868, Email: cskh@cinemasystem.vn.
                5. Do not include any Markdown wrappers. Return pure HTML.
                """;

            var payload = new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={_settings.ApiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var aiReply = jsonDoc.GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString();

                if (!string.IsNullOrWhiteSpace(aiReply))
                {
                    await _emailService.SendEmailAsync(toEmail, formattedSubject, aiReply.Trim(), cancellationToken);
                    return;
                }
            }
        }
        catch
        {
            // Ignore AI exception and send fallback
        }

        await _emailService.SendEmailAsync(toEmail, formattedSubject, fallbackBody, cancellationToken);
    }
}
