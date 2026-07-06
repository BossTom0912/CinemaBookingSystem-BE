using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Dashboard;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly CinemaDbContext _dbContext;

    public DashboardService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// API 1: Thẻ chỉ số tổng quan (Overview).
    /// Gross Revenue, Net Revenue, Total Refunds, AOV, Total Tickets Sold.
    /// </summary>
    public async Task<DashboardOverviewResponse> GetOverviewAsync(
        DashboardFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        var query = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => (b.BookingStatus == DomainConstants.EntityStatus.Paid ||
                         b.BookingStatus == DomainConstants.EntityStatus.Completed) &&
                        b.CreatedAt >= fromDate && b.CreatedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(request.CinemaId))
        {
            query = query.Where(b => b.Showtime != null && b.Showtime.Room != null && b.Showtime.Room.CinemaId == request.CinemaId);
        }

        var grossRevenue = await query.SumAsync(b => (decimal?)b.TotalAmount, cancellationToken) ?? 0m;

        // Tính tổng tiền hoàn của các booking trong phạm vi lọc
        var bookingIdsQuery = query.Select(b => b.BookingId);
        var totalRefunds = await _dbContext.Refunds
            .AsNoTracking()
            .Where(r => bookingIdsQuery.Contains(r.BookingId) && r.RefundStatus == "COMPLETED")
            .SumAsync(r => (decimal?)r.RefundAmount, cancellationToken) ?? 0m;

        var netRevenue = grossRevenue - totalRefunds;

        var totalSuccessfulBookings = await query.CountAsync(cancellationToken);

        var totalTicketsSold = await query
            .SelectMany(b => b.BookingSeats)
            .CountAsync(cancellationToken);

        var aov = totalSuccessfulBookings > 0
            ? Math.Round(netRevenue / totalSuccessfulBookings, 2)
            : 0m;

        return new DashboardOverviewResponse
        {
            GrossRevenue = grossRevenue,
            TotalRefunds = totalRefunds,
            NetRevenue = netRevenue,
            AverageOrderValue = aov,
            TotalTicketsSold = totalTicketsSold,
            TotalSuccessfulBookings = totalSuccessfulBookings
        };
    }

    /// <summary>
    /// API 2: Xu hướng doanh thu theo mốc thời gian (Tuần ISO hoặc Tháng/Năm).
    /// </summary>
    public async Task<List<RevenueTrendItemResponse>> GetRevenueTrendsAsync(
        RevenueTrendRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        var query = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => (b.BookingStatus == DomainConstants.EntityStatus.Paid ||
                         b.BookingStatus == DomainConstants.EntityStatus.Completed) &&
                        b.CreatedAt >= fromDate && b.CreatedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(request.CinemaId))
        {
            query = query.Where(b => b.Showtime != null && b.Showtime.Room != null && b.Showtime.Room.CinemaId == request.CinemaId);
        }

        var bookingsList = await query
            .Select(b => new
            {
                b.BookingId,
                b.CreatedAt,
                b.TotalAmount
            })
            .ToListAsync(cancellationToken);

        var bookingIds = bookingsList.Select(b => b.BookingId).ToList();
        var refunds = await _dbContext.Refunds
            .AsNoTracking()
            .Where(r => bookingIds.Contains(r.BookingId) && r.RefundStatus == "COMPLETED")
            .Select(r => new { r.BookingId, r.RefundAmount })
            .ToListAsync(cancellationToken);

        var refundMap = refunds
            .GroupBy(r => r.BookingId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.RefundAmount));

        var isWeekly = string.Equals(request.Period, "week", StringComparison.OrdinalIgnoreCase);

        var groupedData = bookingsList
            .GroupBy(b => isWeekly
                ? GetIsoWeekLabel(b.CreatedAt)
                : b.CreatedAt.ToString("MM/yyyy"))
            .Select(g =>
            {
                var gross = g.Sum(b => b.TotalAmount);
                var refundAmt = g.Sum(b => refundMap.TryGetValue(b.BookingId, out var amt) ? amt : 0m);
                var net = gross - refundAmt;

                return new RevenueTrendItemResponse
                {
                    Label = g.Key,
                    GrossRevenue = gross,
                    NetRevenue = net,
                    BookingCount = g.Count()
                };
            })
            .ToList();

        return groupedData;
    }

    /// <summary>
    /// API 3: Xếp hạng Top 3 phim có doanh thu cao nhất.
    /// </summary>
    public async Task<List<MovieRankingResponse>> GetTop3MoviesRankingAsync(
        DashboardFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        var query = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => (b.BookingStatus == DomainConstants.EntityStatus.Paid ||
                         b.BookingStatus == DomainConstants.EntityStatus.Completed) &&
                        b.CreatedAt >= fromDate && b.CreatedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(request.CinemaId))
        {
            query = query.Where(b => b.Showtime != null && b.Showtime.Room != null && b.Showtime.Room.CinemaId == request.CinemaId);
        }

        var topMovies = await query
            .Where(b => b.Showtime != null && b.Showtime.Movie != null)
            .SelectMany(b => b.BookingSeats, (b, seat) => new
            {
                MovieId = b.Showtime!.MovieId,
                Title = b.Showtime.Movie.Title,
                SeatPrice = seat.SeatPrice
            })
            .GroupBy(m => new { m.MovieId, m.Title })
            .Select(g => new MovieRankingResponse
            {
                MovieId = g.Key.MovieId,
                MovieTitle = g.Key.Title,
                TicketRevenue = g.Sum(x => x.SeatPrice),
                TicketsSold = g.Count()
            })
            .OrderByDescending(m => m.TicketRevenue)
            .Take(3)
            .ToListAsync(cancellationToken);

        return topMovies;
    }

    /// <summary>
    /// API 4: Tỷ lệ lấp đầy rạp (Occupancy Rate %) & Bóc tách doanh thu Bắp nước (F&B) vs Vé.
    /// </summary>
    public async Task<OccupancyAndFbBreakdownResponse> GetOccupancyAndFbBreakdownAsync(
        DashboardFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        // 1. Tính tổng số ghế khả dụng từ các suất chiếu trong khoảng thời gian
        var showtimeQuery = _dbContext.Showtimes
            .AsNoTracking()
            .Where(s => s.StartTime >= fromDate && s.StartTime <= toDate);

        if (!string.IsNullOrWhiteSpace(request.CinemaId))
        {
            showtimeQuery = showtimeQuery.Where(s => s.Room != null && s.Room.CinemaId == request.CinemaId);
        }

        var totalAvailableSeatsCapacity = await showtimeQuery
            .Select(s => s.Room.Seats.Count)
            .SumAsync(cancellationToken);

        // 2. Tính tổng số ghế đã bán & doanh thu vé / F&B
        var bookingQuery = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => (b.BookingStatus == DomainConstants.EntityStatus.Paid ||
                         b.BookingStatus == DomainConstants.EntityStatus.Completed) &&
                        b.CreatedAt >= fromDate && b.CreatedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(request.CinemaId))
        {
            bookingQuery = bookingQuery.Where(b => b.Showtime != null && b.Showtime.Room != null && b.Showtime.Room.CinemaId == request.CinemaId);
        }

        var totalSoldSeats = await bookingQuery
            .SelectMany(b => b.BookingSeats)
            .CountAsync(cancellationToken);

        var ticketRevenue = await bookingQuery
            .SelectMany(b => b.BookingSeats)
            .SumAsync(s => (decimal?)s.SeatPrice, cancellationToken) ?? 0m;

        var fbRevenue = await bookingQuery
            .SelectMany(b => b.BookingFbItems)
            .SumAsync(item => (decimal?)item.Subtotal, cancellationToken) ?? 0m;

        var totalRev = ticketRevenue + fbRevenue;

        var occupancyRate = totalAvailableSeatsCapacity > 0
            ? Math.Round((double)totalSoldSeats / totalAvailableSeatsCapacity * 100, 2)
            : 0;

        var fbPercentage = totalRev > 0
            ? Math.Round((double)(fbRevenue / totalRev) * 100, 2)
            : 0;

        return new OccupancyAndFbBreakdownResponse
        {
            OccupancyRate = occupancyRate,
            TotalSoldSeats = totalSoldSeats,
            TotalAvailableSeatsCapacity = totalAvailableSeatsCapacity,
            TicketRevenue = ticketRevenue,
            FbRevenue = fbRevenue,
            FbRevenuePercentage = fbPercentage
        };
    }

    /// <summary>
    /// API 5: Phân loại tỷ lệ kênh bán hàng (Online vs Counter Key-Value).
    /// </summary>
    public async Task<List<SalesChannelBreakdownResponse>> GetSalesChannelBreakdownAsync(
        DashboardFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        var query = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => (b.BookingStatus == DomainConstants.EntityStatus.Paid ||
                         b.BookingStatus == DomainConstants.EntityStatus.Completed) &&
                        b.CreatedAt >= fromDate && b.CreatedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(request.CinemaId))
        {
            query = query.Where(b => b.Showtime != null && b.Showtime.Room != null && b.Showtime.Room.CinemaId == request.CinemaId);
        }

        var channelGroups = await query
            .GroupBy(b => b.BookingChannel)
            .Select(g => new
            {
                Channel = g.Key,
                TotalRevenue = g.Sum(b => b.TotalAmount),
                BookingCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        var combinedRevenue = channelGroups.Sum(c => c.TotalRevenue);

        var result = channelGroups.Select(c => new SalesChannelBreakdownResponse
        {
            Channel = c.Channel,
            ChannelLabel = string.Equals(c.Channel, "ONLINE", StringComparison.OrdinalIgnoreCase)
                ? "Trực tuyến (Online)"
                : "Tại quầy (Counter)",
            TotalRevenue = c.TotalRevenue,
            BookingCount = c.BookingCount,
            Percentage = combinedRevenue > 0
                ? Math.Round((double)(c.TotalRevenue / combinedRevenue) * 100, 2)
                : 0
        }).ToList();

        return result;
    }

    #region Helpers

    private static (DateTime FromDate, DateTime ToDate) ResolveDateRange(DateTime? fromDate, DateTime? toDate)
    {
        var start = fromDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = toDate ?? DateTime.UtcNow;
        return (start, end);
    }

    private static string GetIsoWeekLabel(DateTime date)
    {
        var week = ISOWeek.GetWeekOfYear(date);
        var year = ISOWeek.GetYear(date);
        return $"W{week:D2}/{year}";
    }

    #endregion
}
