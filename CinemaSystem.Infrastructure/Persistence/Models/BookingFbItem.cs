using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class BookingFbItem
{
    public string BookingFbitemId { get; set; } = null!;

    public string BookingId { get; set; } = null!;

    public string FbItemId { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Subtotal { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual FbItem FbItem { get; set; } = null!;
}
