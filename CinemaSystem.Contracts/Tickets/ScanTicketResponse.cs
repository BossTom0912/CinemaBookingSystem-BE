namespace CinemaSystem.Contracts.Tickets;

public sealed class ScanTicketResponse
{
    public string CheckInLogId { get; init; } = string.Empty;

    public DateTime CheckedInAt { get; init; }

    public string TicketId { get; init; } = string.Empty;

    public string TicketStatus { get; init; } = string.Empty;

    public string BookingId { get; init; } = string.Empty;

    public string BookingStatus { get; init; } = string.Empty;

    public string ShowtimeId { get; init; } = string.Empty;

    public DateTime ShowtimeStartTime { get; init; }

    public DateTime ShowtimeEndTime { get; init; }

    public string MovieTitle { get; init; } = string.Empty;

    public string CinemaId { get; init; } = string.Empty;

    public string CinemaName { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string RoomName { get; init; } = string.Empty;

    public string SeatId { get; init; } = string.Empty;

    public string SeatCode { get; init; } = string.Empty;

    public string RowLabel { get; init; } = string.Empty;

    public int SeatNumber { get; init; }
}
