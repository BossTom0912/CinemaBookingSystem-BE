using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using CinemaSystem.Application.Common;
namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Adapter gọi Gemini để phân loại nội dung review APPROVED/FLAGGED/REJECTED.
/// </summary>
/// <remarks>
/// Được <c>ReviewService</c> tại
/// <c>CinemaSystem.Infrastructure/Services/ReviewService.cs</c> gọi. Class này
/// chỉ giao tiếp Gemini và trả <see cref="AiModerationResult"/>; ReviewService
/// mới là nơi quyết định ghi REVIEW và moderation history.
/// </remarks>
public class GeminiModerationService : IAiModerationService
{
    // Khởi tạo HttpClient tĩnh dùng chung cho toàn bộ ứng dụng
    private static readonly HttpClient _httpClient = new HttpClient();
    // Khai báo đối tượng chứa các cấu hình về Gemini API
    private readonly GeminiSettings _settings;

    // Khởi tạo GeminiModerationService thông qua Dependency Injection
    public GeminiModerationService(IOptions<GeminiSettings> settings)
    {
        // Lấy giá trị cấu hình Gemini từ Options
        _settings = settings.Value;
    }

    // Hàm gọi AI kiểm duyệt nội dung đánh giá và tự động phản hồi
    public async Task<AiModerationResult> ModerateReviewAsync(int rating, string comment, CancellationToken cancellationToken = default)
    {
        // Kiểm tra xem API Key có bị trống hoặc thiết lập không hợp lệ không
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            // Nếu không có API Key, trả về trạng thái cờ báo (Flagged) để Admin tự kiểm duyệt
            return new AiModerationResult { Status = ReviewConstants.Flagged, Reason = "MISSING_API_KEY" };
        }

        // Định nghĩa câu lệnh hướng dẫn (Prompt) dành riêng cho nhiệm vụ của AI
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

        // Xây dựng một gói payload định dạng ẩn danh chứa chỉ dẫn gửi tới API
        var payload = new
        {
            // Chỉ định cấu trúc system_instruction để chứa Prompt
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            // Truyền nội dung đầu vào mà AI cần xử lý
            contents = new[]
            {
                // Khởi tạo thông tin dạng tin nhắn của người dùng kèm số điểm và bình luận
                new { role = "user", parts = new[] { new { text = $"Đánh giá: {rating} sao. Nội dung: '{comment}'" } } }
            },
            // Chỉ định cấu hình tạo sinh với yêu cầu ép trả ra định dạng JSON thuần
            generationConfig = new { responseMimeType = "application/json" }
        };

        // Gắn API Key vào chuỗi định tuyến để gọi tới mô hình Gemini-3.1-flash-lite
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={_settings.ApiKey}";

        // Bắt đầu một khối lệnh thực thi an toàn để theo dõi và xử lý lỗi
        try
        {
            // Thực thi yêu cầu POST qua mạng tới hệ thống Google AI
            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

            // Kiểm tra HTTP Status xem quá trình xử lý có thất bại hay không
            if (!response.IsSuccessStatusCode)
            {
                // Đọc thông báo phản hồi khi có lỗi từ server Gemini
                var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                // Trả về kết quả đánh giá là Flagged cho hệ thống kèm nguyên nhân AI_API_ERROR
                return new AiModerationResult { Status = ReviewConstants.Flagged, Reason = "AI_API_ERROR" };
            }

            // Chuyển đổi dữ liệu JSON từ AI phản hồi về dạng đối tượng JsonElement
            var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            
            // Xử lý chuỗi JSON lấy chính xác nội dung câu trả lời do AI khởi tạo
            var aiTextResponse = jsonDoc.GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString();

            // Đảm bảo kết quả lấy được không phải rỗng hay trống
            if (string.IsNullOrWhiteSpace(aiTextResponse))
            {
                // Trả về cờ Flagged nếu kết quả trả về của AI bất hợp lệ
                return new AiModerationResult { Status = ReviewConstants.Flagged, Reason = "AI_EMPTY_RESPONSE" };
            }

            // Tạo các tùy chọn dùng khi phân tích cú pháp JSON để bỏ qua chữ hoa thường
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            // Chuyển đổi chuỗi trả lời (dạng JSON) thành một đối tượng AiModerationResult của C#
            var result = JsonSerializer.Deserialize<AiModerationResult>(aiTextResponse, options);

            // Nếu phân tích thành công thì trả về object, nếu null thì trả về lỗi Flagged
            return result ?? new AiModerationResult { Status = ReviewConstants.Flagged, Reason = "JSON_PARSE_ERROR" };
        }
        // Theo dõi những trường hợp ngoại lệ liên quan tới mạng lưới hoặc JSON bị lỗi cấu trúc
        catch (Exception ex)
        {
            // Trả về trạng thái bị cắm cờ để admin xử lý kèm ghi lại chi tiết lỗi
            return new AiModerationResult { Status = ReviewConstants.Flagged, Reason = $"EXCEPTION: {ex.Message}" };
        }
    }
}
