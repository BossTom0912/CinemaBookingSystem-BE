using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Controllers;

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
        var result = await _chatbotService.AskAsync(request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse<object>.Fail(result.Message, result.ErrorCode ?? "ERROR"));
        }

        return Ok(ApiResponse<ChatbotResponse>.Ok(result.Data!));
    }
}
