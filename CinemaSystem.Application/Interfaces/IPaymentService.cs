using CinemaSystem.Contracts.Payments;

namespace CinemaSystem.Application.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default,
        string? clientIpAddress = null);

    Task ConfirmPaymentAsync(
        string transactionContent,
        decimal amount,
        string? providerTransactionCode = null,
        string? rawCallbackPayload = null,
        CancellationToken cancellationToken = default);
}
