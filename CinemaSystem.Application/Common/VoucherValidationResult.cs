using CinemaSystem.Domain.Entities;

namespace CinemaSystem.Application.Common;

public sealed class VoucherValidationResult
{
    public Voucher Voucher { get; }
    public decimal DiscountAmount { get; }

    public VoucherValidationResult(Voucher voucher, decimal discountAmount)
    {
        Voucher = voucher;
        DiscountAmount = discountAmount;
    }
}
