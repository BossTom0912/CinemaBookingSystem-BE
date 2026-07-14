using System;

namespace CinemaSystem.Domain.Entities;

public class CustomerVoucher
{
    public string CustomerVoucherId { get; set; } = null!;
    public string CustomerProfileId { get; set; } = null!;
    public string VoucherId { get; set; } = null!;
    public DateTime ClaimedAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    public virtual CustomerProfile CustomerProfile { get; set; } = null!;
    public virtual Voucher Voucher { get; set; } = null!;
}
