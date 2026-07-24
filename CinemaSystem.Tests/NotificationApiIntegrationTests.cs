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

    [Fact]
    public async Task GetNotifications_WithStaffToken_ReturnsOnlyStaffNotifications()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var staffId = "USR_STAFF_FILTER_TEST";
        var custId = "USR_CUST_OTHER_TEST";

        await SeedStaffUserAsync(factory, staffId, "staff_filter@test.com");
        await SeedUserAsync(factory, custId, "cust_other@test.com");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            db.Notifications.Add(new Notification
            {
                NotificationId = "NOT_STAFF_1",
                UserId = staffId,
                Title = "Lệnh chuẩn bị phòng chiếu",
                Message = "Suất chiếu dọn dẹp phòng.",
                CreatedAt = DateTime.UtcNow
            });
            db.Notifications.Add(new Notification
            {
                NotificationId = "NOT_CUST_1",
                UserId = custId,
                Title = "Xác nhận đặt vé",
                Message = "Vé xem phim của bạn đã đặt thành công.",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff(staffId));

        var response = await client.GetAsync("/api/notifications?pageIndex=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedList<NotificationResponse>>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Lệnh chuẩn bị phòng chiếu", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetInternalFeed_WithStaffToken_ExcludesManagerOnlyAlerts()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var staffId = "USR_STAFF_FEED_TEST";
        var managerId = "USR_MGR_FEED_TEST";

        await SeedStaffUserAsync(factory, staffId, "staff_feed@test.com");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            db.Notifications.Add(new Notification
            {
                NotificationId = "NOT_STAFF_FEED_1",
                UserId = staffId,
                Title = "Lệnh chuẩn bị phòng chiếu",
                Message = "Dọn phòng chiếu 1.",
                CreatedAt = DateTime.UtcNow
            });
            db.Notifications.Add(new Notification
            {
                NotificationId = "NOT_MGR_FEED_1",
                UserId = managerId,
                Title = "Cảnh báo tồn kho thấp",
                Message = "Nguyên liệu quầy bắp nước chạm ngưỡng.",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff(staffId));

        var response = await client.GetAsync("/api/notifications/internal-feed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<NotificationResponse>>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.DoesNotContain(body.Data, n => n.Title.Contains("tồn kho"));
    }

    [Fact]
    public async Task Manager_SendNotification_ToStaff_Succeeds()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var managerId = "USR_MGR_SEND_TEST";
        var staffId = "USR_STAFF_TARGET_TEST";
        await SeedManagerUserAsync(factory, managerId, "mgr_send@test.com");
        await SeedStaffUserAsync(factory, staffId, "staff_target@test.com");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager(managerId));

        var response = await client.PostAsJsonAsync("/api/notifications/send", new SendNotificationRequest
        {
            TargetGroup = "STAFF",
            Title = "Nhắc ca trực",
            Message = "Vui lòng có mặt đúng giờ ca làm chiều.",
            Type = "Internal"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationResponse>>(JsonOptions);
        Assert.True(body!.Success);
    }

    [Fact]
    public async Task Manager_SendNotification_ToCustomer_FailsWithForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var managerId = "USR_MGR_SEND_FAIL";
        await SeedManagerUserAsync(factory, managerId, "mgr_fail@test.com");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager(managerId));

        var response = await client.PostAsJsonAsync("/api/notifications/send", new SendNotificationRequest
        {
            TargetGroup = "CUSTOMERS",
            Title = "Thông báo ưu đãi",
            Message = "Không được phép gửi từ Manager."
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_SendNotification_FailsWithForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var staffId = "USR_STAFF_SEND_FAIL";
        await SeedStaffUserAsync(factory, staffId, "staff_send_fail@test.com");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff(staffId));

        var response = await client.PostAsJsonAsync("/api/notifications/send", new SendNotificationRequest
        {
            TargetGroup = "STAFF",
            Title = "Thử gửi thông báo",
            Message = "Staff không được phép gửi."
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedManagerUserAsync(CinemaWebApplicationFactory factory, string userId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var managerRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleId == AuthConstants.RoleIds.Manager);
        if (managerRole == null)
        {
            managerRole = new Role { RoleId = AuthConstants.RoleIds.Manager, RoleName = AuthConstants.Roles.Manager, Description = "Manager" };
            db.Roles.Add(managerRole);
        }

        db.Users.Add(new User
        {
            UserId = userId,
            RoleId = managerRole.RoleId,
            Email = email,
            PasswordHash = "hash",
            FullName = "Test Manager",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
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
