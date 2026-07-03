namespace CinemaSystem.Application.Common;

public enum PaymentRefundGatewayOutcome
{
    Success,
    Failed,
    Unsupported
}

public sealed record PaymentRefundGatewayResult(
    PaymentRefundGatewayOutcome Outcome,
    string? ProviderRefundCode,
    string? FailureReason)
{
    public static PaymentRefundGatewayResult Success(string providerRefundCode)
    {
        return new PaymentRefundGatewayResult(
            PaymentRefundGatewayOutcome.Success,
            providerRefundCode,
            null);
    }

    public static PaymentRefundGatewayResult Failed(string failureReason)
    {
        return new PaymentRefundGatewayResult(
            PaymentRefundGatewayOutcome.Failed,
            null,
            failureReason);
    }

    public static PaymentRefundGatewayResult Unsupported(string failureReason)
    {
        return new PaymentRefundGatewayResult(
            PaymentRefundGatewayOutcome.Unsupported,
            null,
            failureReason);
    }
}
