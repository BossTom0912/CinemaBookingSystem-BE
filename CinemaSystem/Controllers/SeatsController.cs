using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Seats;
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
        _seatService = seatService ?? throw new ArgumentNullException(nameof(seatService));
    }

    /// <summary>
    /// Retrieves all seats for a specific room.
    /// </summary>
    /// <param name="roomId">The room ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of seats for the room.</returns>
    [HttpGet("room/{roomId}")]
    [Authorize(Policy = AuthConstants.Policies.CanSelectSeat)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<SeatResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSeatsByRoom(string roomId, CancellationToken cancellationToken)
    {
        var result = await _seatService.GetSeatsByRoomAsync(roomId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Batch updates the status of multiple seats with optimized database operations.
    /// OPTIMIZATION: Uses AddRangeAsync and SaveChangesAsync only once for all seats.
    /// This endpoint demonstrates the optimized batch update pattern - reducing N database calls to 1.
    /// </summary>
    /// <param name="request">Batch update request containing seat IDs and new status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of seats updated.</returns>
    [HttpPost("batch-update-status")]
    [Authorize(Policy = AuthConstants.Policies.CanManageCinemaRoomSeat)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> BatchUpdateSeatStatus(
        BatchUpdateSeatStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", "VALIDATION_ERROR"));
        }

        var result = await _seatService.BatchUpdateSeatStatusAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
