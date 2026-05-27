using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class CinemaFbInventory
{
    public string CinemaInventoryId { get; set; } = null!;

    public string CinemaId { get; set; } = null!;

    public string FbItemId { get; set; } = null!;

    public int Quantity { get; set; }

    public virtual Cinema Cinema { get; set; } = null!;

    public virtual FbItem FbItem { get; set; } = null!;
}
