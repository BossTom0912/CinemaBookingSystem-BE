using CinemaSystem.Contracts.Payments;

namespace CinemaSystem.Application.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default);
    Task ConfirmPaymentAsync(
        string transactionContent,
        decimal amount,
        string? providerTransactionCode = null,
        string? rawCallbackPayload = null,
        CancellationToken cancellationToken = default);
}
