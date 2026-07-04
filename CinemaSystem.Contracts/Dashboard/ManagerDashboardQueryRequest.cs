using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Dashboard;

public sealed class ManagerDashboardQueryRequest
{
    [Required]
    public DateTime? From { get; init; }

    [Required]
    public DateTime? To { get; init; }

    [MaxLength(DashboardContractConstants.MovieIdMaxLength)]
    public string? MovieId { get; init; }
}
