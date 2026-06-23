using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Dashboard;

namespace CinemaSystem.Application.Interfaces;

public interface IManagerDashboardService
{
    Task<ServiceResult<ManagerDashboardOverviewResponse>> GetOverviewAsync(
        string? cinemaScopeId,
        ManagerDashboardOverviewQueryRequest request,
        CancellationToken cancellationToken);
}
