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

    public TicketsController(ITicketScanService ticketScanService)
    {
        _ticketScanService = ticketScanService;
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
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                "Unauthorized.",
                BookingConstants.ErrorCodes.Unauthorized));
        }

        var actorRole = GetActorRole();
        if (string.IsNullOrWhiteSpace(actorRole))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail(
                    "The authenticated role is not allowed to scan tickets.",
                    BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden));
        }

        var result = await _ticketScanService.ScanAsync(
            userId,
            actorRole,
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

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);
        return StatusCode(result.StatusCode, response);
    }
}
