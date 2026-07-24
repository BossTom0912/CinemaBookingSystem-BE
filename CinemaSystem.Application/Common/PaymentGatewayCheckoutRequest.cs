namespace CinemaSystem.Application.Common;

public sealed record PaymentGatewayCheckoutRequest(
    string PaymentId,
    string TransactionCode,
    decimal Amount,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    string ClientIpAddress);
