using CinemaSystem.Application.Common;

namespace CinemaSystem.Application.Interfaces;

public interface IPaymentGateway
{
    string ProviderName { get; }

    string CreateCheckoutUrl(PaymentGatewayCheckoutRequest request);
}
