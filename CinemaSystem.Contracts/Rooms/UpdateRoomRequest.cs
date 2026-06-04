using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Rooms;

public sealed class UpdateRoomRequest
{
    [Required]
    [MaxLength(100)]
    public string RoomName { get; init; } = string.Empty;

    [MaxLength(30)]
    public string RoomStatus { get; init; } = "ACTIVE";
}
