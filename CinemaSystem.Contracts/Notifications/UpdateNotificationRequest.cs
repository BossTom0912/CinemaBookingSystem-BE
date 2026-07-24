namespace CinemaSystem.Contracts.Notifications;

public sealed class UpdateNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
