namespace CinemaSystem.Contracts.Customers;

public sealed class VerifyEmailUpdateRequest
{
    public string NewEmail { get; init; } = string.Empty;
    public string Otp { get; init; } = string.Empty;
}
