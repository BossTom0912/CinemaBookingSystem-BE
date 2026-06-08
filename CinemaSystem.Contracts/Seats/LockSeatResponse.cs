namespace CinemaSystem.Contracts.Seats;

public sealed class LockSeatResponse
{
    public string ShowtimeSeatId { get; init; } = string.Empty;

    public string ShowtimeId { get; init; } = string.Empty;

    public string SeatId { get; init; } = string.Empty;

    public string SeatStatus { get; init; } = string.Empty;

    public DateTime LockedUntil { get; init; }
}
