using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize(Policy = AuthConstants.Policies.CanScanTicket)]
public sealed class TicketsController : ControllerBase
{
    private readonly ITicketScanService _ticketScanService;
    private readonly ICinemaScopeAuthorizationService _cinemaScopeAuthorizationService;

    public TicketsController(
        ITicketScanService ticketScanService,
        ICinemaScopeAuthorizationService cinemaScopeAuthorizationService)
    {
        _ticketScanService = ticketScanService;
        _cinemaScopeAuthorizationService = cinemaScopeAuthorizationService;
    }

    [HttpPost("scan")]
    [ProducesResponseType(
        typeof(ApiResponse<ScanTicketResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Scan(
        [FromBody] ScanTicketRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.GetUserCinemaScopeAsync(
            User,
            cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                "Unauthorized.",
                BookingConstants.ErrorCodes.Unauthorized));
        }

        var result = await _ticketScanService.ScanAsync(
            userId,
            scope.CinemaId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    private string GetUserId()
    {
        return User.FindFirst(AuthConstants.Claims.UserId)?.Value
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? string.Empty;
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);
        return StatusCode(result.StatusCode, response);
    }

    private ObjectResult ToActionResult(CinemaScopeAuthorizationResult result)
    {
        var response = ApiResponse<object>.Fail(result.Message, result.ErrorCode);
        return StatusCode(result.StatusCode, response);
    }
}
