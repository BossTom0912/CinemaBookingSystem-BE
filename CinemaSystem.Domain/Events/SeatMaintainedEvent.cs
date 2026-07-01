using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Domain.Events;

public class SeatMaintainedEvent : INotification
{
    public string SeatId { get; set; } = null!;
    public string RoomId { get; set; } = null!;
}
