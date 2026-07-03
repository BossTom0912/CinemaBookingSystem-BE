using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class RequestRefundLinkRequest
{
    [Required, StringLength(RefundContractConstants.EntityIdMaxLength)]
    public string BookingId { get; init; } = string.Empty;

    [StringLength(RefundContractConstants.EntityIdMaxLength)]
    public string? TicketId { get; init; }

    [Required, StringLength(
        RefundContractConstants.RefundRequestReasonMaxLength,
        MinimumLength = RefundContractConstants.RefundRequestReasonMinLength)]
    public string Reason { get; init; } = string.Empty;
}
