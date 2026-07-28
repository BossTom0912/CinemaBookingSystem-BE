namespace CinemaSystem.Application.Common;

public sealed record VoucherPolicyValidationResult(
    bool IsValid,
    string? ErrorCode = null,
    string? Message = null)
{
    public static VoucherPolicyValidationResult Valid()
        => new(true);

    public static VoucherPolicyValidationResult Invalid(string errorCode, string message)
        => new(false, errorCode, message);
}
