using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Application.Interfaces;

public interface IManualRefundService
{
    Task<ServiceResult<IReadOnlyList<ManualRefundResponse>>> GetPendingAsync(CancellationToken cancellationToken);
    Task<ServiceResult<AssignManualRefundResponse>> AssignAsync(string refundId, string adminUserId, CancellationToken cancellationToken);
    Task<ServiceResult<RefundProcessingResponse>> ConfirmAsync(string refundId, string adminUserId, ManualRefundConfirmationRequest request, CancellationToken cancellationToken);
}
