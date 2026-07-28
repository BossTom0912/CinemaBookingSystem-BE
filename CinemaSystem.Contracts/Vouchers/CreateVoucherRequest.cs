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

    public string Category { get; init; } = DomainConstants.VoucherCategory.Event;

    public string ApplicableScope { get; init; } = DomainConstants.VoucherScope.TotalOrder;

    public string TargetType { get; init; } = DomainConstants.VoucherTargetType.AllCustomers;

    public string? TargetCustomerIds { get; init; }

    public string? SpecificFbItemIds { get; init; }

    public string? ShowtimeId { get; init; }

    public string? RoomId { get; init; }

    public bool IsPrivate { get; init; } = false;

    [Range(1, int.MaxValue, ErrorMessage = "RequiredTicketCount must be greater than zero.")]
    public int? RequiredTicketCount { get; init; }
}
