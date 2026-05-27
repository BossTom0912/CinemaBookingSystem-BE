using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class Payment
{
    public string PaymentId { get; set; } = null!;

    public string BookingId { get; set; } = null!;

    public string PaymentProviderId { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? TransactionCode { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? PaymentMethod { get; set; }

    public string? ProviderTransactionCode { get; set; }

    public string? FailureReason { get; set; }

    public string? RawCallbackPayload { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual PaymentProvider PaymentProvider { get; set; } = null!;

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
