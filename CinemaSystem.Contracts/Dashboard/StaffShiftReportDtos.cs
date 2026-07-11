using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Dashboard;

public sealed class StaffShiftReportQueryRequest
{
    [Required]
    public DateTime? From { get; init; }

    [Required]
    public DateTime? To { get; init; }

    public string? StaffProfileId { get; init; }

    public string? CinemaId { get; init; }
}

public sealed class StaffShiftReportResponse
{
    public string? StaffProfileId { get; init; }

    public string StaffName { get; init; } = string.Empty;

    public string? CinemaId { get; init; }

    public string CinemaName { get; init; } = string.Empty;

    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public int CheckedInTicketCount { get; init; }

    public int FulfilledFbOrderCount { get; init; }

    public int CounterFbOrderCount { get; init; }

    public int OnlineFulfilledFbOrderCount { get; init; }

    public decimal TotalCounterRevenue { get; init; }

    public decimal CashRevenue { get; init; }

    public decimal TransferRevenue { get; init; }

    public decimal UnclassifiedCounterRevenue { get; init; }

    public List<StaffShiftReportTransactionResponse> Transactions { get; init; } = new();
}

public sealed class StaffShiftReportTransactionResponse
{
    public DateTime OccurredAt { get; init; }

    public string TransactionType { get; init; } = string.Empty;

    public string ReferenceId { get; init; } = string.Empty;

    public string? StaffProfileId { get; init; }

    public string StaffName { get; init; } = string.Empty;

    public string? CinemaId { get; init; }

    public string CinemaName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string? PaymentMethod { get; init; }

    public string Description { get; init; } = string.Empty;
}
