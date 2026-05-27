using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class Refund
{
    public string RefundId { get; set; } = null!;

    public string BookingId { get; set; } = null!;

    public string PaymentId { get; set; } = null!;

    public string PaymentProviderId { get; set; } = null!;

    public string? ShowtimeCancellationId { get; set; }

    public decimal RefundAmount { get; set; }

    public string RefundStatus { get; set; } = null!;

    public string? RefundReason { get; set; }

    public string? ProviderRefundCode { get; set; }

    public string? FailureReason { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual Payment Payment { get; set; } = null!;

    public virtual PaymentProvider PaymentProvider { get; set; } = null!;

    public virtual ShowtimeCancellation? ShowtimeCancellation { get; set; }
}
