using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Data;

public static class DbInitializer
{
    private const string DefaultCinemaId = "CIN_DEV_SEED";

    public static async Task SeedAsync(IServiceProvider serviceProvider, bool isDevelopment)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        await SeedRolesAsync(dbContext, logger);
        await SeedAdminAsync(dbContext, passwordHasher, clock, logger);

        if (isDevelopment)
        {
            await EnsureDevCinemaAsync(dbContext, logger);
            await SeedDevTestUsersAsync(dbContext, passwordHasher, clock, logger);
        }
    }

    private static async Task SeedRolesAsync(CinemaDbContext dbContext, ILogger logger)
    {
        var roleDefinitions = new[]
        {
            (AuthConstants.RoleIds.Admin, AuthConstants.Roles.Admin, "System administrator"),
            (AuthConstants.RoleIds.Staff, AuthConstants.Roles.Staff, "Cinema staff account"),
            (AuthConstants.RoleIds.Customer, AuthConstants.Roles.Customer, "Customer account")
        };

        foreach (var (roleId, roleName, description) in roleDefinitions)
        {
            var exists = await dbContext.Roles.AnyAsync(
                role => role.RoleId == roleId || role.RoleName == roleName);
            if (exists)
            {
                continue;
            }

            dbContext.Roles.Add(new Role
            {
                RoleId = roleId,
                RoleName = roleName,
                Description = description
            });
            logger.LogInformation("Seeded role {RoleName}.", roleName);
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedAdminAsync(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IClock clock,
        ILogger logger)
    {
        var hasAdmin = await dbContext.Users
            .Include(user => user.Role)
            .AnyAsync(user => user.Role.RoleName == AuthConstants.Roles.Admin);

        if (hasAdmin)
        {
            return;
        }

        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "Admin seeding skipped because ADMIN_PASSWORD is not configured.");
            return;
        }

        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@cinema.com";
        var normalizedEmail = NormalizeEmail(adminEmail);
        var now = clock.UtcNow;

        var adminRole = await dbContext.Roles.SingleAsync(
            role => role.RoleName == AuthConstants.Roles.Admin);

        var adminUser = new User
        {
            UserId = NewId("USR"),
            RoleId = adminRole.RoleId,
            Email = normalizedEmail,
            PasswordHash = passwordHasher.HashSecret(adminPassword),
            FullName = "System Administrator",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = now
        };

        dbContext.Users.Add(adminUser);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded admin user {Email}.", normalizedEmail);
    }

    private static async Task SeedDevTestUsersAsync(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IClock clock,
        ILogger logger)
    {
        var staffPassword = Environment.GetEnvironmentVariable("DEV_STAFF_PASSWORD");
        if (!string.IsNullOrWhiteSpace(staffPassword))
        {
            await SeedStaffUserAsync(
                dbContext,
                passwordHasher,
                clock,
                logger,
                "staff@test.com",
                staffPassword,
                "Dev Staff");
        }

        var customerPassword = Environment.GetEnvironmentVariable("DEV_CUSTOMER_PASSWORD");
        if (!string.IsNullOrWhiteSpace(customerPassword))
        {
            await SeedCustomerUserAsync(
                dbContext,
                passwordHasher,
                clock,
                logger,
                "customer@test.com",
                customerPassword,
                "Dev Customer");
        }
    }

    private static async Task SeedStaffUserAsync(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IClock clock,
        ILogger logger,
        string email,
        string password,
        string fullName)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail))
        {
            return;
        }

        var staffRole = await dbContext.Roles.SingleAsync(
            role => role.RoleName == AuthConstants.Roles.Staff);
        var now = clock.UtcNow;
        var userId = NewId("USR");

        var staffUser = new User
        {
            UserId = userId,
            RoleId = staffRole.RoleId,
            Email = normalizedEmail,
            PasswordHash = passwordHasher.HashSecret(password),
            FullName = fullName,
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = now
        };

        dbContext.Users.Add(staffUser);
        dbContext.StaffProfiles.Add(new StaffProfile
        {
            StaffProfileId = NewId("STF"),
            UserId = userId,
            CinemaId = DefaultCinemaId,
            Position = "Ticket Scanner",
            EmploymentStatus = "ACTIVE"
        });

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded dev staff user {Email}.", normalizedEmail);
    }

    private static async Task SeedCustomerUserAsync(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IClock clock,
        ILogger logger,
        string email,
        string password,
        string fullName)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail))
        {
            return;
        }

        var customerRole = await dbContext.Roles.SingleAsync(
            role => role.RoleName == AuthConstants.Roles.Customer);
        var now = clock.UtcNow;
        var userId = NewId("USR");

        var customerUser = new User
        {
            UserId = userId,
            RoleId = customerRole.RoleId,
            Email = normalizedEmail,
            PasswordHash = passwordHasher.HashSecret(password),
            FullName = fullName,
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = now
        };

        dbContext.Users.Add(customerUser);
        dbContext.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = NewId("CUS"),
            UserId = userId,
            MemberLevel = "STANDARD",
            RewardPoints = 0
        });

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded dev customer user {Email}.", normalizedEmail);
    }

    private static async Task EnsureDevCinemaAsync(CinemaDbContext dbContext, ILogger logger)
    {
        if (await dbContext.Cinemas.AnyAsync(cinema => cinema.CinemaId == DefaultCinemaId))
        {
            return;
        }

        dbContext.Cinemas.Add(new Cinema
        {
            CinemaId = DefaultCinemaId,
            CinemaName = "Dev Cinema",
            Address = "1 Dev Street",
            City = "Ho Chi Minh",
            CinemaStatus = "ACTIVE"
        });

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded development cinema {CinemaId}.", DefaultCinemaId);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
