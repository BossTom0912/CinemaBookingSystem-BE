using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Auth;

namespace CinemaSystem.Application.Interfaces;

public interface IAccountProvisioningService
{
    Task<ServiceResult<IReadOnlyList<AssignableAccountRoleResponse>>> GetAssignableRolesAsync(
        string actorUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProvisionedAccountResponse>> ProvisionAsync(
        string actorUserId,
        ProvisionManagedAccountRequest request,
        CancellationToken cancellationToken);
}
