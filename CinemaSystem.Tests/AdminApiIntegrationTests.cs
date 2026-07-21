using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

/// <summary>
/// HTTP integration coverage for data-driven account provisioning.
/// </summary>
public sealed class AdminApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetAssignableAccountRoles_AdminToken_ReturnsConfiguredNonAdminRoles()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedProvisioningPrerequisitesAsync(factory);

        using var client = CreateAdminClient(factory);
        var response = await client.GetAsync("/api/admin/account-provisioning/roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<List<AssignableAccountRoleResponse>>>(response);
        Assert.True(body!.Success);
        var roles = Assert.IsType<List<AssignableAccountRoleResponse>>(body.Data);
        Assert.Equal(
            [AuthConstants.RoleIds.Customer, AuthConstants.RoleIds.Manager, AuthConstants.RoleIds.Staff],
            roles.Select(role => role.RoleId).OrderBy(roleId => roleId));
        Assert.DoesNotContain(roles, role => role.RoleId == AuthConstants.RoleIds.Admin);
    }

    [Fact]
    public async Task ProvisionAccount_InactiveAdminAccount_IsRejectedEvenWithAdminToken()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedProvisioningPrerequisitesAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var admin = await db.Users.SingleAsync(user => user.UserId == "USR_TEST_ADMIN");
            admin.Status = AuthConstants.UserStatus.Inactive;
            await db.SaveChangesAsync();
        }

        using var client = CreateAdminClient(factory);
        var response = await client.PostAsJsonAsync("/api/admin/users", new ProvisionManagedAccountRequest
        {
            Email = "inactive-admin-attempt@test.com",
            FullName = "Blocked Attempt",
            RoleId = AuthConstants.RoleIds.Customer
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ProvisionedAccountResponse>>(response);
        Assert.Equal("ACCOUNT_PROVISIONING_ACTOR_NOT_FOUND", body!.ErrorCode);
    }

    [Fact]
    public async Task ProvisionManager_AdminToken_CreatesStaffProfileAndQueuesInvitation()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedProvisioningPrerequisitesAsync(factory);

        using var client = CreateAdminClient(factory);
        var response = await client.PostAsJsonAsync("/api/admin/users", new ProvisionManagedAccountRequest
        {
            Email = "newmanager@test.com",
            FullName = "New Manager",
            PhoneNumber = "0900000000",
            RoleId = AuthConstants.RoleIds.Manager,
            CinemaId = "CIN_ADMIN"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ProvisionedAccountResponse>>(response);
        Assert.True(body!.Success);
        Assert.Equal(AuthConstants.RoleIds.Manager, body.Data!.RoleId);
        Assert.Equal("CIN_ADMIN", body.Data.CinemaId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var user = await db.Users.SingleAsync(user => user.Email == "newmanager@test.com");
        var profile = await db.StaffProfiles.SingleAsync(profile => profile.UserId == user.UserId);

        Assert.Equal(AuthConstants.RoleIds.Manager, user.RoleId);
        Assert.Equal("CIN_ADMIN", profile.CinemaId);
        Assert.Equal("Manager", profile.Position);
        Assert.False(user.EmailVerified);
        Assert.Contains(factory.EmailCapture.Emails, email => email.ToEmail == "newmanager@test.com");
        Assert.Contains(await db.AuditLogs.ToListAsync(), audit =>
            audit.UserId == "USR_TEST_ADMIN"
            && audit.Action == "ACCOUNT_PROVISIONED"
            && audit.EntityId == user.UserId);
    }

    [Fact]
    public async Task ProvisionCustomer_AdminToken_CreatesCustomerProfileWithoutCinema()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedProvisioningPrerequisitesAsync(factory);

        using var client = CreateAdminClient(factory);
        var response = await client.PostAsJsonAsync("/api/admin/users", new ProvisionManagedAccountRequest
        {
            Email = "newcustomer@test.com",
            FullName = "New Customer",
            RoleId = AuthConstants.RoleIds.Customer
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var user = await db.Users.SingleAsync(user => user.Email == "newcustomer@test.com");

        Assert.Equal(AuthConstants.RoleIds.Customer, user.RoleId);
        Assert.NotNull(await db.CustomerProfiles.SingleOrDefaultAsync(profile => profile.UserId == user.UserId));
        Assert.Null(await db.StaffProfiles.SingleOrDefaultAsync(profile => profile.UserId == user.UserId));
    }

    [Fact]
    public async Task ProvisionAdmin_AdminToken_IsRejectedByAssignmentRules()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedProvisioningPrerequisitesAsync(factory);

        using var client = CreateAdminClient(factory);
        var response = await client.PostAsJsonAsync("/api/admin/users", new ProvisionManagedAccountRequest
        {
            Email = "blockedadmin@test.com",
            FullName = "Blocked Admin",
            RoleId = AuthConstants.RoleIds.Admin
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ProvisionedAccountResponse>>(response);
        Assert.Equal("ROLE_ASSIGNMENT_NOT_ALLOWED", body!.ErrorCode);
    }

    [Fact]
    public async Task ProvisionStaff_WithoutCinema_ReturnsValidationError()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedProvisioningPrerequisitesAsync(factory);

        using var client = CreateAdminClient(factory);
        var response = await client.PostAsJsonAsync("/api/admin/users", new ProvisionManagedAccountRequest
        {
            Email = "staffwithoutcinema@test.com",
            FullName = "Staff Without Cinema",
            RoleId = AuthConstants.RoleIds.Staff
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ProvisionedAccountResponse>>(response);
        Assert.Equal("CINEMA_REQUIRED", body!.ErrorCode);
    }

    [Fact]
    public async Task ProvisionAccount_ManagerRole_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PostAsJsonAsync("/api/admin/users", new ProvisionManagedAccountRequest
        {
            Email = "blocked@test.com",
            FullName = "Blocked",
            RoleId = AuthConstants.RoleIds.Customer
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProvisionAccount_DuplicateEmail_ReturnsConflict()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedProvisioningPrerequisitesAsync(factory);
        await SeedExistingUserAsync(factory, "dup@test.com");

        using var client = CreateAdminClient(factory);
        var response = await client.PostAsJsonAsync("/api/admin/users", new ProvisionManagedAccountRequest
        {
            Email = "dup@test.com",
            FullName = "Duplicate",
            RoleId = AuthConstants.RoleIds.Customer
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ProvisionedAccountResponse>>(response);
        Assert.Equal("DUPLICATE_EMAIL", body!.ErrorCode);
    }

    private static HttpClient CreateAdminClient(CinemaWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());
        return client;
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }

    private static async Task SeedProvisioningPrerequisitesAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        db.Cinemas.Add(new Cinema
        {
            CinemaId = "CIN_ADMIN",
            CinemaName = "Admin Cinema",
            Address = "1",
            City = "HCM",
            CinemaStatus = DomainConstants.CinemaStatus.Active
        });

        db.Roles.AddRange(
            new Role
            {
                RoleId = AuthConstants.RoleIds.Customer,
                RoleName = AuthConstants.Roles.Customer,
                Description = "Customer"
            },
            new Role
            {
                RoleId = AuthConstants.RoleIds.Staff,
                RoleName = AuthConstants.Roles.Staff,
                Description = "Staff"
            },
            new Role
            {
                RoleId = AuthConstants.RoleIds.Manager,
                RoleName = AuthConstants.Roles.Manager,
                Description = "Manager"
            },
            new Role
            {
                RoleId = AuthConstants.RoleIds.Admin,
                RoleName = AuthConstants.Roles.Admin,
                Description = "Admin"
            });

        db.RoleProvisioningPolicies.AddRange(
            new RoleProvisioningPolicy
            {
                RoleId = AuthConstants.RoleIds.Customer,
                ProfileKind = DomainConstants.AccountProfileKind.Customer,
                RequiresCinema = false,
                IsActive = true,
                IsPublicRegistrationAllowed = true
            },
            new RoleProvisioningPolicy
            {
                RoleId = AuthConstants.RoleIds.Staff,
                ProfileKind = DomainConstants.AccountProfileKind.Staff,
                RequiresCinema = true,
                DefaultStaffPosition = "Staff",
                IsActive = true,
                IsPublicRegistrationAllowed = false
            },
            new RoleProvisioningPolicy
            {
                RoleId = AuthConstants.RoleIds.Manager,
                ProfileKind = DomainConstants.AccountProfileKind.Staff,
                RequiresCinema = true,
                DefaultStaffPosition = "Manager",
                IsActive = true,
                IsPublicRegistrationAllowed = false
            },
            new RoleProvisioningPolicy
            {
                RoleId = AuthConstants.RoleIds.Admin,
                ProfileKind = DomainConstants.AccountProfileKind.None,
                RequiresCinema = false,
                IsActive = true,
                IsPublicRegistrationAllowed = false
            });

        db.RoleAssignmentRules.AddRange(
            new RoleAssignmentRule
            {
                GrantorRoleId = AuthConstants.RoleIds.Admin,
                GranteeRoleId = AuthConstants.RoleIds.Customer,
                IsActive = true
            },
            new RoleAssignmentRule
            {
                GrantorRoleId = AuthConstants.RoleIds.Admin,
                GranteeRoleId = AuthConstants.RoleIds.Staff,
                IsActive = true
            },
            new RoleAssignmentRule
            {
                GrantorRoleId = AuthConstants.RoleIds.Admin,
                GranteeRoleId = AuthConstants.RoleIds.Manager,
                IsActive = true
            });

        db.Users.Add(new User
        {
            UserId = "USR_TEST_ADMIN",
            RoleId = AuthConstants.RoleIds.Admin,
            Email = "admin@test.com",
            PasswordHash = "test-hash",
            FullName = "Test Admin",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedExistingUserAsync(CinemaWebApplicationFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        db.Users.Add(new User
        {
            UserId = "USR_EXISTING",
            RoleId = AuthConstants.RoleIds.Customer,
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
