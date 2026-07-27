using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using CinemaSystem.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CinemaSystem.Hubs;

/// <summary>
/// SignalR Hub quản lý kết nối thời gian thực cho tính năng Soát Vé Đa Thiết Bị.
///
/// Chiến lược Mapping UserID ↔ ConnectionId:
///   Mỗi khi một client kết nối (Desktop hoặc Mobile của cùng nhân viên),
///   Hub tự động thêm ConnectionId vào một SignalR Group có tên là UserId.
///   Khi Mobile quét vé thành công, Controller chỉ cần broadcast tới
///   Group[userId] và Desktop sẽ nhận ngay lập tức.
/// </summary>
[Authorize(Policy = AuthConstants.Policies.CanScanTicket)]
public sealed class TicketHub : Hub
{
    private readonly ILogger<TicketHub> _logger;

    public TicketHub(ILogger<TicketHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Được gọi tự động khi một client kết nối tới Hub.
    /// Thêm ConnectionId vào Group của UserId để sau này có thể broadcast theo user.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            // Thêm connection vào Group tên là userId.
            // Nếu cùng nhân viên mở cả Desktop lẫn Mobile, cả 2 connection
            // đều nằm trong cùng Group → broadcast tới group là đủ.
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            _logger.LogInformation(
                "[TicketHub] Client connected. ConnectionId={ConnectionId} UserId={UserId}",
                Context.ConnectionId,
                userId);
        }
        else
        {
            _logger.LogWarning(
                "[TicketHub] Anonymous connection rejected. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Được gọi tự động khi một client ngắt kết nối.
    /// SignalR tự động xóa connection khỏi mọi Group khi disconnect,
    /// nhưng ta vẫn log để theo dõi.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();

        _logger.LogInformation(
            "[TicketHub] Client disconnected. ConnectionId={ConnectionId} UserId={UserId} Error={Error}",
            Context.ConnectionId,
            userId ?? "unknown",
            exception?.Message ?? "none");

        await base.OnDisconnectedAsync(exception);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private string? GetUserId()
    {
        // Lấy userId từ JWT claim – khớp với cách TicketsController lấy claim
        return Context.User?.FindFirst(AuthConstants.Claims.UserId)?.Value
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    }
}
