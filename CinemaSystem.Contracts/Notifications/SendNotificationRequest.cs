using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Notifications;

public sealed class SendNotificationRequest
{
    // Send to a single user
    public string? UserId { get; init; }

    // Or send to multiple specific users
    public List<string>? UserIds { get; init; }

    // Or send to a predefined target group: ALL, CUSTOMERS, STAFF, MANAGERS, ADMINS
    public string? TargetGroup { get; init; }

    public string? BookingId { get; init; }

    [Required]
    [MaxLength(255)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; init; } = string.Empty;

    public string Channel { get; init; } = "App"; // App, Email, SMS, Signage, Internal

    public string Type { get; init; } = "Transactional"; // Transactional, Loyalty, Promotional, Internal
}
