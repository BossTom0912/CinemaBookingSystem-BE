namespace CinemaSystem.Contracts.Refunds;

public sealed class BankResponse
{
    public string BankCode { get; init; } = string.Empty;
    public string BankBin { get; init; } = string.Empty;
    public string ShortName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}
