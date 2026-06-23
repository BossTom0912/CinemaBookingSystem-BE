namespace CinemaSystem.Contracts.Dashboard;

public sealed class ManagerDashboardOverviewResponse
{
    public string? CinemaId { get; init; }

    public string? CinemaName { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public decimal GrossRevenue { get; init; }

    public decimal RefundedAmount { get; init; }

    public decimal TotalRevenue { get; init; }

    public int TicketsSold { get; init; }

    public int TotalShowtimeSeats { get; init; }

    public decimal RoomOccupancyRate { get; init; }
}
