using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly ICinemaScopeAuthorizationService _cinemaScopeAuthorizationService;

    public RoomsController(
        IRoomService roomService,
        ICinemaScopeAuthorizationService cinemaScopeAuthorizationService)
    {
        _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
        _cinemaScopeAuthorizationService = cinemaScopeAuthorizationService
            ?? throw new ArgumentNullException(nameof(cinemaScopeAuthorizationService));
    }

    [HttpGet("rooms")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Staff)]
    public async Task<IActionResult> GetRooms([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var scope = await _cinemaScopeAuthorizationService.GetUserCinemaScopeAsync(User, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _roomService.GetRoomsAsync(scope.CinemaId, includeInactive, cancellationToken);
        return ToActionResult(result.MapDataTo<IReadOnlyList<Contracts.Rooms.RoomResponse>, IReadOnlyList<RoomResponse>>());
    }

    [HttpGet("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Staff)]
    public async Task<IActionResult> GetRoomById(string roomId, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var scope = await _cinemaScopeAuthorizationService.AuthorizeRoomAsync(User, roomId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _roomService.GetRoomByIdAsync(roomId, includeInactive, cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Rooms.RoomResponse, RoomResponse>());
    }

    [HttpPost("cinemas/{cinemaId}/rooms")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> CreateRoom(
        string cinemaId,
        CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.AuthorizeCinemaAsync(User, cinemaId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _roomService.CreateRoomAsync(
            cinemaId,
            request.MapTo<Contracts.Rooms.CreateRoomRequest>(),
            cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Rooms.RoomResponse, RoomResponse>());
    }

    [HttpPut("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> UpdateRoom(
        string roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.AuthorizeRoomAsync(User, roomId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _roomService.UpdateRoomAsync(
            roomId,
            request.MapTo<Contracts.Rooms.UpdateRoomRequest>(),
            GetUserId(),
            cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Rooms.RoomResponse, RoomResponse>());
    }

    [HttpDelete("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> DeleteRoom(string roomId, CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.AuthorizeRoomAsync(User, roomId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _roomService.DeleteRoomAsync(roomId, GetUserId(), cancellationToken);
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
        var scope = await _cinemaScopeAuthorizationService.AuthorizeRoomAsync(User, roomId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _roomService.GenerateSeatsAsync(
            roomId,
            request.MapTo<Contracts.Rooms.GenerateSeatsRequest>(),
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

    private ObjectResult ToActionResult(CinemaScopeAuthorizationResult result)
    {
        var response = ApiResponse<object>.Fail(result.Message, result.ErrorCode);
        return StatusCode(result.StatusCode, response);
    }

    private string GetUserId()
    {
        return User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("userId")?.Value
            ?? string.Empty;
    }
}
