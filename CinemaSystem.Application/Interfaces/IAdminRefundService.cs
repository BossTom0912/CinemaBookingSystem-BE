using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Application.Interfaces;

public interface IAdminRefundService
{
    Task<ServiceResult<bool>> CancelShowtimesAndRefundAsync(string[] showtimeIds, string reason, CancellationToken cancellationToken);
    Task<ServiceResult<PagedList<RefundDto>>> GetRefundsAsync(string status, int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> ConfirmRefundAsync(string bookingId, string adminUserId, CancellationToken cancellationToken);
}

public class RefundDto
{
    public string BookingId { get; set; } = string.Empty;
    public string ShowtimeId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string RefundReason { get; set; } = string.Empty;
    public string BookingStatus { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
}
