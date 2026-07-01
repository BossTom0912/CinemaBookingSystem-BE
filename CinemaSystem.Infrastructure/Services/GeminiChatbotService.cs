using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Orchestrator chatbot: gom dữ liệu phim/suất chiếu và gọi Gemini sinh câu trả lời.
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

    // Khởi tạo GeminiChatbotService thông qua Dependency Injection
    public GeminiChatbotService(
        IMovieService movieService,
        IShowtimeService showtimeService,
        IOptions<GeminiSettings> settings,
        CinemaSystem.Infrastructure.Persistence.CinemaDbContext dbContext)
    {
        // Gán service xử lý phim
        _movieService = movieService;
        // Gán service xử lý lịch chiếu
        _showtimeService = showtimeService;
        // Lấy giá trị cấu hình Gemini từ Options
        _settings = settings.Value;
        // Gán DbContext
        _dbContext = dbContext;
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

        // Truy vấn danh sách các bộ phim từ cơ sở dữ liệu (tối đa 100 phim)
        var moviesResult = await _movieService.GetMoviesAsync(null, 1, 100, null, false, cancellationToken);
        // Truy vấn danh sách lịch chiếu hiện tại từ cơ sở dữ liệu
        var showtimesResult = await _showtimeService.GetShowtimesAsync(cancellationToken);

        // Khởi tạo chuỗi động (StringBuilder) để xây dựng bối cảnh (context) cho AI
        var contextBuilder = new StringBuilder("Movie Theater Context:\n");
        // Kiểm tra xem việc lấy dữ liệu phim có thành công và có dữ liệu hay không
        if (moviesResult.Success && moviesResult.Data != null)
        {
            // Bổ sung tiêu đề thông báo danh sách phim hiện có vào ngữ cảnh
            contextBuilder.AppendLine("Available Movies:");
            // Duyệt qua từng bộ phim trong kết quả trả về
            foreach (var m in moviesResult.Data.Items)
            {
                // Xử lý nối danh sách các thể loại phim thành một chuỗi
                var genresStr = m.Genres != null ? string.Join(", ", m.Genres) : "";
                // Thêm chi tiết về tên phim, thể loại, thời lượng, đánh giá và thông tin nổi bật vào ngữ cảnh
                contextBuilder.AppendLine($"- {m.MovieNameVn} (Genre: {genresStr}, Duration: {m.Duration}m, Avg Rating: {m.AvgRating}, Highlight: {m.Highlight})");
            }
        }
        
        // Kiểm tra xem việc lấy lịch chiếu có thành công và có dữ liệu hay không
        if (showtimesResult.Success && showtimesResult.Data != null)
        {
            // Bổ sung tiêu đề thông báo danh sách lịch chiếu vào ngữ cảnh
            contextBuilder.AppendLine("Available Showtimes:");
            // Duyệt qua từng lịch chiếu có sẵn
            foreach (var s in showtimesResult.Data)
            {
                // Bổ sung chi tiết một lịch chiếu: Phim, Thời gian, Rạp, Phòng chiếu, Giá và trạng thái vào ngữ cảnh
                contextBuilder.AppendLine($"- Movie: {s.MovieTitle}, Time: {s.StartTime:yyyy-MM-dd HH:mm} to {s.EndTime:HH:mm}, Cinema: {s.CinemaName}, Room: {s.RoomName}, Price: {s.BasePrice}, Status: {s.Status}");
            }
        }

        // Định nghĩa câu lệnh hệ thống (System Prompt) hướng dẫn AI xử lý tin nhắn
        string systemPrompt = $@"You are a helpful customer support AI for our movie theater system.
        Use the following context to answer the user's questions about movies and showtimes.
        If you don't know the answer or the information is not in the context, politely say you don't have that information.
        Keep your answers concise, friendly, and formatted nicely.

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
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={_settings.ApiKey}";

        // Thực hiện gửi yêu cầu POST bất đồng bộ tới Gemini API
        var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        // Kiểm tra nếu API phản hồi lỗi (không thành công)
        if (!response.IsSuccessStatusCode)
        {
            // Đọc chi tiết nội dung lỗi được trả về từ API
            var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
            // Trả kết quả thất bại và kèm theo mã trạng thái và mô tả
            return ServiceResult<ChatbotResponse>.Fail(500, $"AI API error: {response.StatusCode} - {errorDetails}", "AI_API_ERROR");
        }

        // Chuyển đổi phản hồi của AI (Json) thành đối tượng JsonElement
        var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        // Gán tin nhắn trả lời mặc định trong trường hợp lỗi xử lý
        var replyText = "I'm sorry, I couldn't generate a response.";
        // Bắt đầu xử lý giải nén chuỗi JSON phản hồi
        try
        {
            // Trích xuất nội dung văn bản AI sinh ra thông qua cấu trúc JSON
            replyText = jsonDoc.GetProperty("candidates")[0]
                               .GetProperty("content")
                               .GetProperty("parts")[0]
                               .GetProperty("text").GetString();
        }
        // Bắt mọi lỗi xảy ra khi truy cập thuộc tính JSON để giữ lại thông báo mặc định
        catch { }

        // Khởi tạo một thực thể lịch sử cuộc hội thoại
        var history = new CinemaSystem.Domain.Entities.ChatHistory
        {
            // Khởi tạo chuỗi định danh duy nhất (ID)
            ChatHistoryId = Guid.NewGuid().ToString(),
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
