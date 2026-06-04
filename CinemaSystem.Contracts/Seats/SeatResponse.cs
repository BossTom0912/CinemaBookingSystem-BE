namespace CinemaSystem.Contracts.Seats;

public sealed class SeatResponse
{
    public string SeatId { get; init; } = null!;

    public string RoomId { get; init; } = null!;

    public string RowLabel { get; init; } = null!;

    public int SeatNumber { get; init; }

    public string SeatCode { get; init; } = null!;

    public string SeatTypeId { get; init; } = null!;

    public bool IsActive { get; init; }
}
