using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

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

    public virtual RefundClaim? RefundClaim { get; set; }

    public virtual ManualRefundProcess? ManualRefundProcess { get; set; }

    public virtual ICollection<CustomerRefundRequest> CustomerRefundRequests { get; set; } = new List<CustomerRefundRequest>();
}
