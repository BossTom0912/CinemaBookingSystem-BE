using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class VoucherUsage
{
    public string VoucherUsageId { get; set; } = null!;

    public string VoucherId { get; set; } = null!;

    public string CustomerProfileId { get; set; } = null!;

    public string BookingId { get; set; } = null!;

    public string UsageStatus { get; set; } = null!;

    public DateTime? UsedAt { get; set; }

    public decimal DiscountAmount { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual CustomerProfile CustomerProfile { get; set; } = null!;

    public virtual Voucher Voucher { get; set; } = null!;
}
