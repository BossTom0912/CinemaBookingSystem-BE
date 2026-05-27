namespace CinemaSystem.Application.Interfaces;

public interface IClock
{
    DateTime UtcNow { get; }
}
