using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Application.Interfaces;

public interface IRefundClaimService
{
    Task<ServiceResult<IReadOnlyList<BankResponse>>> GetBanksAsync(CancellationToken cancellationToken);
    Task<ServiceResult<RefundClaimResponse>> ResolveAsync(string userId, ResolveRefundClaimRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<RefundClaimResponse>> SaveBankAccountAsync(string userId, string claimId, SaveRefundBankAccountRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<RefundClaimResponse>> SubmitAsync(string userId, string claimId, CancellationToken cancellationToken);
    Task<ServiceResult<object>> RequestNewLinkAsync(string userId, RequestRefundLinkRequest request, CancellationToken cancellationToken);
}
