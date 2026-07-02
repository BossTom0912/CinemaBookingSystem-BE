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
/// calls the configured Google Gemini endpoint. The generated exchange is then
/// persisted to CHAT_HISTORY; the current request contract does not identify a
/// user, so the stored UserId is null.
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

        // Gemini trả lời xong, service lưu CHAT_HISTORY rồi ServiceResult mới
        // quay lại Controller; UserId hiện null vì request chưa mang định danh.
        if (!result.Success)
        {
            return BadRequest(ApiResponse<object>.Fail(result.Message, result.ErrorCode ?? "ERROR"));
        }

        return Ok(ApiResponse<ChatbotResponse>.Ok(result.Data!));
    }
}
