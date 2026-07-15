using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Dashboard;

namespace CinemaSystem.Application.Interfaces;

public interface IStaffShiftReportService
{
    Task<ApiResponse<StaffShiftReportResponse>> GetShiftReportAsync(
        UserScope scope,
        StaffShiftReportQueryRequest request,
        CancellationToken cancellationToken);
}
