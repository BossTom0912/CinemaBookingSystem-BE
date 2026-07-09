using System.ComponentModel.DataAnnotations;
using CinemaSystem.Domain.Constants;

namespace CinemaSystem.Contracts.Rooms;

public sealed class CreateRoomRequest
{
    [Required]
    [MaxLength(100)]
    public string RoomName { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Capacity { get; init; }

    [MaxLength(30)]
    public string RoomStatus { get; init; } = DomainConstants.RoomStatus.Active;
}
