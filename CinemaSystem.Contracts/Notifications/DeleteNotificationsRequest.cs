using System.Collections.Generic;

namespace CinemaSystem.Contracts.Notifications;

public sealed class DeleteNotificationsRequest
{
    public List<string> NotificationIds { get; set; } = new();
}
