using CinemaSystem.Application.Common;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests.Infrastructure;

public static class CinemaScopeTestData
{
    public static Task SeedManagerScopeAsync(
        CinemaWebApplicationFactory factory,
        string cinemaId,
        string userId = "USR_TEST_MANAGER")
    {
        return SeedInternalUserScopeAsync(
            factory,
            userId,
            cinemaId,
            AuthConstants.RoleIds.Manager,
            AuthConstants.Roles.Manager,
            "Manager");
    }

    public static Task SeedStaffScopeAsync(
        CinemaWebApplicationFactory factory,
        string cinemaId,
        string userId = "USR_TEST_STAFF")
    {
        return SeedInternalUserScopeAsync(
            factory,
            userId,
            cinemaId,
            AuthConstants.RoleIds.Staff,
            AuthConstants.Roles.Staff,
            "Staff");
    }

    private static async Task SeedInternalUserScopeAsync(
        CinemaWebApplicationFactory factory,
        string userId,
        string cinemaId,
        string roleId,
        string roleName,
        string position)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        if (!await db.Roles.AnyAsync(role => role.RoleId == roleId))
        {
            db.Roles.Add(new Role
            {
                RoleId = roleId,
                RoleName = roleName,
                Description = $"{roleName} test role"
            });
        }

        if (!await db.Users.AnyAsync(user => user.UserId == userId))
        {
            db.Users.Add(new User
            {
                UserId = userId,
                RoleId = roleId,
                Email = $"{roleName.ToLowerInvariant()}@test.com",
                PasswordHash = "TEST_HASH",
                FullName = $"Test {position}",
                Status = AuthConstants.UserStatus.Active,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var profile = await db.StaffProfiles.FirstOrDefaultAsync(item => item.UserId == userId);
        if (profile is null)
        {
            db.StaffProfiles.Add(new StaffProfile
            {
                StaffProfileId = $"STF_{Guid.NewGuid():N}",
                UserId = userId,
                CinemaId = cinemaId,
                Position = position,
                EmploymentStatus = "ACTIVE"
            });
        }
        else
        {
            profile.CinemaId = cinemaId;
            profile.Position = position;
            profile.EmploymentStatus = "ACTIVE";
        }

        await db.SaveChangesAsync();
    }
}
