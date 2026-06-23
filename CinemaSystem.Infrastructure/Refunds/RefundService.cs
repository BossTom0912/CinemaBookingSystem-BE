using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Refunds;

public sealed class RefundService : IRefundService
{
    private static readonly HashSet<string> ValidRefundStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        BookingConstants.RefundStatus.Pending,
        BookingConstants.RefundStatus.Success,
        BookingConstants.RefundStatus.Failed,
        BookingConstants.RefundStatus.ManualRequired
    };

    private readonly CinemaDbContext _dbContext;

    public RefundService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<IReadOnlyList<RefundResponse>>> GetRefundsAsync(
        string? cinemaScopeId,
        RefundQueryRequest request,
        CancellationToken cancellationToken)
    {
        var status = NormalizeStatus(request.Status);
        if (status is not null && !ValidRefundStatuses.Contains(status))
        {
            return ServiceResult<IReadOnlyList<RefundResponse>>.Fail(
                400,
                "Refund status is invalid.",
                "INVALID_REFUND_STATUS");
        }

        var from = EnsureUtc(request.From);
        var to = EnsureUtc(request.To);
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return ServiceResult<IReadOnlyList<RefundResponse>>.Fail(
                400,
                "From date must be earlier than or equal to To date.",
                "INVALID_DATE_RANGE");
        }

        var showtimeId = NormalizeId(request.ShowtimeId);

        var query = _dbContext.Refunds.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(cinemaScopeId))
        {
            query = query.Where(item =>
                item.Booking.Showtime.Room.CinemaId == cinemaScopeId);
        }

        if (status is not null)
        {
            query = query.Where(item => item.RefundStatus == status);
        }

        if (showtimeId is not null)
        {
            query = query.Where(item => item.Booking.ShowtimeId == showtimeId);
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.RequestedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.RequestedAt <= to.Value);
        }

        var refunds = await query
            .OrderByDescending(item => item.RequestedAt)
            .Select(item => new RefundResponse
            {
                RefundId = item.RefundId,
                BookingId = item.BookingId,
                PaymentId = item.PaymentId,
                ShowtimeId = item.Booking.ShowtimeId,
                MovieTitle = item.Booking.Showtime.Movie.Title,
                CinemaId = item.Booking.Showtime.Room.CinemaId,
                CinemaName = item.Booking.Showtime.Room.Cinema.CinemaName,
                RefundAmount = item.RefundAmount,
                RefundStatus = item.RefundStatus,
                RefundReason = item.RefundReason,
                FailureReason = item.FailureReason,
                RequestedAt = item.RequestedAt,
                RefundedAt = item.RefundedAt
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<RefundResponse>>.Ok(
            refunds,
            "Refunds retrieved successfully.");
    }

    private static string? NormalizeStatus(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeId(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static DateTime? EnsureUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
