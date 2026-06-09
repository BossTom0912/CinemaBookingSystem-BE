using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
    }

    [HttpGet("rooms")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Staff)]
    public async Task<IActionResult> GetRooms(CancellationToken cancellationToken)
    {
        var result = await _roomService.GetRoomsAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Staff)]
    public async Task<IActionResult> GetRoomById(string roomId, CancellationToken cancellationToken)
    {
        var result = await _roomService.GetRoomByIdAsync(roomId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("cinemas/{cinemaId}/rooms")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> CreateRoom(
        string cinemaId,
        CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _roomService.CreateRoomAsync(cinemaId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> UpdateRoom(
        string roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _roomService.UpdateRoomAsync(roomId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> DeleteRoom(string roomId, CancellationToken cancellationToken)
    {
        var result = await _roomService.DeleteRoomAsync(roomId, cancellationToken);
        return ToActionResult(result);
    }
    [HttpPost("{roomId}/generate-seats")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateSeats(
        string roomId,
        [FromBody] GenerateSeatsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _roomService.GenerateSeatsAsync(
            roomId,
            request,
            cancellationToken);

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
