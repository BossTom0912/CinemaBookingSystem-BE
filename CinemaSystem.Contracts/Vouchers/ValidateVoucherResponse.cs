namespace CinemaSystem.Contracts.Vouchers;

public sealed class ValidateVoucherResponse
{
    public bool IsValid { get; init; }
    public decimal DiscountAmount { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
}
