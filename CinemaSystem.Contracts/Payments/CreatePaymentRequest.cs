namespace CinemaSystem.Contracts.Payments;

public sealed class CreatePaymentRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string BookingId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string PaymentProviderId { get; set; } = string.Empty;
}
