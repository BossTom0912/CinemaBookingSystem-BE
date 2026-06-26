namespace CinemaSystem.Application.Common;

public sealed record PaymentRefundGatewayRequest(
    string RefundId,
    string PaymentId,
    string PaymentProviderId,
    string PaymentProviderName,
    decimal Amount,
    string? ProviderTransactionCode,
    string? Reason);
