using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User is not authenticated."));
        }

        var result = await _notificationService.GetNotificationsAsync(userId, isRead, pageIndex, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("read")]
    public async Task<IActionResult> MarkAsRead(
        [FromBody] MarkReadRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User is not authenticated."));
        }

        var result = await _notificationService.MarkAsReadAsync(userId, request.NotificationIds, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User is not authenticated."));
        }

        var result = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("send")]
    [Authorize(Roles = $"{AuthConstants.Roles.Admin},{AuthConstants.Roles.Manager},{AuthConstants.Roles.Staff}")]
    public async Task<IActionResult> SendNotification(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _notificationService.SendNotificationAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("trigger-system")]
    [Authorize(Roles = $"{AuthConstants.Roles.Admin},{AuthConstants.Roles.Manager},{AuthConstants.Roles.Staff}")]
    public async Task<IActionResult> TriggerSystemNotification(
        [FromBody] TriggerSystemNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _notificationService.TriggerSystemNotificationAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("internal-feed")]
    [Authorize(Roles = $"{AuthConstants.Roles.Admin},{AuthConstants.Roles.Manager},{AuthConstants.Roles.Staff}")]
    public async Task<IActionResult> GetInternalFeed(CancellationToken cancellationToken)
    {
        var result = await _notificationService.GetInternalFeedAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("filter-users")]
    [Authorize(Roles = $"{AuthConstants.Roles.Admin},{AuthConstants.Roles.Manager},{AuthConstants.Roles.Staff}")]
    public async Task<IActionResult> GetFilteredUsers(
        [FromQuery] bool? isFlagged,
        [FromQuery] bool? hasBooked,
        [FromQuery] string? roomId,
        [FromQuery] string? showtimeId,
        [FromQuery] string? movieId,
        [FromQuery] string? targetGroup,
        CancellationToken cancellationToken)
    {
        var result = await _notificationService.GetFilteredUsersAsync(
            isFlagged, hasBooked, roomId, showtimeId, movieId, targetGroup, cancellationToken);
        return ToActionResult(result);
    }

    private string? GetUserId()
    {
        return User.FindFirst(AuthConstants.Claims.UserId)?.Value
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
