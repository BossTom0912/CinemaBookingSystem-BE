using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Showtimes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/manager/showtimes")]
public sealed class ShowtimeCancellationsController : ControllerBase
{
    private readonly IShowtimeCancellationService _showtimeCancellationService;
    private readonly ICinemaScopeAuthorizationService _cinemaScopeAuthorizationService;

    public ShowtimeCancellationsController(
        IShowtimeCancellationService showtimeCancellationService,
        ICinemaScopeAuthorizationService cinemaScopeAuthorizationService)
    {
        _showtimeCancellationService = showtimeCancellationService;
        _cinemaScopeAuthorizationService = cinemaScopeAuthorizationService;
    }

    [HttpPost("{showtimeId}/cancel")]
    [Authorize(Policy = AuthConstants.Policies.CanCancelShowtimeAndRefund)]
    [ProducesResponseType(
        typeof(ApiResponse<CancelShowtimeResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelShowtime(
        string showtimeId,
        [FromBody] CancelShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.AuthorizeShowtimeAsync(
            User,
            showtimeId,
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

        var result = await _showtimeCancellationService.CancelShowtimeAsync(
            showtimeId,
            userId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    private string GetUserId()
    {
        return User.FindFirst("userId")?.Value
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")?.Value
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
