namespace CinemaSystem.Domain.Entities;

public sealed class CustomerRefundRequest
{
    public string CustomerRefundRequestId { get; set; } = null!;
    public string RefundId { get; set; } = null!;
    public string CustomerProfileId { get; set; } = null!;
    public string? TicketId { get; set; }
    public string RequestReason { get; set; } = null!;
    public string RequestStatus { get; set; } = null!;
    public string? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Refund Refund { get; set; } = null!;
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public Ticket? Ticket { get; set; }
    public User? ProcessedByUser { get; set; }
}
