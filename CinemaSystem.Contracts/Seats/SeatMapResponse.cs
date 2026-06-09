namespace CinemaSystem.Contracts.Seats;

public sealed class SeatMapResponse
{
    public string ShowtimeId { get; init; } = string.Empty;

    public IReadOnlyList<SeatMapItemResponse> AvailableSeats { get; init; } = [];

    public IReadOnlyList<SeatMapItemResponse> LockedSeats { get; init; } = [];

    public IReadOnlyList<SeatMapItemResponse> SoldSeats { get; init; } = [];
}

public sealed class SeatMapItemResponse
{
    public string ShowtimeSeatId { get; init; } = string.Empty;

    public string SeatId { get; init; } = string.Empty;

    public string RowLabel { get; init; } = string.Empty;

    public int SeatNumber { get; init; }

    public string SeatCode { get; init; } = string.Empty;

    public string SeatTypeId { get; init; } = string.Empty;

    public string SeatStatus { get; init; } = string.Empty;

    public DateTime? LockedUntil { get; init; }
}
