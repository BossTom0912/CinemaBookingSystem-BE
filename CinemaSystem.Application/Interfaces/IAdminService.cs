using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Auth;

namespace CinemaSystem.Application.Interfaces;

public interface IAdminService
{
    Task<ServiceResult<object>> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken);
}
