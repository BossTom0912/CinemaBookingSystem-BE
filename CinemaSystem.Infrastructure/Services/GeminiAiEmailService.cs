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
        // Default apology email body in case Gemini fails
        var fallbackBody = $"""
            [VI] Kính gửi Quý khách,
            Chúng tôi thành thật xin lỗi vì sự cố phát sinh từ rạp chiếu phim: {reason}. Chi tiết: {details}.
            Để đền bù cho sự bất tiện này, chúng tôi xin đề xuất các phương án giải quyết:
            - Lựa chọn 1: Chuyển sang một suất chiếu mới bất kỳ theo ý muốn của bạn (Quý khách sẽ được giảm giá hoặc nâng hạng ghế miễn phí nếu có).
            - Lựa chọn 2: Nhận hoàn tiền đầy đủ (hệ thống đang tiến hành xử lý hoàn trả).
            Xin chân thành cảm ơn sự thông cảm của Quý khách.

            [EN] Dear Valued Customer,
            We sincerely apologize for the theater issue: {reason}. Details: {details}.
            To compensate for this inconvenience, we would like to offer the following options:
            - Option 1: Transfer to a new showtime of your choice (with a discount coupon or a complimentary seat upgrade if available).
            - Option 2: Receive a full refund (already being processed by the system).
            Thank you for your understanding.
            """;

        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            await _emailService.SendEmailAsync(toEmail, subject, fallbackBody, cancellationToken);
            return;
        }

        try
        {
            var prompt = $"""
                Write a formal, polite, and empathetic bilingual apology email in both English and Vietnamese for a cinema booking issue.
                Reason for apology: {reason}
                Details/Changes: {details}
                Requirements:
                1. Make sure it has a professional tone, apologizes for the inconvenience caused by the cinema.
                2. Clearly propose these two resolution options for the customer to choose:
                   - Option A: Transfer to a new showtime of their choice, with a discount coupon code or a complimentary seat upgrade.
                   - Option B: Propose a full refund (already being processed by the system).
                3. Explicitly structure it with clear [VI] (Vietnamese) and [EN] (English) sections.
                4. Do not include any Markdown wrappers (like ```html or ```text), subject line, or headers in the output. Just return the email body.
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
                    await _emailService.SendEmailAsync(toEmail, subject, aiReply.Trim(), cancellationToken);
                    return;
                }
            }
        }
        catch
        {
            // Ignore AI exception and send fallback
        }

        // Send fallback email if AI fails
        await _emailService.SendEmailAsync(toEmail, subject, fallbackBody, cancellationToken);
    }
}
