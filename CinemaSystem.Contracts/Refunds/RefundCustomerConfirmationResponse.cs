namespace CinemaSystem.Contracts.Refunds;

public sealed class RefundCustomerConfirmationResponse
{
    public string RefundId { get; init; } = string.Empty;
    public string ManualRefundProcessId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public string BookingId { get; init; } = string.Empty;
    public decimal RefundAmount { get; init; }
    public string MovieTitle { get; init; } = string.Empty;
    public string CinemaName { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public DateTime ShowtimeStartTime { get; init; }
    public IReadOnlyList<string> SeatCodes { get; init; } = Array.Empty<string>();
    public string? BankCode { get; init; }
    public string? BankName { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? AccountHolderName { get; init; }
}
