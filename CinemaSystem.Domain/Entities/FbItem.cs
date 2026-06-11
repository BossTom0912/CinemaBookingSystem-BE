using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class FbItem
{
    public string FbItemId { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public decimal Price { get; set; }

    public string ItemStatus { get; set; } = null!;

    public virtual ICollection<BookingFbItem> BookingFbItems { get; set; } = new List<BookingFbItem>();

    public virtual ICollection<CinemaFbInventory> CinemaFbInventories { get; set; } = new List<CinemaFbInventory>();
}
