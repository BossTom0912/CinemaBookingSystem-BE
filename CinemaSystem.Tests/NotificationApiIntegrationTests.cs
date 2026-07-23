using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Notifications;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class NotificationApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetNotifications_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetNotifications_WithAuth_ReturnsUserNotifications()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var userId = "USR_CUSTOMER_NOTIF_TEST";
        await SeedUserAndNotificationsAsync(factory, userId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer(userId));

        var response = await client.GetAsync("/api/notifications?pageIndex=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedList<NotificationResponse>>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(2, body.Data.TotalCount);
        Assert.Equal("Xác nhận đặt vé thành công", body.Data.Items[0].Title);
        Assert.Equal("Chúc mừng sinh nhật", body.Data.Items[1].Title);
    }

    [Fact]
    public async Task MarkAsRead_MarksSelectedAsRead()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var userId = "USR_CUST_READ_TEST";
        await SeedUserAndNotificationsAsync(factory, userId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var notifIds = await db.Notifications.Where(n => n.UserId == userId).Select(n => n.NotificationId).ToListAsync();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer(userId));

        var response = await client.PutAsJsonAsync("/api/notifications/read", new MarkReadRequest
        {
            NotificationIds = new List<string> { notifIds[0] }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
        Assert.True(body!.Success);

        // Verify database updated
        var updatedNotif = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.NotificationId == notifIds[0]);
        Assert.True(updatedNotif!.IsRead);

        var remainingUnread = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.NotificationId == notifIds[1]);
        Assert.False(remainingUnread!.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksAllAsRead()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var userId = "USR_CUST_READ_ALL_TEST";
        await SeedUserAndNotificationsAsync(factory, userId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer(userId));

        var response = await client.PutAsync("/api/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
        Assert.True(body!.Success);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var hasUnread = await db.Notifications.AnyAsync(n => n.UserId == userId && !n.IsRead);
        Assert.False(hasUnread);
    }

    [Fact]
    public async Task SendNotification_WithAdminToken_SavesAndDelivers()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var targetUserId = "USR_TARGET_NOTIF";
        await SeedUserAsync(factory, targetUserId, "target@test.com");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var response = await client.PostAsJsonAsync("/api/notifications/send", new SendNotificationRequest
        {
            UserId = targetUserId,
            Title = "Nhắc lịch chiếu (Reminder)",
            Message = "Suất chiếu phim bom tấn của bạn sẽ bắt đầu trong 30 phút. Vui lòng đến rạp đúng giờ.",
            Channel = "SMS",
            Type = "Transactional"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationResponse>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.Equal("Nhắc lịch chiếu (Reminder)", body.Data!.Title);
        Assert.Equal("Sent", body.Data.Status);
        Assert.Equal("SMS", body.Data.Channel);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var exists = await db.Notifications.AnyAsync(n => n.UserId == targetUserId && n.Title == "Nhắc lịch chiếu (Reminder)");
        Assert.True(exists);
    }

    [Fact]
    public async Task TriggerSystemNotification_RoomCleanup_CreatesCleanupOrderForStaff()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var staffUserId = "USR_STAFF_CLEANER";
        await SeedStaffUserAsync(factory, staffUserId, "cleaner@test.com");
        var showtimeId = await SeedShowtimeAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff(staffUserId));

        var response = await client.PostAsJsonAsync("/api/notifications/trigger-system", new TriggerSystemNotificationRequest
        {
            Type = "RoomCleanup",
            TargetId = showtimeId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
        Assert.True(body!.Success);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == staffUserId);
        Assert.NotNull(notif);
        Assert.Equal("Lệnh chuẩn bị phòng chiếu", notif.Title);
        Assert.Contains("vừa kết thúc. Nhân viên vệ sinh chuẩn bị dọn dẹp", notif.Message);
    }

    [Fact]
    public async Task SendNotification_WithTargetGroup_DispatchesToAllGroupUsers()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var staff1 = "USR_STAFF_G1";
        var staff2 = "USR_STAFF_G2";
        await SeedStaffUserAsync(factory, staff1, "staff1@test.com");
        await SeedStaffUserAsync(factory, staff2, "staff2@test.com");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var response = await client.PostAsJsonAsync("/api/notifications/send", new SendNotificationRequest
        {
            TargetGroup = "STAFF",
            Title = "Thông báo họp giao ban",
            Message = "Họp giao ban định kỳ vào 8h00 sáng mai.",
            Channel = "App",
            Type = "Internal"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationResponse>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.Equal("STAFF", body.Data!.UserId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var notifs = await db.Notifications.Where(n => n.Title == "Thông báo họp giao ban").ToListAsync();
        Assert.Contains(notifs, n => n.UserId == staff1);
        Assert.Contains(notifs, n => n.UserId == staff2);
    }

    private static async Task SeedUserAsync(CinemaWebApplicationFactory factory, string userId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        
        var customerRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleId == AuthConstants.RoleIds.Customer);
        if (customerRole == null)
        {
            customerRole = new Role { RoleId = AuthConstants.RoleIds.Customer, RoleName = AuthConstants.Roles.Customer, Description = "Customer" };
            db.Roles.Add(customerRole);
        }

        db.Users.Add(new User
        {
            UserId = userId,
            RoleId = customerRole.RoleId,
            Email = email,
            PasswordHash = "hash",
            FullName = "Test Customer",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedStaffUserAsync(CinemaWebApplicationFactory factory, string userId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        
        var staffRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleId == AuthConstants.RoleIds.Staff);
        if (staffRole == null)
        {
            staffRole = new Role { RoleId = AuthConstants.RoleIds.Staff, RoleName = AuthConstants.Roles.Staff, Description = "Staff" };
            db.Roles.Add(staffRole);
        }

        db.Users.Add(new User
        {
            UserId = userId,
            RoleId = staffRole.RoleId,
            Email = email,
            PasswordHash = "hash",
            FullName = "Test Staff",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedUserAndNotificationsAsync(CinemaWebApplicationFactory factory, string userId)
    {
        await SeedUserAsync(factory, userId, $"{userId}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        
        db.Notifications.Add(new Notification
        {
            NotificationId = $"NOT_1_{userId}",
            UserId = userId,
            Title = "Xác nhận đặt vé thành công",
            Message = "Cảm ơn bạn đã đặt vé xem phim.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });

        db.Notifications.Add(new Notification
        {
            NotificationId = $"NOT_2_{userId}",
            UserId = userId,
            Title = "Chúc mừng sinh nhật",
            Message = "Nhận ngay voucher bắp nước miễn phí.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        });

        await db.SaveChangesAsync();
    }

    private static async Task<string> SeedShowtimeAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var cinema = new Cinema { CinemaId = "CIN_NOTIF", CinemaName = "Cinema Notif", Address = "A", City = "HCM", CinemaStatus = "ACTIVE" };
        db.Cinemas.Add(cinema);

        var room = new Room { RoomId = "ROM_NOTIF", CinemaId = cinema.CinemaId, RoomName = "Room Notif 1", Capacity = 100, RoomStatus = "ACTIVE" };
        db.Rooms.Add(room);

        var movie = new Movie { MovieId = "MOV_NOTIF", Title = "Notif Blockbuster Movie", DurationMinutes = 120, MovieStatus = "ACTIVE" };
        db.Movies.Add(movie);

        var showtime = new Showtime
        {
            ShowtimeId = "SHW_NOTIF_TEST",
            MovieId = movie.MovieId,
            RoomId = room.RoomId,
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            BasePrice = 100000,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow
        };
        db.Showtimes.Add(showtime);

        await db.SaveChangesAsync();
        return showtime.ShowtimeId;
    }
}
