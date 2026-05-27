using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class RewardPointTransaction
{
    public string RewardTransactionId { get; set; } = null!;

    public string CustomerProfileId { get; set; } = null!;

    public string? BookingId { get; set; }

    public string TransactionType { get; set; } = null!;

    public int Points { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual CustomerProfile CustomerProfile { get; set; } = null!;
}
