namespace CinemaSystem.Contracts.Seats;

public class SeatDto
{
    public string RowLabel { get; set; } = null!;
    public int SeatNumber { get; set; }
    public string SeatTypeId { get; set; } = null!;
}
