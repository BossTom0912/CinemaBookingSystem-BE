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

public class GeminiModerationService : IAiModerationService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly GeminiSettings _settings;

    public GeminiModerationService(IOptions<GeminiSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<AiModerationResult> ModerateReviewAsync(int rating, string comment, CancellationToken cancellationToken = default)
    {
        // Nếu không có API Key hoặc bị lỗi cấu hình, trả về Flagged để Admin duyệt tay
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new AiModerationResult { Status = "FLAGGED", Reason = "MISSING_API_KEY" };
        }

        // ĐÂY LÀ PROMPT DÀNH CHO AI (VỪA KIỂM DUYỆT VỪA CSKH)
        string systemPrompt = @"Bạn là một hệ thống Trí tuệ Nhân tạo kiểm duyệt nội dung và Chăm sóc khách hàng cho rạp chiếu phim.
Nhiệm vụ của bạn là đọc bình luận và số sao đánh giá (từ 0 đến 5) của khách hàng.

BƯỚC 1: KIỂM DUYỆT (Moderation)
Phân loại nội dung thành 1 trong 3 trạng thái:
- APPROVED: Bình luận hợp lệ, lịch sự, khen hoặc chê phim một cách bình thường.
- REJECTED: Chứa từ ngữ thô tục, chửi thề (ví dụ: c*c, l*n, đ*m), xúc phạm, chứa link quảng cáo, hoặc lặp ký tự vô nghĩa (spam).
- FLAGGED: Nội dung không rõ ràng, nghi ngờ vi phạm nhưng không chắc chắn (cần con người duyệt lại).

BƯỚC 2: TRẢ LỜI TỰ ĐỘNG (Auto-Reply)
- Nếu APPROVED: Viết 1 câu trả lời thân thiện (tối đa 2 câu). Xưng 'Rạp' và gọi 'Bạn'. Khen thì cảm ơn, chê phim thì xoa dịu.
- Nếu REJECTED hoặc FLAGGED: Trả lời ngắn gọn: 'Nội dung của bạn vi phạm tiêu chuẩn cộng đồng hoặc đang chờ duyệt.'

YÊU CẦU ĐẦU RA BẮT BUỘC:
Chỉ trả về ĐÚNG 1 chuỗi JSON hợp lệ, không có markdown (```json), không giải thích thêm:
{
  ""status"": ""APPROVED"" | ""REJECTED"" | ""FLAGGED"",
  ""isSpam"": true | false,
  ""reason"": ""Lý do ngắn gọn nếu bị REJECTED/FLAGGED, rỗng nếu APPROVED"",
  ""moderatorMessage"": ""Câu trả lời tự động của rạp""
}";

        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = $"Đánh giá: {rating} sao. Nội dung: '{comment}'" } } }
            },
            // Ép Gemini trả về dạng JSON để code C# dễ Parse
            generationConfig = new { responseMimeType = "application/json" }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={_settings.ApiKey}";

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                // Báo lỗi API để ReviewService lưu status là Flagged
                return new AiModerationResult { Status = "FLAGGED", Reason = "AI_API_ERROR" };
            }

            var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

            // Lấy chuỗi JSON mà AI sinh ra
            var aiTextResponse = jsonDoc.GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString();

            if (string.IsNullOrWhiteSpace(aiTextResponse))
            {
                return new AiModerationResult { Status = "FLAGGED", Reason = "AI_EMPTY_RESPONSE" };
            }

            // Parse chuỗi JSON đó thành Object C#
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<AiModerationResult>(aiTextResponse, options);

            return result ?? new AiModerationResult { Status = "FLAGGED", Reason = "JSON_PARSE_ERROR" };
        }
        catch (Exception ex)
        {
            // Bắt mọi Exception (Mất mạng, JSON sai format...) và đưa về Flagged
            return new AiModerationResult { Status = "FLAGGED", Reason = $"EXCEPTION: {ex.Message}" };
        }
    }
}
