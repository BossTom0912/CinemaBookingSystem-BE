using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Showtimes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/showtimes")]
public sealed class ShowtimesController : ControllerBase
{
    private readonly IShowtimeService _showtimeService;

    public ShowtimesController(IShowtimeService showtimeService)
    {
        _showtimeService = showtimeService;
    }

    [HttpGet]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Staff + "," + AuthConstants.Roles.Customer)]
    public async Task<IActionResult> GetShowtimes(CancellationToken cancellationToken)
    {
        var result = await _showtimeService.GetShowtimesAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{showtimeId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Staff + "," + AuthConstants.Roles.Customer)]
    public async Task<IActionResult> GetShowtimeById(string showtimeId, CancellationToken cancellationToken)
    {
        var result = await _showtimeService.GetShowtimeByIdAsync(showtimeId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> CreateShowtime(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _showtimeService.CreateShowtimeAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{showtimeId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> UpdateShowtime(
        string showtimeId,
        UpdateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _showtimeService.UpdateShowtimeAsync(showtimeId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{showtimeId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> DeleteShowtime(string showtimeId, CancellationToken cancellationToken)
    {
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
}
