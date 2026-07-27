using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Application.Interfaces;

public interface IBankDirectoryAdminService
{
    Task<ServiceResult<IReadOnlyList<BankDirectoryResponse>>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<ServiceResult<BankDirectoryResponse>> UpsertAsync(
        string bankCode,
        UpsertBankDirectoryRequest request,
        CancellationToken cancellationToken);
}
