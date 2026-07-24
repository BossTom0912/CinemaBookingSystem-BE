using System;

namespace CinemaSystem.Contracts.Notifications;

public sealed class NotificationResponse
{
    public string NotificationId { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string? BookingId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool IsRead { get; init; }

    public DateTime CreatedAt { get; init; }

    public string Channel { get; init; } = "App"; // App, Email, SMS, Signage, Internal

    public string Type { get; init; } = "Transactional"; // Transactional, Loyalty, Promotional, Internal

    public string Status { get; init; } = "Sent"; // Created, Sending, Sent, Failed

    public int? CinemaId { get; init; }

    public string? CinemaName { get; init; }
}
