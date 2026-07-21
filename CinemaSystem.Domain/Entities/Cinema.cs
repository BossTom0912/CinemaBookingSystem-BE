using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Cinema
{
    public string CinemaId { get; set; } = null!;

    public string CinemaName { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string City { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string CinemaStatus { get; set; } = null!;

    public virtual ICollection<CinemaFbInventory> CinemaFbInventories { get; set; } = new List<CinemaFbInventory>();

    public virtual ICollection<CompensationCombo> RedeemedCompensationCombos { get; set; } = new List<CompensationCombo>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    public virtual ICollection<StaffProfile> StaffProfiles { get; set; } = new List<StaffProfile>();
}
