namespace CinemaSystem.Infrastructure.Configuration;

public sealed class TicketScanSettings
{
    public const string SectionName = "TicketScanSettings";

    public int? OpenBeforeStartMinutes { get; set; }

    public int? CloseAfterEndMinutes { get; set; }
}
