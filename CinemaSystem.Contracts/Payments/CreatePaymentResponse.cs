namespace CinemaSystem.Contracts.Payments;

public sealed class CreatePaymentResponse
{
    public string PaymentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty;
    public System.DateTime? ExpiresAt { get; set; }
}
