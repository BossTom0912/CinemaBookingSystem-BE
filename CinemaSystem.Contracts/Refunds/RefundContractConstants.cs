namespace CinemaSystem.Contracts.Refunds;

public static class RefundContractConstants
{
    public const int EntityIdMaxLength = 50;

    public const int BankTransactionCodeMinLength = 3;
    public const int BankTransactionCodeMaxLength = 255;
    public const string MinimumRefundAmount = "0.01";
    public const string MaximumRefundAmount = "9999999999999999";

    public const int ProofUrlMinLength = 3;
    public const int ProofUrlMaxLength = 1000;
    public const int NoteMaxLength = 1000;

    public const int RefundRequestReasonMinLength = 5;
    public const int RefundRequestReasonMaxLength = 1000;

    public const int ClaimTokenMinLength = 20;
    public const int ClaimTokenMaxLength = 500;

    public const int BankCodeMaxLength = 20;
    public const int AccountNumberMinLength = 6;
    public const int AccountNumberMaxLength = 20;
    public const string AccountNumberPattern = @"^[0-9]{6,20}$";
    public const int AccountHolderNameMinLength = 2;
    public const int AccountHolderNameMaxLength = 255;
    public const int BankAccountVisibleSuffixLength = 4;
    public const string MaskedAccountPrefix = "******";

    public const int CancellationReasonMaxLength = 1000;
    public const int FailureReasonMaxLength = 1000;
}
