using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Notifications;

namespace CinemaSystem.Application.Interfaces;

public interface INotificationService
{
    Task<ServiceResult<PagedList<NotificationResponse>>> GetNotificationsAsync(
        string userId,
        bool? isRead,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> MarkAsReadAsync(
        string userId,
        List<string> notificationIds,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<NotificationResponse>> SendNotificationAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<NotificationResponse>> SendNotificationAsync(
        string? senderUserId,
        SendNotificationRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> TriggerSystemNotificationAsync(
        TriggerSystemNotificationRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<NotificationResponse>>> GetInternalFeedAsync(string? userId = null, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<UserFilterItemResponse>>> GetFilteredUsersAsync(
        bool? isFlagged,
        bool? hasBooked,
        string? roomId,
        string? showtimeId,
        string? movieId,
        string? targetGroup,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteNotificationsAsync(
        List<string> notificationIds,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> UpdateNotificationAsync(
        string notificationId,
        string title,
        string message,
        CancellationToken cancellationToken);
}
