namespace CinemaSystem.Contracts.Payments;

public sealed class CreatePaymentRequest
{
    public string BookingId { get; set; } = string.Empty;
    public string PaymentProviderId { get; set; } = string.Empty;
}
