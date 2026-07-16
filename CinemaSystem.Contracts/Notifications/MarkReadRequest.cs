using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Notifications;

public sealed class MarkReadRequest
{
    [Required]
    public List<string> NotificationIds { get; init; } = new();
}
