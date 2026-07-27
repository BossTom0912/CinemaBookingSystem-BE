using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Tickets;
using CinemaSystem.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize(Policy = AuthConstants.Policies.CanScanTicket)]
public sealed class TicketsController : ControllerBase
{
    private readonly ITicketScanService _ticketScanService;
    private readonly IHubContext<TicketHub> _ticketHub;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(
        ITicketScanService ticketScanService,
        IHubContext<TicketHub> ticketHub,
        ILogger<TicketsController> logger)
    {
        _ticketScanService = ticketScanService;
        _ticketHub = ticketHub;
        _logger = logger;
    }

    [HttpPost("scan/preview")]
    [ProducesResponseType(typeof(ApiResponse<ScanTicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Preview(
        [FromBody] ScanTicketRequest request,
        CancellationToken cancellationToken)
    {
        var access = GetScanAccess();
        if (access.Result is not null)
        {
            return access.Result;
        }

        var result = await _ticketScanService.PreviewAsync(
            access.UserId,
            access.ActorRole,
            request,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            await BroadcastTicketPreviewAsync(access.UserId, result.Data, cancellationToken);
        }

        return ToActionResult(result);
    }

    [HttpPost("scan/confirm")]
    [ProducesResponseType(typeof(ApiResponse<ScanTicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        [FromBody] ConfirmTicketScanRequest request,
        CancellationToken cancellationToken)
    {
        var access = GetScanAccess();
        if (access.Result is not null)
        {
            return access.Result;
        }

        var result = await _ticketScanService.ConfirmAsync(
            access.UserId,
            access.ActorRole,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("scan")]
    [ProducesResponseType(typeof(ApiResponse<ScanTicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Scan(
        [FromBody] ScanTicketRequest request,
        CancellationToken cancellationToken)
    {
        var access = GetScanAccess();
        if (access.Result is not null)
        {
            return access.Result;
        }

        var result = await _ticketScanService.ScanAsync(
            access.UserId,
            access.ActorRole,
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

    private string GetActorRole()
    {
        foreach (var role in new[]
        {
            AuthConstants.Roles.Admin,
            AuthConstants.Roles.Manager,
            AuthConstants.Roles.Staff,
            AuthConstants.Roles.Customer
        })
        {
            if (User.IsInRole(role))
            {
                return role;
            }
        }

        var claimRole = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? string.Empty;
        return AuthConstants.Roles.Normalize(claimRole);
    }

    private (string UserId, string ActorRole, ObjectResult? Result) GetScanAccess()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (
                string.Empty,
                string.Empty,
                Unauthorized(ApiResponse<object>.Fail(
                    "Unauthorized.",
                    BookingConstants.ErrorCodes.Unauthorized)));
        }

        var actorRole = GetActorRole();
        if (string.IsNullOrWhiteSpace(actorRole))
        {
            return (
                userId,
                string.Empty,
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(
                        "The authenticated role is not allowed to scan tickets.",
                        BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden)));
        }

        return (userId, actorRole, null);
    }

    private async Task BroadcastTicketPreviewAsync(
        string userId,
        ScanTicketResponse ticket,
        CancellationToken cancellationToken)
    {
        try
        {
            await _ticketHub.Clients
                .Group(userId)
                .SendAsync("ReceiveScannedTicket", ticket, cancellationToken);

            _logger.LogInformation(
                "[TicketHub] Broadcasted ticket preview to Group={UserId} TicketId={TicketId}",
                userId,
                ticket.TicketId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[TicketHub] Failed to broadcast preview to Group={UserId}. Preview result still returned to caller.",
                userId);
        }
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);
        return StatusCode(result.StatusCode, response);
    }
}
