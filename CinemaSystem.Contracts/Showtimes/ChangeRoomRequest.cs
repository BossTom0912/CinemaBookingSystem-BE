namespace CinemaSystem.Contracts.Showtimes;

public class ChangeRoomRequest
{
    public string NewRoomId { get; set; } = default!;
    public System.Collections.Generic.Dictionary<string, string>? SeatMapping { get; set; }
    public string? CompensationVoucherCode { get; set; }
    public string? CompensationNote { get; set; }
}
