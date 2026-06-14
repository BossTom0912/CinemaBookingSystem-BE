namespace CinemaSystem.Contracts.Seats;

public sealed class UnlockSeatResponse
{
    public string ShowtimeSeatId { get; init; } = string.Empty;

    public string ShowtimeId { get; init; } = string.Empty;

    public string SeatId { get; init; } = string.Empty;

    public string SeatStatus { get; init; } = string.Empty;
}
