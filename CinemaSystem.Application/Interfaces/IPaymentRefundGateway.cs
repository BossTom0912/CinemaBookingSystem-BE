using CinemaSystem.Application.Common;

namespace CinemaSystem.Application.Interfaces;

public interface IPaymentRefundGateway
{
    Task<PaymentRefundGatewayResult> RefundAsync(
        PaymentRefundGatewayRequest request,
        CancellationToken cancellationToken);
}
