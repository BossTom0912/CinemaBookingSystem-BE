namespace CinemaSystem.Contracts.Showtimes;

public sealed class ShowtimeResponse
{
    public string ShowtimeId { get; init; } = string.Empty;

    public string MovieId { get; init; } = string.Empty;

    public string MovieTitle { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string RoomName { get; init; } = string.Empty;

    public string CinemaId { get; init; } = string.Empty;

    public string CinemaName { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public decimal BasePrice { get; init; }

    public string Status { get; init; } = string.Empty;

    public int ShowtimeSeatCount { get; init; }

    public bool HasBookings { get; init; }
}
