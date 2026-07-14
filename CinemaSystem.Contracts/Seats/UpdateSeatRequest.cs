namespace CinemaSystem.Contracts.Seats;

public sealed class UpdateSeatRequest
{
    public string SeatId { get; set; } = default!;
    public string RowLabel { get; set; } = default!;
    public int SeatNumber { get; set; }
    public string SeatTypeId { get; set; } = default!;
    public string SeatStatus { get; set; } = default!;
}
