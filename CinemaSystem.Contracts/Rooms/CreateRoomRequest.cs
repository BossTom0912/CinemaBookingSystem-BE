using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Rooms;

public sealed class CreateRoomRequest
{
    [Required]
    [MaxLength(100)]
    public string RoomName { get; init; } = string.Empty;

    [Range(1, 500)]
    public int Capacity { get; init; }

    [Range(1, 50)]
    public int SeatsPerRow { get; init; } = 10;

    [MaxLength(30)]
    public string RoomStatus { get; init; } = "ACTIVE";
}
