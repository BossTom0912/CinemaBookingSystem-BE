using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Contracts.Dashboard;

namespace CinemaSystem.Application.Interfaces;

public interface IDashboardService
{
    /// <summary>
    /// API 1: Lấy thẻ chỉ số tổng quan (Overview).
    /// </summary>
    Task<DashboardOverviewResponse> GetOverviewAsync(DashboardFilterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// API 2: Lấy dữ liệu xu hướng doanh thu theo mốc thời gian (Key-Value cho Chart).
    /// </summary>
    Task<List<RevenueTrendItemResponse>> GetRevenueTrendsAsync(RevenueTrendRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// API 3: Truy vấn xếp hạng Top 3 phim có doanh thu cao nhất.
    /// </summary>
    Task<List<MovieRankingResponse>> GetTop3MoviesRankingAsync(DashboardFilterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// API 4: Tính tỷ lệ lấp đầy rạp (Occupancy Rate %) và bóc tách doanh thu Bắp nước (F&B) vs Vé phim.
    /// </summary>
    Task<OccupancyAndFbBreakdownResponse> GetOccupancyAndFbBreakdownAsync(DashboardFilterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// API 5: Phân loại tỷ lệ doanh thu theo kênh bán hàng (Online vs Counter).
    /// </summary>
    Task<List<SalesChannelBreakdownResponse>> GetSalesChannelBreakdownAsync(DashboardFilterRequest request, CancellationToken cancellationToken = default);
}
