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

    Task<ServiceResult<IReadOnlyList<ManagedUserResponse>>> GetManagedUsersAsync(
        CancellationToken cancellationToken);

    Task<ServiceResult<ManagedUserResponse>> UpdateUserRoleCinemaAsync(
        string actorUserId,
        string targetUserId,
        UpdateUserRoleCinemaRequest request,
        CancellationToken cancellationToken);
}
