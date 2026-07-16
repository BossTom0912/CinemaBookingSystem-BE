using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// API Dashboard báo cáo doanh thu & phân tích dữ liệu rạp phim cho Admin / Manager.
/// </summary>
[Route("api/v1/admin/dashboard")]
[ApiController]
[Authorize(Policy = AuthConstants.Policies.CanViewSystemDashboard)]
public class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public AdminDashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// API 1: Thẻ chỉ số tổng quan (Overview) - Gross Revenue, Net Revenue, Total Refunds, AOV, Total Tickets Sold.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponse<DashboardOverviewResponse>), 200)]
    public async Task<ActionResult<ApiResponse<DashboardOverviewResponse>>> GetOverview(
        [FromQuery] DashboardFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var overview = await _dashboardService.GetOverviewAsync(filter, cancellationToken);
        return Ok(new ApiResponse<DashboardOverviewResponse>
        {
            Success = true,
            Message = "Lấy thẻ chỉ số tổng quan dashboard thành công.",
            Data = overview
        });
    }

    /// <summary>
    /// API 2: Xu hướng doanh thu theo mốc thời gian (Key-Value: Label - Gross/Net Revenue, Booking Count).
    /// </summary>
    [HttpGet("revenue-trend")]
    [ProducesResponseType(typeof(ApiResponse<List<RevenueTrendItemResponse>>), 200)]
    public async Task<ActionResult<ApiResponse<List<RevenueTrendItemResponse>>>> GetRevenueTrends(
        [FromQuery] RevenueTrendRequest filter,
        CancellationToken cancellationToken)
    {
        var trends = await _dashboardService.GetRevenueTrendsAsync(filter, cancellationToken);
        return Ok(new ApiResponse<List<RevenueTrendItemResponse>>
        {
            Success = true,
            Message = "Lấy dữ liệu xu hướng doanh thu thành công.",
            Data = trends
        });
    }

    /// <summary>
    /// API 3: Top 3 phim có doanh thu cao nhất (Key-Value: Tên phim - Doanh thu vé).
    /// </summary>
    [HttpGet("movie-ranking")]
    [ProducesResponseType(typeof(ApiResponse<List<MovieRankingResponse>>), 200)]
    public async Task<ActionResult<ApiResponse<List<MovieRankingResponse>>>> GetMovieRanking(
        [FromQuery] DashboardFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var topMovies = await _dashboardService.GetTop3MoviesRankingAsync(filter, cancellationToken);
        return Ok(new ApiResponse<List<MovieRankingResponse>>
        {
            Success = true,
            Message = "Lấy danh sách top 3 phim doanh thu cao nhất thành công.",
            Data = topMovies
        });
    }

    /// <summary>
    /// API 4: Tỷ lệ lấp đầy phòng chiếu (Occupancy Rate %) & Bóc tách doanh thu Bắp nước (F&B) vs Vé.
    /// </summary>
    [HttpGet("occupancy-and-fb")]
    [ProducesResponseType(typeof(ApiResponse<OccupancyAndFbBreakdownResponse>), 200)]
    public async Task<ActionResult<ApiResponse<OccupancyAndFbBreakdownResponse>>> GetOccupancyAndFb(
        [FromQuery] DashboardFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var breakdown = await _dashboardService.GetOccupancyAndFbBreakdownAsync(filter, cancellationToken);
        return Ok(new ApiResponse<OccupancyAndFbBreakdownResponse>
        {
            Success = true,
            Message = "Lấy tỷ lệ lấp đầy rạp và bóc tách doanh thu F&B thành công.",
            Data = breakdown
        });
    }

    /// <summary>
    /// API 5: Phân loại tỷ lệ kênh bán hàng (Trực tuyến vs Tại quầy).
    /// </summary>
    [HttpGet("sales-channels")]
    [ProducesResponseType(typeof(ApiResponse<List<SalesChannelBreakdownResponse>>), 200)]
    public async Task<ActionResult<ApiResponse<List<SalesChannelBreakdownResponse>>>> GetSalesChannels(
        [FromQuery] DashboardFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var channels = await _dashboardService.GetSalesChannelBreakdownAsync(filter, cancellationToken);
        return Ok(new ApiResponse<List<SalesChannelBreakdownResponse>>
        {
            Success = true,
            Message = "Lấy phân loại tỷ lệ kênh bán hàng thành công.",
            Data = channels
        });
    }
}
