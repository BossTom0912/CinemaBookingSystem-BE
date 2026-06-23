using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Dashboard;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Reports;

public sealed class ManagerDashboardService : IManagerDashboardService
{
    private readonly CinemaDbContext _dbContext;

    public ManagerDashboardService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ManagerDashboardOverviewResponse>> GetOverviewAsync(
        string? cinemaScopeId,
        ManagerDashboardOverviewQueryRequest request,
        CancellationToken cancellationToken)
    {
        var from = EnsureUtc(request.From);
        var to = EnsureUtc(request.To);
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return ServiceResult<ManagerDashboardOverviewResponse>.Fail(
                400,
                "From date must be earlier than or equal to To date.",
                "INVALID_DATE_RANGE");
        }

        var cinema = await ResolveCinemaAsync(cinemaScopeId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cinemaScopeId) && cinema is null)
        {
            return ServiceResult<ManagerDashboardOverviewResponse>.Fail(
                404,
                "Cinema was not found.",
                "CINEMA_NOT_FOUND");
        }

        var grossRevenueQuery = ApplyPaymentShowtimeScope(
            _dbContext.Payments.AsNoTracking()
                .Where(item => item.PaymentStatus == BookingConstants.PaymentStatus.Success),
            cinemaScopeId,
            from,
            to);

        var refundedAmountQuery = ApplyRefundShowtimeScope(
            _dbContext.Refunds.AsNoTracking()
                .Where(item => item.RefundStatus == BookingConstants.RefundStatus.Success),
            cinemaScopeId,
            from,
            to);

        var ticketsSoldQuery = ApplyTicketShowtimeScope(
            _dbContext.Tickets.AsNoTracking()
                .Where(item =>
                    item.TicketStatus != BookingConstants.TicketStatus.Cancelled
                    && item.TicketStatus != BookingConstants.TicketStatus.Refunded
                    && item.BookingSeat.Booking.Showtime.Status != BookingConstants.ShowtimeStatus.Cancelled
                    && (item.BookingSeat.Booking.BookingStatus == BookingConstants.BookingStatus.Paid
                        || item.BookingSeat.Booking.BookingStatus == BookingConstants.BookingStatus.Completed)),
            cinemaScopeId,
            from,
            to);

        var showtimeSeatsQuery = ApplyShowtimeScope(
                _dbContext.Showtimes.AsNoTracking()
                    .Where(item => item.Status != BookingConstants.ShowtimeStatus.Cancelled),
                cinemaScopeId,
                from,
                to)
            .SelectMany(item => item.ShowtimeSeats);

        var grossRevenue = await grossRevenueQuery
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var refundedAmount = await refundedAmountQuery
            .SumAsync(item => (decimal?)item.RefundAmount, cancellationToken) ?? 0m;
        var ticketsSold = await ticketsSoldQuery.CountAsync(cancellationToken);
        var totalShowtimeSeats = await showtimeSeatsQuery.CountAsync(cancellationToken);

        var response = new ManagerDashboardOverviewResponse
        {
            CinemaId = cinema?.CinemaId,
            CinemaName = cinema?.CinemaName,
            From = from,
            To = to,
            GrossRevenue = grossRevenue,
            RefundedAmount = refundedAmount,
            TotalRevenue = grossRevenue - refundedAmount,
            TicketsSold = ticketsSold,
            TotalShowtimeSeats = totalShowtimeSeats,
            RoomOccupancyRate = CalculateOccupancyRate(ticketsSold, totalShowtimeSeats)
        };

        return ServiceResult<ManagerDashboardOverviewResponse>.Ok(
            response,
            "Manager dashboard overview retrieved successfully.");
    }

    private async Task<CinemaInfo?> ResolveCinemaAsync(
        string? cinemaScopeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cinemaScopeId))
        {
            return null;
        }

        return await _dbContext.Cinemas
            .AsNoTracking()
            .Where(item => item.CinemaId == cinemaScopeId)
            .Select(item => new CinemaInfo(item.CinemaId, item.CinemaName))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<Showtime> ApplyShowtimeScope(
        IQueryable<Showtime> query,
        string? cinemaScopeId,
        DateTime? from,
        DateTime? to)
    {
        if (!string.IsNullOrWhiteSpace(cinemaScopeId))
        {
            query = query.Where(item => item.Room.CinemaId == cinemaScopeId);
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.StartTime <= to.Value);
        }

        return query;
    }

    private static IQueryable<Payment> ApplyPaymentShowtimeScope(
        IQueryable<Payment> query,
        string? cinemaScopeId,
        DateTime? from,
        DateTime? to)
    {
        if (!string.IsNullOrWhiteSpace(cinemaScopeId))
        {
            query = query.Where(item => item.Booking.Showtime.Room.CinemaId == cinemaScopeId);
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.Booking.Showtime.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.Booking.Showtime.StartTime <= to.Value);
        }

        return query;
    }

    private static IQueryable<Refund> ApplyRefundShowtimeScope(
        IQueryable<Refund> query,
        string? cinemaScopeId,
        DateTime? from,
        DateTime? to)
    {
        if (!string.IsNullOrWhiteSpace(cinemaScopeId))
        {
            query = query.Where(item => item.Booking.Showtime.Room.CinemaId == cinemaScopeId);
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.Booking.Showtime.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.Booking.Showtime.StartTime <= to.Value);
        }

        return query;
    }

    private static IQueryable<Ticket> ApplyTicketShowtimeScope(
        IQueryable<Ticket> query,
        string? cinemaScopeId,
        DateTime? from,
        DateTime? to)
    {
        if (!string.IsNullOrWhiteSpace(cinemaScopeId))
        {
            query = query.Where(item => item.BookingSeat.Booking.Showtime.Room.CinemaId == cinemaScopeId);
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.BookingSeat.Booking.Showtime.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.BookingSeat.Booking.Showtime.StartTime <= to.Value);
        }

        return query;
    }

    private static decimal CalculateOccupancyRate(int ticketsSold, int totalShowtimeSeats)
    {
        if (totalShowtimeSeats <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)ticketsSold / totalShowtimeSeats * 100m, 2, MidpointRounding.AwayFromZero);
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

    private sealed record CinemaInfo(string CinemaId, string CinemaName);
}
