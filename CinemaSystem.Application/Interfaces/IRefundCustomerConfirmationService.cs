using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Application.Interfaces;

public interface IRefundCustomerConfirmationService
{
    Task<ServiceResult<RefundCustomerConfirmationResponse>> PreviewAsync(
        string customerUserId,
        string token,
        CancellationToken cancellationToken);

    Task<ServiceResult<RefundCustomerConfirmationResponse>> SendAsync(
        string refundId,
        string adminUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult<RefundCustomerConfirmationResponse>> ConfirmAsync(
        string customerUserId,
        ConfirmRefundByCustomerRequest request,
        CancellationToken cancellationToken);
}
