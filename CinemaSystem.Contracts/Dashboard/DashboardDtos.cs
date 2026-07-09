using System;
using System.Collections.Generic;

namespace CinemaSystem.Contracts.Dashboard;

/// <summary>
/// DTO chứa bộ lọc chung cho các API Dashboard (khoảng thời gian, chi nhánh rạp).
/// </summary>
public class DashboardFilterRequest
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? CinemaId { get; init; }
}

/// <summary>
/// DTO bộ lọc cho API Xu hướng doanh thu.
/// </summary>
public class RevenueTrendRequest : DashboardFilterRequest
{
    /// <summary>
    /// Chu kỳ gom nhóm: "week" (theo tuần ISO) hoặc "month" (theo tháng/năm). Mặc định "month".
    /// </summary>
    public string Period { get; init; } = "month";
}

/// <summary>
/// API 1: Thẻ chỉ số tổng quan (Overview).
/// </summary>
public record DashboardOverviewResponse
{
    public decimal GrossRevenue { get; init; }
    public decimal TotalRefunds { get; init; }
    public decimal NetRevenue { get; init; }
    public decimal AverageOrderValue { get; init; }
    public int TotalTicketsSold { get; init; }
    public int TotalSuccessfulBookings { get; init; }
}

/// <summary>
/// API 2: Phần tử dữ liệu xu hướng doanh thu (Revenue Trend Key-Value).
/// </summary>
public record RevenueTrendItemResponse
{
    public string Label { get; init; } = string.Empty;
    public decimal GrossRevenue { get; init; }
    public decimal NetRevenue { get; init; }
    public int BookingCount { get; init; }
}

/// <summary>
/// API 3: Xếp hạng top 3 phim doanh thu cao nhất (Movie Ranking Key-Value).
/// </summary>
public record MovieRankingResponse
{
    public string MovieId { get; init; } = string.Empty;
    public string MovieTitle { get; init; } = string.Empty;
    public decimal TicketRevenue { get; init; }
    public int TicketsSold { get; init; }
}

/// <summary>
/// API 4: Tỷ lệ lấp đầy phòng chiếu & bóc tách doanh thu Bắp nước F&B vs Vé.
/// </summary>
public record OccupancyAndFbBreakdownResponse
{
    public double OccupancyRate { get; init; }
    public int TotalSoldSeats { get; init; }
    public int TotalAvailableSeatsCapacity { get; init; }
    public decimal TicketRevenue { get; init; }
    public decimal FbRevenue { get; init; }
    public double FbRevenuePercentage { get; init; }
    public List<FbItemSalesResponse> FbItems { get; init; } = new();
}

/// <summary>
/// Doanh số từng món F&B đã bán trong khoảng lọc dashboard.
/// </summary>
public record FbItemSalesResponse
{
    public string FbItemId { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int QuantitySold { get; init; }
    public decimal Revenue { get; init; }
}

/// <summary>
/// API 5: Phân loại tỷ lệ kênh bán hàng (Online vs Counter Key-Value).
/// </summary>
public record SalesChannelBreakdownResponse
{
    public string Channel { get; init; } = string.Empty;
    public string ChannelLabel { get; init; } = string.Empty;
    public decimal TotalRevenue { get; init; }
    public int BookingCount { get; init; }
    public double Percentage { get; init; }
}
