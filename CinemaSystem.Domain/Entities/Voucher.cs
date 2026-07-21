using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Voucher
{
    public string VoucherId { get; set; } = null!;

    public string VoucherCode { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public int UsageLimit { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string VoucherStatus { get; set; } = null!;

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public decimal? MinOrderAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public int? PerCustomerLimit { get; set; }

    public int UsedCount { get; set; }

    public string? Category { get; set; }

    public string? ApplicableScope { get; set; }

    public string? TargetType { get; set; }

    public string? TargetCustomerIds { get; set; }

    public string? SpecificFbItemIds { get; set; }

    public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
}
