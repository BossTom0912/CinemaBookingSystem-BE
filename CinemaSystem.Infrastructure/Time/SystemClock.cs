using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
