using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Application.Interfaces;

public interface IRefundProcessor
{
    Task<ServiceResult<RefundProcessingResponse>> ProcessAsync(
        string refundId,
        CancellationToken cancellationToken);
}
