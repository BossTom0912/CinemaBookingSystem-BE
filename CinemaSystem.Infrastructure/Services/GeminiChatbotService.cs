using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Contracts.Common;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Orchestrator chatbot: gom dữ liệu phim, suất chiếu, rạp chiếu, voucher và gọi Gemini sinh câu trả lời.
/// </summary>
/// <remarks>
/// Nhận request từ <c>CinemaSystem/Controllers/ChatbotController.cs</c>; gọi
/// <see cref="IMovieService"/>, <see cref="IShowtimeService"/>, Gemini HTTP API
/// và ghi CHAT_HISTORY bằng CinemaDbContext. Kết quả quay về ChatbotController.
/// </remarks>
public class GeminiChatbotService : IChatbotService
{
    // Khởi tạo HttpClient tĩnh dùng chung cho toàn bộ ứng dụng
    private static readonly HttpClient _httpClient = new HttpClient();
    // Khai báo service xử lý nghiệp vụ phim
    private readonly IMovieService _movieService;
    // Khai báo service xử lý nghiệp vụ lịch chiếu
    private readonly IShowtimeService _showtimeService;
    // Khai báo cấu hình dành cho Gemini API
    private readonly GeminiSettings _settings;
    // Khai báo DbContext để tương tác với cơ sở dữ liệu
    private readonly CinemaSystem.Infrastructure.Persistence.CinemaDbContext _dbContext;
    // Khai báo IClock để lấy thời gian
    private readonly IClock _clock;

    // Khởi tạo GeminiChatbotService thông qua Dependency Injection
    public GeminiChatbotService(
        IMovieService movieService,
        IShowtimeService showtimeService,
        IOptions<GeminiSettings> settings,
        CinemaSystem.Infrastructure.Persistence.CinemaDbContext dbContext,
        IClock clock)
    {
        // Gán service xử lý phim
        _movieService = movieService;
        // Gán service xử lý lịch chiếu
        _showtimeService = showtimeService;
        // Lấy giá trị cấu hình Gemini từ Options
        _settings = settings.Value;
        // Gán DbContext
        _dbContext = dbContext;
        // Gán Clock
        _clock = clock;
    }

    // Hàm gọi AI xử lý tin nhắn của người dùng và trả về phản hồi
    public async Task<ServiceResult<ChatbotResponse>> AskAsync(ChatbotRequest request, CancellationToken cancellationToken)
    {
        // Kiểm tra xem API Key của Gemini đã được cấu hình chưa
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            // Trả về lỗi nếu thiếu API Key
            return ServiceResult<ChatbotResponse>.Fail(500, "Gemini API key is not configured.", "MISSING_API_KEY");
        }

        // Truy vấn danh sách cụm rạp chiếu phim đang hoạt động
        var cinemas = await _dbContext.Cinemas
            .AsNoTracking()
            .Where(c => c.CinemaStatus == DomainConstants.CinemaStatus.Active)
            .ToListAsync(cancellationToken);

        // Truy vấn danh sách voucher đang hoạt động và còn thời hạn sử dụng
        var now = _clock.UtcNow;
        var vouchers = await _dbContext.Vouchers
            .AsNoTracking()
            .Where(v => v.VoucherStatus == DomainConstants.VoucherStatus.Active
                && v.StartDate <= now
                && v.EndDate >= now
                && v.UsedCount < v.UsageLimit)
            .ToListAsync(cancellationToken);

        // Truy vấn danh sách các bộ phim từ cơ sở dữ liệu (tối đa theo cấu hình)
        var moviesResult = await _movieService.GetMoviesAsync(
            null,
            PaginationDefaults.FirstPageIndex,
            _settings.ContextMovieLimit,
            null,
            false,
            cancellationToken);

        // Truy vấn danh sách lịch chiếu hiện tại từ cơ sở dữ liệu
        var showtimesResult = await _showtimeService.GetShowtimesAsync(cancellationToken);

        // Khởi tạo chuỗi động (StringBuilder) để xây dựng bối cảnh (context) cho AI
        var contextBuilder = new StringBuilder("Movie Theater System Context:\n");

        // 1. Thông tin rạp chiếu (Địa chỉ và Hotline)
        contextBuilder.AppendLine("\nAvailable Cinemas (Locations, Addresses & Contact Hotlines):");
        if (cinemas != null && cinemas.Any())
        {
            foreach (var c in cinemas)
            {
                contextBuilder.AppendLine($"- {c.CinemaName}: Address: {c.Address}, City: {c.City}, Phone: {c.PhoneNumber ?? "N/A"}");
            }
        }
        else
        {
            contextBuilder.AppendLine("- No active cinemas available at the moment.");
        }

        // 2. Thông tin Voucher hiện có
        contextBuilder.AppendLine("\nAvailable Active Vouchers & Promotions:");
        if (vouchers != null && vouchers.Any())
        {
            foreach (var v in vouchers)
            {
                var discountStr = v.DiscountType == DomainConstants.DiscountType.Percent ? $"{v.DiscountValue}%" : $"{v.DiscountValue:N0} VND";
                var minOrderStr = v.MinOrderAmount.HasValue ? $"{v.MinOrderAmount.Value:N0} VND" : "No minimum";
                contextBuilder.AppendLine($"- Code: {v.VoucherCode} | Title: {v.Title} | Discount: {discountStr} | Description: {v.Description} | Min Order: {minOrderStr} | Expiry Date: {v.EndDate:yyyy-MM-dd HH:mm}");
            }
        }
        else
        {
            contextBuilder.AppendLine("- No active global vouchers found right now. However, customers can check their personal Wallet or register a new account to receive a welcome voucher.");
        }

        // 3. Thông tin phim đang chiếu
        contextBuilder.AppendLine("\nAvailable Movies:");
        if (moviesResult.Success && moviesResult.Data != null && moviesResult.Data.Items != null)
        {
            foreach (var m in moviesResult.Data.Items)
            {
                var genresStr = m.Genres != null ? string.Join(", ", m.Genres) : "";
                contextBuilder.AppendLine($"- {m.MovieNameVn} (Genre: {genresStr}, Duration: {m.Duration}m, Avg Rating: {m.AvgRating}, Highlight: {m.Highlight})");
            }
        }
        else
        {
            contextBuilder.AppendLine("- No movies are currently available.");
        }
        
        // 4. Thông tin lịch chiếu
        contextBuilder.AppendLine("\nAvailable Showtimes:");
        if (showtimesResult.Success && showtimesResult.Data != null)
        {
            foreach (var s in showtimesResult.Data)
            {
                contextBuilder.AppendLine($"- Movie: {s.MovieTitle}, Time: {s.StartTime:yyyy-MM-dd HH:mm} to {s.EndTime:HH:mm}, Cinema: {s.CinemaName}, Room: {s.RoomName}, Price: {s.BasePrice:N0} VND, Status: {s.Status}");
            }
        }
        else
        {
            contextBuilder.AppendLine("- No showtimes are scheduled at this time.");
        }

        // Định nghĩa câu lệnh hệ thống (System Prompt) hướng dẫn AI xử lý tin nhắn
        string systemPrompt = $@"You are 'CinemaBot', a helpful, friendly, and professional customer support AI for our movie theater system (CinemaSystem).
        Your mission is to guide users on booking tickets, active vouchers, how to get vouchers, showtimes, and cinema locations (addresses & hotlines).
        Use the following database context to answer the user's questions. 
        If the user asks for information that is not in the context, politely say you don't have that information.

        GUIDELINES FOR YOUR ANSWERS:
        1. Guiding Ticket Booking (Hướng dẫn đặt vé):
           Explain the 6-step online booking process clearly and step-by-step:
           - Bước 1: Đăng nhập/Đăng ký tài khoản thành viên trên website hoặc ứng dụng di động CinemaSystem để tích lũy điểm thưởng và sử dụng voucher.
           - Bước 2: Chọn phim, rạp chiếu gần bạn nhất và suất chiếu (khung giờ) phù hợp.
           - Bước 3: Lựa chọn vị trí ghế ngồi (Ghế Thường, Ghế VIP hoặc Ghế đôi Sweetbox) trên sơ đồ phòng chiếu. (Lưu ý: Ghế được giữ tạm thời trong 5-10 phút).
           - Bước 4: Chọn thêm dịch vụ bắp nước (F&B) đi kèm nếu mong muốn.
           - Bước 5: Tại màn hình thanh toán, nhập mã voucher khả dụng (ví dụ: các mã voucher trong danh sách bên dưới) để được giảm giá đơn hàng.
           - Bước 6: Thanh toán trực tuyến quét mã QR qua cổng thanh toán SePay. Sau khi giao dịch thành công, mã QR vé điện tử sẽ gửi về email của bạn hoặc hiển thị trong phần lịch sử giao dịch. Bạn chỉ cần đưa mã QR này cho nhân viên soát vé để vào phòng chiếu mà không cần đổi vé giấy.

        2. Vouchers & Promotions (Nội dung voucher hiện có):
           List the active vouchers from the context dynamically. Emphasize their code, description, discount value, and minimum order requirements. If no vouchers are in the context, let them know they can register a new account to get a welcome voucher or check their personal Wallet.

        3. How to Get Vouchers (Cách nhận voucher):
           Guide the user with the following options:
           - Đăng ký tài khoản thành viên mới: Nhận ngay voucher chào mừng thành viên mới gửi trực tiếp vào ví voucher của họ.
           - Tích điểm thành viên (Reward Points): Mua vé thành công tích lũy điểm để đổi voucher ưu đãi tiếp theo.
           - Theo dõi các chương trình/minigame trên trang Fanpage chính thức của rạp để nhận giftcode khuyến mãi đặc biệt.

        4. Showtimes & Addresses:
           Always format showtimes and addresses beautifully in bullet points or markdown tables. Use the exact addresses and hotlines provided in the context.

        5. Language, Tone & Formatting Rules (Quy tắc Ngôn ngữ, Giọng điệu & Định dạng):
           - NEVER use markdown bold syntax (such as double asterisks '**') or stars in your responses. Keep the text clean, flat, and professional.
           - Keep answers extremely concise, clean, and to the point. Avoid fluff or wordy explanations.
           - Use simple lists with plain bullet points (like '-' or '•') or numbered lists for clean structure.
           - Always respond in Vietnamese (matching the user's query). Use a warm, polite, and customer-oriented tone. Use polite Vietnamese pronouns: 'Dạ', 'Em' (as CinemaBot), 'Anh/Chị' or 'Bạn' (for customers).

        Context:
        {contextBuilder.ToString()}";

        // Khởi tạo đối tượng payload JSON chứa thông tin gửi tới AI
        var payload = new
        {
            // Thiết lập chỉ dẫn cấu hình hệ thống
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            // Truyền nội dung tin nhắn của người dùng vào yêu cầu
            contents = new[]
            {
                // Định dạng vai trò người dùng (user) và text đi kèm
                new { role = "user", parts = new[] { new { text = request.Message } } }
            }
        };

        // Tạo đường dẫn gọi API của Gemini kèm theo Key
        var url =
            $"{_settings.ApiBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(_settings.Model)}:generateContent";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.TryAddWithoutValidation(
            GeminiSettings.ApiKeyHeaderName,
            _settings.ApiKey);

        // Thực hiện gửi yêu cầu POST bất đồng bộ tới Gemini API
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Kiểm tra nếu API phản hồi lỗi (không thành công)
        if (!response.IsSuccessStatusCode)
        {
            // Đọc chi tiết nội dung lỗi được trả về từ API
            var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
            // Trả kết quả thất bại và kèm theo mã trạng thái và mô tả
            return ServiceResult<ChatbotResponse>.Fail(500, $"AI API error: {response.StatusCode} - {errorDetails}", "AI_API_ERROR");
        }

        // Gán tin nhắn trả lời mặc định trong trường hợp lỗi xử lý
        var replyText = "I'm sorry, I couldn't generate a response.";
        // Bắt đầu xử lý giải nén chuỗi JSON phản hồi
        try
        {
            // Chuyển đổi phản hồi của AI (Json) thành đối tượng Json Element
            var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            // Trích xuất nội dung văn bản AI sinh ra thông qua cấu trúc JSON
            replyText = jsonDoc.GetProperty("candidates")[0]
                               .GetProperty("content")
                               .GetProperty("parts")[0]
                               .GetProperty("text").GetString();
        }
        // Bắt mọi lỗi xảy ra khi truy cập thuộc tính JSON hoặc lỗi phân tích cú pháp để giữ lại thông báo mặc định
        catch { }

        // Khởi tạo một thực thể lịch sử cuộc hội thoại
        var history = new CinemaSystem.Domain.Entities.ChatHistory
        {
            // Khởi tạo chuỗi định danh duy nhất (ID)
            ChatHistoryId =
                $"{DomainConstants.EntityIdPrefix.ChatHistory}_{Guid.NewGuid():N}",
            // Để trống định danh người dùng (hiện tại chưa dùng trong request)
            UserId = null, // Since ChatbotRequest doesn't currently supply UserId in this contract
            // Lưu giữ lại câu hỏi của người dùng
            UserMessage = request.Message,
            // Lưu giữ lại phản hồi của AI
            AiReplyMessage = replyText ?? string.Empty,
            // Ghi nhận thời điểm hiện tại chuẩn UTC
            CreatedAt = DateTime.UtcNow
        };
        // Thêm bản ghi lịch sử vào danh sách theo dõi của Entity Framework
        _dbContext.ChatHistories.Add(history);
        // Lưu thay đổi vào Cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Đóng gói và trả về câu trả lời cho Frontend
        return ServiceResult<ChatbotResponse>.Ok(new ChatbotResponse { Reply = replyText ?? string.Empty });
    }
}
