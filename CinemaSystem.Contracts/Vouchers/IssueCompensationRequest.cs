using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Vouchers;

public sealed class IssueCompensationRequest
{
    [Required]
    public string VoucherId { get; init; } = string.Empty;

    [Required]
    public List<string> CustomerProfileIds { get; init; } = new();

    public string? Reason { get; init; }
}
