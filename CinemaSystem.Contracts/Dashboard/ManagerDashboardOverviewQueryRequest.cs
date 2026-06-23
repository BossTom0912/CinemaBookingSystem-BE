namespace CinemaSystem.Contracts.Dashboard;

public sealed class ManagerDashboardOverviewQueryRequest
{
    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}
