using System;
using System.ComponentModel.DataAnnotations;
using CinemaSystem.Domain.Constants;

namespace CinemaSystem.Contracts.Vouchers;

public sealed class UpdateVoucherRequest
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    [Required]
    [RegularExpression(DomainConstants.VoucherStatus.Active + "|" + DomainConstants.VoucherStatus.Inactive, 
        ErrorMessage = "VoucherStatus must be '" + DomainConstants.VoucherStatus.Active + "' or '" + DomainConstants.VoucherStatus.Inactive + "'.")]
    public string VoucherStatus { get; init; } = DomainConstants.VoucherStatus.Active;

    [Range(0, double.MaxValue)]
    public decimal? MinOrderAmount { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal? MaxDiscountAmount { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int UsageLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int? PerCustomerLimit { get; init; }

    [Required]
    public DateTime StartDate { get; init; }

    [Required]
    public DateTime EndDate { get; init; }

    public string? Category { get; init; }

    public string? ApplicableScope { get; init; }

    public string? TargetType { get; init; }

    public string? TargetCustomerIds { get; init; }

    public string? SpecificFbItemIds { get; init; }
}
