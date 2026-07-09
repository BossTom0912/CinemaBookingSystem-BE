using System.ComponentModel.DataAnnotations;
using CinemaSystem.Domain.Constants;

namespace CinemaSystem.Contracts.Rooms;

public sealed class UpdateRoomRequest
{
    [Required]
    [MaxLength(100)]
    public string RoomName { get; init; } = string.Empty;
    public int Capacity
    {
        get; init;
    }

        [MaxLength(30)]
    public string RoomStatus { get; init; } = DomainConstants.RoomStatus.Active;
}
