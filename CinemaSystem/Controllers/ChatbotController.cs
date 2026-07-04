using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Controllers;

/// <summary>
/// Chatbot HTTP entry point.
/// </summary>
/// <remarks>
/// Processing continues through <see cref="IChatbotService"/> to
/// <c>CinemaSystem.Infrastructure.Services.GeminiChatbotService</c>. That class
/// loads movie/showtime context through their Application interfaces and then
/// calls the configured Google Gemini endpoint. Chat history is not persisted
/// on the current main branch.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] ChatbotRequest request, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: IChatbotService được DI map sang GeminiChatbotService tại
        // CinemaSystem.Infrastructure/Services/GeminiChatbotService.cs.
        // Service tiếp tục gọi IMovieService/MovieService và
        // IShowtimeService/ShowtimeService lấy context, rồi mới gọi Google Gemini.
        var result = await _chatbotService.AskAsync(request, cancellationToken);

        // Gemini trả lời hoặc lỗi xong thì ServiceResult quay lại Controller;
        // nhánh main hiện không chuyển tiếp sang class lưu CHAT_HISTORY.
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.Message, result.ErrorCode ?? "ERROR"));
        }

        return StatusCode(result.StatusCode, ApiResponse<ChatbotResponse>.Ok(result.Data!));
    }
}
