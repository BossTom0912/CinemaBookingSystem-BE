using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Application.Interfaces;

public interface IRefundService
{
    Task<ServiceResult<IReadOnlyList<RefundResponse>>> GetRefundsAsync(
        string? cinemaScopeId,
        RefundQueryRequest request,
        CancellationToken cancellationToken);
}
