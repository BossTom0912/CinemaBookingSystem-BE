using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class CustomerProfile
{
    public string CustomerProfileId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string MemberLevel { get; set; } = null!;

    public int RewardPoints { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? IdentityCard { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<RewardPointTransaction> RewardPointTransactions { get; set; } = new List<RewardPointTransaction>();

    public virtual ICollection<RefundClaim> RefundClaims { get; set; } = new List<RefundClaim>();

    public virtual ICollection<CustomerRefundRequest> CustomerRefundRequests { get; set; } = new List<CustomerRefundRequest>();

    public virtual ICollection<CancellationCompensation> CancellationCompensations { get; set; } = new List<CancellationCompensation>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
}
