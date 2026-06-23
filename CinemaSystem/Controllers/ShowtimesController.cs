using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/showtimes")]
public sealed class ShowtimesController : ControllerBase
{
    private readonly IShowtimeService _showtimeService;
    private readonly ICinemaScopeAuthorizationService _cinemaScopeAuthorizationService;

    public ShowtimesController(
        IShowtimeService showtimeService,
        ICinemaScopeAuthorizationService cinemaScopeAuthorizationService)
    {
        _showtimeService = showtimeService;
        _cinemaScopeAuthorizationService = cinemaScopeAuthorizationService
            ?? throw new ArgumentNullException(nameof(cinemaScopeAuthorizationService));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetShowtimes(CancellationToken cancellationToken)
    {
        var result = await _showtimeService.GetShowtimesAsync(cancellationToken);
        return ToActionResult(result.MapDataTo<IReadOnlyList<Contracts.Showtimes.ShowtimeResponse>, IReadOnlyList<ShowtimeResponse>>());
    }

    [HttpGet("{showtimeId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetShowtimeById(string showtimeId, CancellationToken cancellationToken)
    {
        var result = await _showtimeService.GetShowtimeByIdAsync(showtimeId, cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Showtimes.ShowtimeResponse, ShowtimeResponse>());
    }

    [HttpPost]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> CreateShowtime(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.AuthorizeRoomAsync(User, request.RoomId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _showtimeService.CreateShowtimeAsync(
            request.MapTo<Contracts.Showtimes.CreateShowtimeRequest>(),
            cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Showtimes.ShowtimeResponse, ShowtimeResponse>());
    }

    [HttpPut("{showtimeId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> UpdateShowtime(
        string showtimeId,
        UpdateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var currentScope = await _cinemaScopeAuthorizationService.AuthorizeShowtimeAsync(User, showtimeId, cancellationToken);
        if (!currentScope.Allowed)
        {
            return ToActionResult(currentScope);
        }

        var targetRoomScope = await _cinemaScopeAuthorizationService.AuthorizeRoomAsync(User, request.RoomId, cancellationToken);
        if (!targetRoomScope.Allowed)
        {
            return ToActionResult(targetRoomScope);
        }

        var result = await _showtimeService.UpdateShowtimeAsync(
            showtimeId,
            request.MapTo<Contracts.Showtimes.UpdateShowtimeRequest>(),
            cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Showtimes.ShowtimeResponse, ShowtimeResponse>());
    }

    [HttpDelete("{showtimeId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> DeleteShowtime(string showtimeId, CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.AuthorizeShowtimeAsync(User, showtimeId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _showtimeService.DeleteShowtimeAsync(showtimeId, cancellationToken);
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
}
