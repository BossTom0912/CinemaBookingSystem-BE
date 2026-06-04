using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class Booking
{
    public string BookingId { get; set; } = null!;

    public string? CustomerProfileId { get; set; }

    public string ShowtimeId { get; set; } = null!;

    public string BookingStatus { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public string? CreatedByStaffProfileId { get; set; }

    public string BookingChannel { get; set; } = null!;

    public string? GuestName { get; set; }

    public string? GuestPhone { get; set; }

    public string? GuestEmail { get; set; }

    public virtual ICollection<BookingFbItem> BookingFbItems { get; set; } = new List<BookingFbItem>();

    public virtual ICollection<BookingSeat> BookingSeats { get; set; } = new List<BookingSeat>();

    public virtual StaffProfile? CreatedByStaffProfile { get; set; }

    public virtual CustomerProfile? CustomerProfile { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual Review? Review { get; set; }

    public virtual ICollection<RewardPointTransaction> RewardPointTransactions { get; set; } = new List<RewardPointTransaction>();

    public virtual Showtime Showtime { get; set; } = null!;

    public virtual VoucherUsage? VoucherUsage { get; set; }
}
