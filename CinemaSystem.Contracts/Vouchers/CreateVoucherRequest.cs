using System;
using System.ComponentModel.DataAnnotations;
using CinemaSystem.Domain.Constants;

namespace CinemaSystem.Contracts.Vouchers;

public sealed class CreateVoucherRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string VoucherCode { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    [Required]
    [RegularExpression(DomainConstants.DiscountType.Amount + "|" + DomainConstants.DiscountType.Percent, 
        ErrorMessage = "DiscountType must be '" + DomainConstants.DiscountType.Amount + "' or '" + DomainConstants.DiscountType.Percent + "'.")]
    public string DiscountType { get; init; } = DomainConstants.DiscountType.Amount;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "DiscountValue must be greater than zero.")]
    public decimal DiscountValue { get; init; }

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

    public string Category { get; init; } = "EVENT"; // EVENT, FOOD_BEVERAGE, COMPENSATION

    public string ApplicableScope { get; init; } = "TOTAL_ORDER"; // TOTAL_ORDER, TICKET_ONLY, FOOD_BEVERAGE_ONLY

    public string TargetType { get; init; } = "ALL_CUSTOMERS"; // ALL_CUSTOMERS, SPECIFIC_CUSTOMERS

    public string? TargetCustomerIds { get; init; }

    public string? SpecificFbItemIds { get; init; }
}
