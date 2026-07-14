using System;

namespace CinemaSystem.Contracts.Vouchers;

public sealed class VoucherResponse
{
    public string VoucherId { get; init; } = string.Empty;

    public string VoucherCode { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public string DiscountType { get; init; } = string.Empty;

    public decimal DiscountValue { get; init; }

    public decimal? MinOrderAmount { get; init; }

    public decimal? MaxDiscountAmount { get; init; }

    public int UsageLimit { get; init; }

    public int? PerCustomerLimit { get; init; }

    public int UsedCount { get; init; }

    public DateTime StartDate { get; init; }

    public DateTime EndDate { get; init; }

    public string VoucherStatus { get; init; } = string.Empty;
}
