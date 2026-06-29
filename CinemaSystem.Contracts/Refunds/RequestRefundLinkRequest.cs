using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class RequestRefundLinkRequest
{
    [Required, StringLength(50)]
    public string BookingId { get; init; } = string.Empty;

    [StringLength(50)]
    public string? TicketId { get; init; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;
}
