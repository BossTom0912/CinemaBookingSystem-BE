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
    /// TC-01, TC-02, TC-08, TC-10, TC-11, TC-12, TC-13, TC-14, TC-17.
    /// </summary>
    public async Task<DashboardOverviewResponse> GetOverviewAsync(
        DashboardFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        var bookingQuery = BuildBaseBookingQuery(fromDate, toDate, request.CinemaId);

        var grossRevenue = await bookingQuery
            .SumAsync(b => (decimal?)b.TotalAmount, cancellationToken) ?? 0m;

        // TC-17: Dùng subquery EXISTS thay vì IN (...) để tránh sập SQL Server khi có hàng triệu BookingId
        var totalRefunds = await _dbContext.Refunds
            .AsNoTracking()
            .Where(r => r.RefundStatus == DomainConstants.RefundStatus.Success &&
                        _dbContext.Bookings.Where(b => (b.BookingStatus == DomainConstants.EntityStatus.Paid ||
                                                        b.BookingStatus == DomainConstants.EntityStatus.Completed) &&
                                                       b.CreatedAt >= fromDate && b.CreatedAt <= toDate &&
                                                       (string.IsNullOrEmpty(request.CinemaId) ||
                                                        (b.Showtime != null && b.Showtime.Room != null && b.Showtime.Room.CinemaId == request.CinemaId)))
                            .Any(b => b.BookingId == r.BookingId))
            .SumAsync(r => (decimal?)r.RefundAmount, cancellationToken) ?? 0m;

        var netRevenue = grossRevenue - totalRefunds;

        var totalSuccessfulBookings = await bookingQuery.CountAsync(cancellationToken);

        var totalTicketsSold = await bookingQuery
            .SelectMany(b => b.BookingSeats)
            .CountAsync(cancellationToken);

        // TC-08: Đảm bảo không bị DivideByZeroException khi totalSuccessfulBookings = 0
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
    /// TC-03, TC-04, TC-16 (Fix OutOfMemory: GroupBy trực tiếp trên DB query).
    /// </summary>
    public async Task<List<RevenueTrendItemResponse>> GetRevenueTrendsAsync(
        RevenueTrendRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        var isWeekly = string.Equals(request.Period, DomainConstants.DashboardPeriod.Week, StringComparison.OrdinalIgnoreCase);

        if (isWeekly)
        {
            // Dự án tuần ISO: Lấy danh sách tổng hợp gộp gọn nhẹ từ DB (chỉ lấy CreatedAt và TotalAmount, BookingId)
            var bookingsData = await BuildBaseBookingQuery(fromDate, toDate, request.CinemaId)
                .Select(b => new
                {
                    b.BookingId,
                    b.CreatedAt,
                    b.TotalAmount
                })
                .ToListAsync(cancellationToken);

            if (bookingsData.Count == 0)
            {
                return new List<RevenueTrendItemResponse>();
            }

            var bookingIds = bookingsData.Select(b => b.BookingId).ToList();

            var refundMap = await _dbContext.Refunds
                .AsNoTracking()
                .Where(r => bookingIds.Contains(r.BookingId) && r.RefundStatus == DomainConstants.RefundStatus.Success)
                .GroupBy(r => r.BookingId)
                .Select(g => new { BookingId = g.Key, TotalRefund = g.Sum(r => r.RefundAmount) })
                .ToDictionaryAsync(x => x.BookingId, x => x.TotalRefund, cancellationToken);

            var weeklyGrouped = bookingsData
                .GroupBy(b => GetIsoWeekLabel(b.CreatedAt))
                .Select(g =>
                {
                    var gross = g.Sum(b => b.TotalAmount);
                    var refund = g.Sum(b => refundMap.TryGetValue(b.BookingId, out var rAmt) ? rAmt : 0m);
                    return new RevenueTrendItemResponse
                    {
                        Label = g.Key,
                        GrossRevenue = gross,
                        NetRevenue = gross - refund,
                        BookingCount = g.Count()
                    };
                })
                .ToList();

            return weeklyGrouped;
        }

        // TC-16: Phân tích Tháng/Năm: Push GroupBy trực tiếp xuống SQL Server engine
        var monthlyGrouped = await BuildBaseBookingQuery(fromDate, toDate, request.CinemaId)
            .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                GrossRevenue = g.Sum(b => b.TotalAmount),
                BookingCount = g.Count(),
                BookingIds = g.Select(b => b.BookingId)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var result = new List<RevenueTrendItemResponse>();
        foreach (var m in monthlyGrouped)
        {
            var totalRefundForMonth = await _dbContext.Refunds
                .AsNoTracking()
                .Where(r => m.BookingIds.Contains(r.BookingId) && r.RefundStatus == DomainConstants.RefundStatus.Success)
                .SumAsync(r => (decimal?)r.RefundAmount, cancellationToken) ?? 0m;

            result.Add(new RevenueTrendItemResponse
            {
                Label = $"{m.Month:D2}/{m.Year}",
                GrossRevenue = m.GrossRevenue,
                NetRevenue = m.GrossRevenue - totalRefundForMonth,
                BookingCount = m.BookingCount
            });
        }

        return result;
    }

    /// <summary>
    /// API 3: Xếp hạng Top 3 phim có doanh thu cao nhất.
    /// TC-05.
    /// </summary>
    public async Task<List<MovieRankingResponse>> GetTop3MoviesRankingAsync(
        DashboardFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        var query = BuildBaseBookingQuery(fromDate, toDate, request.CinemaId);

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
    /// TC-06, TC-09, TC-15, TC-18.
    /// </summary>
    public async Task<OccupancyAndFbBreakdownResponse> GetOccupancyAndFbBreakdownAsync(
        DashboardFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        // TC-18: Sinh câu lệnh COUNT(*) thuần túy trên DB cho dung lượng ghế khả dụng
        var showtimeQuery = _dbContext.Showtimes
            .AsNoTracking()
            .Where(s => s.StartTime >= fromDate && s.StartTime <= toDate);

        if (!string.IsNullOrWhiteSpace(request.CinemaId))
        {
            showtimeQuery = showtimeQuery.Where(s => s.Room != null && s.Room.CinemaId == request.CinemaId);
        }

        var totalAvailableSeatsCapacity = await showtimeQuery
            .SelectMany(s => s.Room.Seats)
            .CountAsync(cancellationToken);

        var bookingQuery = BuildBaseBookingQuery(fromDate, toDate, request.CinemaId);

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

        // TC-09: Xử lý khi totalAvailableSeatsCapacity = 0
        var occupancyRate = totalAvailableSeatsCapacity > 0
            ? Math.Round((double)totalSoldSeats / totalAvailableSeatsCapacity * 100, 2)
            : 0;

        // TC-15: Tỷ lệ F&B với độ chính xác cao
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
    /// TC-07.
    /// </summary>
    public async Task<List<SalesChannelBreakdownResponse>> GetSalesChannelBreakdownAsync(
        DashboardFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = ResolveDateRange(request.FromDate, request.ToDate);

        var query = BuildBaseBookingQuery(fromDate, toDate, request.CinemaId);

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
            ChannelLabel = string.Equals(c.Channel, FbConstants.Channel.Online, StringComparison.OrdinalIgnoreCase)
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

    private IQueryable<Domain.Entities.Booking> BuildBaseBookingQuery(DateTime fromDate, DateTime toDate, string? cinemaId)
    {
        // TC-10: Chỉ lấy trạng thái hợp lệ Paid hoặc Completed
        var query = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => (b.BookingStatus == DomainConstants.EntityStatus.Paid ||
                         b.BookingStatus == DomainConstants.EntityStatus.Completed) &&
                        b.CreatedAt >= fromDate && b.CreatedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(cinemaId))
        {
            query = query.Where(b => b.Showtime != null && b.Showtime.Room != null && b.Showtime.Room.CinemaId == cinemaId);
        }

        return query;
    }

    private static (DateTime FromDate, DateTime ToDate) ResolveDateRange(DateTime? fromDate, DateTime? toDate)
    {
        // TC-13: Fallback khi fromDate hoặc toDate null -> Lấy tháng hiện tại UTC
        var start = fromDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = toDate ?? DateTime.UtcNow;

        // TC-12: Xử lý khi fromDate > toDate -> Đảo ngược vị trí để từ ngày <= đến ngày
        if (start > end)
        {
            (start, end) = (end, start);
        }

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
