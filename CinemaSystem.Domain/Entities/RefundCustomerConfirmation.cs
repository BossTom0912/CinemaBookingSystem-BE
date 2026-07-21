namespace CinemaSystem.Domain.Entities;

/// <summary>
/// The customer's explicit approval of the bank-account and refund snapshot
/// before an administrator transfers money manually.
/// </summary>
public sealed class RefundCustomerConfirmation
{
    public string RefundCustomerConfirmationId { get; set; } = null!;
    public string ManualRefundProcessId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public ManualRefundProcess ManualRefundProcess { get; set; } = null!;
}
