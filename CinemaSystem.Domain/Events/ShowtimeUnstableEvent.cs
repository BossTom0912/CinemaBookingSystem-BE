using MediatR;
using System;

namespace CinemaSystem.Domain.Events;

public class ShowtimeUnstableEvent : INotification
{
    public string ShowtimeId { get; set; } = null!;
    public string Reason { get; set; } = null!;
}
