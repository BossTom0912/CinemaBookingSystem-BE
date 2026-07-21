namespace CinemaSystem.Application.Common;

public sealed record CompensationIssueResult(
    string CompensationId,
    int TicketVouchersIssued,
    int ComboVouchersIssued,
    DateTime ExpiresAt,
    bool AlreadyIssued,
    IReadOnlyList<string> TicketVoucherCodes,
    string? ComboVoucherCode);
