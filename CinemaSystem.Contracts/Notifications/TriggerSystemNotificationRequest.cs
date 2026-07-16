using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Notifications;

public sealed class TriggerSystemNotificationRequest
{
    [Required]
    public string Type { get; init; } = string.Empty; // RoomCleanup, RoomReady, TechnicalIssue, LowInventory, ComboPreparation, ShiftChange, EmergencyBroadcast

    public string? TargetId { get; init; } // E.g., ShowtimeId, RoomId, UserId

    public string? Message { get; init; } // Optional custom message text
}
