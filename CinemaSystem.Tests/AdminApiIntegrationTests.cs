using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Infrastructure.Persistence;

using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

/// <summary>
/// Integration test HTTP cho api/admin — tạo tài khoản Staff.
/// Nguồn: CinemaSystem/Controllers/AdminController.cs
/// </summary>
public sealed class AdminApiIntegrationTests
{
  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

  [Fact]
  public async Task CreateStaff_AdminToken_SendsInvitationEmail()
  {
    // Luồng: Admin gọi POST /api/admin/staff → tạo user Staff + gửi email mời.
    await using var factory = new CinemaWebApplicationFactory();
    await SeedAdminPrerequisitesAsync(factory);

    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

    var response = await client.PostAsJsonAsync("/api/admin/staff", new CreateStaffRequest
    {
      Email = "newstaff@test.com",
      FullName = "New Staff"
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<object>>(response);
    Assert.True(body!.Success);

    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    var user = await db.Users.SingleAsync(u => u.Email == "newstaff@test.com");
    Assert.Equal(AuthConstants.Roles.Staff, (await db.Roles.SingleAsync(r => r.RoleId == user.RoleId)).RoleName);
    Assert.Contains(factory.EmailCapture.Emails, e => e.ToEmail == "newstaff@test.com");
  }

  [Fact]
  public async Task CreateStaff_ManagerRole_ReturnsForbidden()
  {
    // Luồng: Manager không có policy CanManageUserAndRole → 403.
    await using var factory = new CinemaWebApplicationFactory();
    await SeedAdminPrerequisitesAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

    var response = await client.PostAsJsonAsync("/api/admin/staff", new CreateStaffRequest
    {
      Email = "blocked@test.com",
      FullName = "Blocked"
    });
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task CreateStaff_DuplicateEmail_ReturnsConflict()
  {
    // Luồng: email đã tồn tại → 409 DUPLICATE_EMAIL.
    await using var factory = new CinemaWebApplicationFactory();
    await SeedAdminPrerequisitesAsync(factory);
    await SeedExistingUserAsync(factory, "dup@test.com");

    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

    var response = await client.PostAsJsonAsync("/api/admin/staff", new CreateStaffRequest
    {
      Email = "dup@test.com",
      FullName = "Duplicate"
    });
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<object>>(response);
    Assert.Equal("DUPLICATE_EMAIL", body!.ErrorCode);
  }

  private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
  {
    return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
  }

  private static async Task SeedAdminPrerequisitesAsync(CinemaWebApplicationFactory factory)
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    db.Cinemas.Add(new Cinema
    {
      CinemaId = "CIN_ADMIN",
      CinemaName = "Admin Cinema",
      Address = "1",
      City = "HCM",
      CinemaStatus = "ACTIVE"
    });
    db.Roles.Add(new Role
    {
      RoleId = AuthConstants.RoleIds.Staff,
      RoleName = AuthConstants.Roles.Staff,
      Description = "Staff"
    });
    await db.SaveChangesAsync();
  }

  private static async Task SeedExistingUserAsync(CinemaWebApplicationFactory factory, string email)
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    var role = await db.Roles.SingleAsync(r => r.RoleId == AuthConstants.RoleIds.Staff);
    db.Users.Add(new User
    {
      UserId = "USR_EXISTING",
      RoleId = role.RoleId,
      Email = email,
      PasswordHash = "hash",
      FullName = "Existing",
      Status = AuthConstants.UserStatus.Active,
      EmailVerified = true,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
  }
}
