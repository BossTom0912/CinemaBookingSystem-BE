using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Tests.Infrastructure;

public sealed class NoOpRefundProcessor : IRefundProcessor
{
    public Task<ServiceResult<RefundProcessingResponse>> ProcessAsync(
        string refundId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ServiceResult<RefundProcessingResponse>.Ok(
            new RefundProcessingResponse
            {
                RefundId = refundId,
                RefundStatus = BookingConstants.RefundStatus.Pending,
                BookingStatus = BookingConstants.BookingStatus.RefundPending
            }));
    }
}
