using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Infrastructure.Refunds;

public sealed class UnsupportedPaymentRefundGateway : IPaymentRefundGateway
{
    public Task<PaymentRefundGatewayResult> RefundAsync(
        PaymentRefundGatewayRequest request,
        CancellationToken cancellationToken)
    {
        // The current SePay integration only receives successful payment webhooks.
        // No refund endpoint or refund credentials are defined in project configuration,
        // so claiming a successful automatic refund would create false financial data.
        return Task.FromResult(PaymentRefundGatewayResult.Unsupported(
            $"Payment provider '{request.PaymentProviderName}' does not have an automatic refund adapter configured."));
    }
}
