using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Seats;

public sealed class UpsertSeatTypeRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string TypeName { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal ExtraFee { get; init; }

    [Range(1, 2)]
    public int SeatSpan { get; init; } = 1;

    public bool IsActive { get; init; } = true;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }
}
