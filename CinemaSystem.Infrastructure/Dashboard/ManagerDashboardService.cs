using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Dashboard;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Dashboard;

public sealed class ManagerDashboardService : IManagerDashboardService
{
    private readonly CinemaDbContext _dbContext;

    public ManagerDashboardService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ManagerDashboardResponse>> GetDashboardAsync(
        string? cinemaScopeId,
        ManagerDashboardQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.From.HasValue || !request.To.HasValue)
        {
            return ServiceResult<ManagerDashboardResponse>.Fail(
                400,
                "From and to are required.",
                "DATE_RANGE_REQUIRED");
        }

        var from = EnsureUtc(request.From.Value);
        var to = EnsureUtc(request.To.Value);
        if (from >= to)
        {
            return ServiceResult<ManagerDashboardResponse>.Fail(
                400,
                "From must be earlier than to.",
                "INVALID_DATE_RANGE");
        }

        var movieId = Normalize(request.MovieId);
        var cinemaName = await GetCinemaNameAsync(cinemaScopeId, cancellationToken);
        if (cinemaScopeId is not null && cinemaName is null)
        {
            return ServiceResult<ManagerDashboardResponse>.Fail(
                404,
                "Cinema was not found.",
                "CINEMA_NOT_FOUND");
        }

        var payments = _dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.PaymentStatus == BookingConstants.PaymentStatus.Success
                && payment.Booking.Showtime.StartTime >= from
                && payment.Booking.Showtime.StartTime < to);

        if (cinemaScopeId is not null)
        {
            payments = payments.Where(payment =>
                payment.Booking.Showtime.Room.CinemaId == cinemaScopeId);
        }

        if (movieId is not null)
        {
            payments = payments.Where(payment =>
                payment.Booking.Showtime.MovieId == movieId);
        }

        var grossRevenue = await payments
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;

        var refunds = _dbContext.Refunds
            .AsNoTracking()
            .Where(refund =>
                refund.Payment.PaymentStatus == BookingConstants.PaymentStatus.Success
                && refund.Booking.Showtime.StartTime >= from
                && refund.Booking.Showtime.StartTime < to);

        if (cinemaScopeId is not null)
        {
            refunds = refunds.Where(refund =>
                refund.Booking.Showtime.Room.CinemaId == cinemaScopeId);
        }

        if (movieId is not null)
        {
            refunds = refunds.Where(refund =>
                refund.Booking.Showtime.MovieId == movieId);
        }

        var refundTotals = await refunds
            .Where(refund =>
                refund.RefundStatus == BookingConstants.RefundStatus.Success
                || refund.RefundStatus == BookingConstants.RefundStatus.Pending
                || refund.RefundStatus == BookingConstants.RefundStatus.ManualRequired)
            .GroupBy(refund => refund.RefundStatus)
            .Select(group => new RefundStatusTotal(
                group.Key,
                group.Sum(refund => refund.RefundAmount)))
            .ToListAsync(cancellationToken);

        var refundedAmount = GetRefundTotal(
            refundTotals,
            BookingConstants.RefundStatus.Success);
        var pendingRefundAmount = GetRefundTotal(
            refundTotals,
            BookingConstants.RefundStatus.Pending);
        var manualRefundAmount = GetRefundTotal(
            refundTotals,
            BookingConstants.RefundStatus.ManualRequired);

        var soldBookingSeats = _dbContext.BookingSeats
            .AsNoTracking()
            .Where(bookingSeat =>
                bookingSeat.Booking.Showtime.StartTime >= from
                && bookingSeat.Booking.Showtime.StartTime < to
                && bookingSeat.Booking.Payments.Any(payment =>
                    payment.PaymentStatus == BookingConstants.PaymentStatus.Success));

        if (cinemaScopeId is not null)
        {
            soldBookingSeats = soldBookingSeats.Where(bookingSeat =>
                bookingSeat.Booking.Showtime.Room.CinemaId == cinemaScopeId);
        }

        if (movieId is not null)
        {
            soldBookingSeats = soldBookingSeats.Where(bookingSeat =>
                bookingSeat.Booking.Showtime.MovieId == movieId);
        }

        var grossTicketsSold = await soldBookingSeats.CountAsync(cancellationToken);
        var refundedTickets = await soldBookingSeats
            .Where(bookingSeat => bookingSeat.Booking.Payments.Any(payment =>
                payment.PaymentStatus == BookingConstants.PaymentStatus.Success
                && (payment.Refunds
                    .Where(refund =>
                        refund.RefundStatus == BookingConstants.RefundStatus.Success)
                    .Sum(refund => (decimal?)refund.RefundAmount) ?? 0m) >= payment.Amount))
            .CountAsync(cancellationToken);

        var eligibleShowtimeSeats = _dbContext.ShowtimeSeats
            .AsNoTracking()
            .Where(showtimeSeat =>
                showtimeSeat.Showtime.StartTime >= from
                && showtimeSeat.Showtime.StartTime < to
                && showtimeSeat.Showtime.Status != BookingConstants.ShowtimeStatus.Cancelled
                && showtimeSeat.SeatStatus != BookingConstants.ShowtimeSeatStatus.Unavailable);

        if (cinemaScopeId is not null)
        {
            eligibleShowtimeSeats = eligibleShowtimeSeats.Where(showtimeSeat =>
                showtimeSeat.Showtime.Room.CinemaId == cinemaScopeId);
        }

        if (movieId is not null)
        {
            eligibleShowtimeSeats = eligibleShowtimeSeats.Where(showtimeSeat =>
                showtimeSeat.Showtime.MovieId == movieId);
        }

        var sellableSeatCapacity = await eligibleShowtimeSeats.CountAsync(cancellationToken);
        var occupiedSeats = await eligibleShowtimeSeats
            .Where(showtimeSeat =>
                showtimeSeat.BookingSeat != null
                && showtimeSeat.BookingSeat.Booking.Payments.Any(payment =>
                    payment.PaymentStatus == BookingConstants.PaymentStatus.Success
                    && (payment.Refunds
                        .Where(refund =>
                            refund.RefundStatus == BookingConstants.RefundStatus.Success)
                        .Sum(refund => (decimal?)refund.RefundAmount) ?? 0m) < payment.Amount))
            .CountAsync(cancellationToken);

        var netTicketsSold = grossTicketsSold - refundedTickets;
        var occupancyRate = sellableSeatCapacity == 0
            ? 0m
            : Math.Round(
                occupiedSeats * BookingConstants.ManagerDashboard.PercentageMultiplier
                    / sellableSeatCapacity,
                BookingConstants.ManagerDashboard.OccupancyRateDecimalPlaces,
                MidpointRounding.AwayFromZero);

        return ServiceResult<ManagerDashboardResponse>.Ok(
            new ManagerDashboardResponse
            {
                CinemaId = cinemaScopeId,
                CinemaName = cinemaName ?? BookingConstants.ManagerDashboard.AllCinemasLabel,
                From = from,
                To = to,
                MovieId = movieId,
                GrossRevenue = grossRevenue,
                RefundedAmount = refundedAmount,
                PendingRefundAmount = pendingRefundAmount,
                ManualRefundAmount = manualRefundAmount,
                NetRevenue = grossRevenue - refundedAmount,
                GrossTicketsSold = grossTicketsSold,
                RefundedTickets = refundedTickets,
                NetTicketsSold = netTicketsSold,
                SellableSeatCapacity = sellableSeatCapacity,
                OccupiedSeats = occupiedSeats,
                OccupancyRate = occupancyRate
            },
            "Manager dashboard retrieved successfully.");
    }

    private async Task<string?> GetCinemaNameAsync(
        string? cinemaScopeId,
        CancellationToken cancellationToken)
    {
        if (cinemaScopeId is null)
        {
            return BookingConstants.ManagerDashboard.AllCinemasLabel;
        }

        return await _dbContext.Cinemas
            .AsNoTracking()
            .Where(cinema => cinema.CinemaId == cinemaScopeId)
            .Select(cinema => cinema.CinemaName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal GetRefundTotal(
        IReadOnlyList<RefundStatusTotal> totals,
        string status)
    {
        return totals
            .Where(item => item.Status == status)
            .Select(item => item.Amount)
            .FirstOrDefault();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private sealed record RefundStatusTotal(string Status, decimal Amount);
}
