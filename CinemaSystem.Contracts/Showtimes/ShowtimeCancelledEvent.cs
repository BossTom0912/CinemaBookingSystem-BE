namespace CinemaSystem.Contracts.Showtimes;

public class ShowtimeCancelledEvent
{
    public string ShowtimeId { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
}
