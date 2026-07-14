using System;
using System.Collections.Generic;

using CinemaSystem.Domain.Constants;

namespace CinemaSystem.Domain.Entities;

public partial class Booking
{
    public string BookingId { get; set; } = null!;

    public string? CustomerProfileId { get; set; }

    public string? ShowtimeId { get; set; }

    public string FbFulfillmentStatus { get; set; } = FbConstants.FulfillmentStatus.NotRequired;

    public DateTime? FbFulfilledAt { get; set; }

    public string? FbFulfilledByStaffProfileId { get; set; }

    public string BookingStatus { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public Guid? ClientRequestId { get; set; }

    public string? RequestFingerprint { get; set; }

    public string? CreatedByStaffProfileId { get; set; }

    public string BookingChannel { get; set; } = null!;

    public string? GuestName { get; set; }

    public string? GuestPhone { get; set; }

    public string? GuestEmail { get; set; }

    public virtual ICollection<BookingFbItem> BookingFbItems { get; set; } = new List<BookingFbItem>();

    public virtual ICollection<BookingSeat> BookingSeats { get; set; } = new List<BookingSeat>();

    public virtual StaffProfile? CreatedByStaffProfile { get; set; }

    public virtual StaffProfile? FbFulfilledByStaffProfile { get; set; }

    public virtual CustomerProfile? CustomerProfile { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual Review? Review { get; set; }

    public virtual ICollection<RewardPointTransaction> RewardPointTransactions { get; set; } = new List<RewardPointTransaction>();

    public virtual Showtime? Showtime { get; set; }

    public virtual VoucherUsage? VoucherUsage { get; set; }
}
