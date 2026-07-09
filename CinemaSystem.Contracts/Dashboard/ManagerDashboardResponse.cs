namespace CinemaSystem.Contracts.Dashboard;

public sealed class ManagerDashboardResponse
{
    public string? CinemaId { get; init; }

    public string CinemaName { get; init; } = string.Empty;

    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public string? MovieId { get; init; }

    public decimal GrossRevenue { get; init; }

    public decimal RefundedAmount { get; init; }

    public decimal PendingRefundAmount { get; init; }

    public decimal ManualRefundAmount { get; init; }

    public decimal NetRevenue { get; init; }

    public int GrossTicketsSold { get; init; }

    public int RefundedTickets { get; init; }

    public int NetTicketsSold { get; init; }

    public int SellableSeatCapacity { get; init; }

    public int OccupiedSeats { get; init; }

    public decimal OccupancyRate { get; init; }
}
