using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Dashboard;

namespace CinemaSystem.Application.Interfaces;

public interface IManagerDashboardService
{
    Task<ServiceResult<ManagerDashboardResponse>> GetDashboardAsync(
        string? cinemaScopeId,
        ManagerDashboardQueryRequest request,
        CancellationToken cancellationToken);
}
