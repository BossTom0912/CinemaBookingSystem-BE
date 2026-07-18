using System;

namespace CinemaSystem.Domain.Entities;

public class CompensationCombo
{
    public string CompensationComboId { get; set; } = null!;

    public string CancellationCompensationId { get; set; } = null!;

    public string VoucherCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? RedeemedAt { get; set; }

    public string? RedeemedAtCinemaId { get; set; }

    public string? RedeemedByStaffProfileId { get; set; }

    public byte[]? RowVersion { get; set; }

    public virtual CancellationCompensation CancellationCompensation { get; set; } = null!;

    public virtual Cinema? RedeemedAtCinema { get; set; }

    public virtual StaffProfile? RedeemedByStaffProfile { get; set; }
}
