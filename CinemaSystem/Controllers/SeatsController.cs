using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/seats")]
public sealed class SeatsController : ControllerBase
{
    private readonly ISeatService _seatService;

    public SeatsController(ISeatService seatService)
    {
        _seatService = seatService
            ?? throw new ArgumentNullException(nameof(seatService));
    }

    /// <summary>
    /// Create seat — applied immediately. Admin and Manager may create.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    [ProducesResponseType(
        typeof(ApiResponse<bool>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSeatRequest(
        [FromBody] CreateSeatRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var result = await _seatService.CreateSeatAsync(
            request.MapTo<Contracts.Seats.CreateSeatRequest>(),
            userId,
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Update seat — applied immediately. Admin and Manager may update.
    /// </summary>
    [HttpPut("{seatId}")]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    [ProducesResponseType(
        typeof(ApiResponse<bool>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> UpdateSeat(
        string seatId,
        [FromBody] UpdateSeatRequest request,
        CancellationToken cancellationToken)
    {
        request.SeatId = seatId;

        var result = await _seatService.UpdateSeatAsync(
            request.MapTo<Contracts.Seats.UpdateSeatRequest>(),
            GetUserId(),
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Delete seat — applied immediately. Admin and Manager may delete.
    /// </summary>
    [HttpDelete("{seatId}")]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    [ProducesResponseType(
        typeof(ApiResponse<bool>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> DeleteSeat(
        string seatId,
        CancellationToken cancellationToken)
    {
        var result = await _seatService.DeleteSeatAsync(
            seatId,
            GetUserId(),
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Manager / Admin can view room seat layout.
    /// </summary>
    [HttpGet("room/{roomId}")]
    [Authorize(
        Roles = $"{AuthConstants.Roles.Staff},{AuthConstants.Roles.Manager},{AuthConstants.Roles.Admin}")]
    [ProducesResponseType(
        typeof(ApiResponse<IEnumerable<SeatResponse>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeatsByRoom(
        string roomId,
        CancellationToken cancellationToken)
    {
        var result = await _seatService.GetSeatsByRoomAsync(
            roomId,
            cancellationToken);

        return ToActionResult(result);
    }

    // Admin approval endpoints removed - approval flow centralized under admin requests controller.

    [Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
    [HttpPost("lock")]
    [ProducesResponseType(
        typeof(ApiResponse<LockSeatResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> LockSeat(
        [FromBody] LockSeatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _seatService.LockSeatAsync(
            request.MapTo<Contracts.Seats.LockSeatRequest>(),
            GetUserId(),
            cancellationToken);

        return ToActionResult(result.MapDataTo<Contracts.Seats.LockSeatResponse, LockSeatResponse>());
    }

    [Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
    [HttpPost("unlock")]
    [ProducesResponseType(
        typeof(ApiResponse<UnlockSeatResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlockSeat(
        [FromBody] UnlockSeatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _seatService.UnlockSeatAsync(
            request.MapTo<Contracts.Seats.UnlockSeatRequest>(),
            GetUserId(),
            cancellationToken);

        return ToActionResult(result.MapDataTo<Contracts.Seats.UnlockSeatResponse, UnlockSeatResponse>());
    }

    [Authorize(Policy = AuthConstants.Policies.CanSelectSeat)]
    [HttpGet("showtimes/{showtimeId}/map")]
    [ProducesResponseType(
        typeof(ApiResponse<SeatMapResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeatMap(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        var result = await _seatService.GetSeatMapAsync(
            showtimeId,
            cancellationToken);

        return ToActionResult(result.MapDataTo<Contracts.Seats.SeatMapResponse, SeatMapResponse>());
    }

    private string GetUserId()
    {
        return User.FindFirst("userId")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty;
    }

    private ObjectResult ToActionResult<T>(
        ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(
                result.Data,
                result.Message)
            : ApiResponse<T>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(
            result.StatusCode,
            response);
    }
}
