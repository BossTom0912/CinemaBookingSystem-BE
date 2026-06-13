namespace CinemaSystem.Contracts.Customers;

public sealed class ChangePasswordRequest
{
    public string OldPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
