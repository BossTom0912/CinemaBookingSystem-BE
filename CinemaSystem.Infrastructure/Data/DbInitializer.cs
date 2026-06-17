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
    private const int SeedFoodInventoryQuantity = 500;

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
            await EnsureFoodAndBeverageInventoryAsync(dbContext, logger);
            await SeedDevTestUsersAsync(dbContext, passwordHasher, clock, logger);
        }
    }

    private static async Task SeedRolesAsync(CinemaDbContext dbContext, ILogger logger)
    {
        var roleDefinitions = new[]
        {
            (AuthConstants.RoleIds.Admin, AuthConstants.Roles.Admin, "System administrator"),
            (AuthConstants.RoleIds.Manager, AuthConstants.Roles.Manager, "Cinema manager account"),
            (AuthConstants.RoleIds.Staff, AuthConstants.Roles.Staff, "Cinema staff account"),
            (AuthConstants.RoleIds.Customer, AuthConstants.Roles.Customer, "Customer account")
        };

        foreach (var (roleId, roleName, description) in roleDefinitions)
        {
            var role = await dbContext.Roles.FirstOrDefaultAsync(
                item => item.RoleId == roleId || item.RoleName == roleName);
            if (role is not null)
            {
                if (role.RoleName != roleName)
                {
                    role.RoleName = roleName;
                    logger.LogInformation("Normalized role {RoleId} to {RoleName}.", roleId, roleName);
                }

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
            .AnyAsync(user => user.RoleId == AuthConstants.RoleIds.Admin);

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
            role => role.RoleId == AuthConstants.RoleIds.Admin);

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
            role => role.RoleId == AuthConstants.RoleIds.Staff);
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
            role => role.RoleId == AuthConstants.RoleIds.Customer);
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

    private static async Task EnsureFoodAndBeverageInventoryAsync(
        CinemaDbContext dbContext,
        ILogger logger)
    {
        var foodDefinitions = new (string FbItemId, string ItemName, decimal Price)[]
        {
            ("FB_POPCORN_PEPSI_L", "Combo bap ngot va Pepsi lon", 75000m),
            ("FB_CHEESE_POPCORN_M", "Bap pho mai co vua", 55000m)
        };

        foreach (var food in foodDefinitions)
        {
            var existingItem = await dbContext.FbItems.SingleOrDefaultAsync(
                item => item.FbItemId == food.FbItemId);

            if (existingItem is null)
            {
                dbContext.FbItems.Add(new FbItem
                {
                    FbItemId = food.FbItemId,
                    ItemName = food.ItemName,
                    Price = food.Price,
                    ItemStatus = BookingConstants.ResourceStatus.Available
                });
                logger.LogInformation("Seeded F&B item {FbItemId}.", food.FbItemId);
                continue;
            }

            if (existingItem.Price <= 0)
            {
                existingItem.Price = food.Price;
            }
        }

        await dbContext.SaveChangesAsync();

        var activeCinemaIds = await dbContext.Cinemas
            .Where(cinema => cinema.CinemaStatus == BookingConstants.ResourceStatus.Active)
            .Select(cinema => cinema.CinemaId)
            .ToListAsync();
        if (activeCinemaIds.Count == 0)
        {
            return;
        }

        var foodItemIds = foodDefinitions
            .Select(item => item.FbItemId)
            .ToList();
        var inventories = await dbContext.CinemaFbInventories
            .Where(inventory =>
                activeCinemaIds.Contains(inventory.CinemaId) &&
                foodItemIds.Contains(inventory.FbItemId))
            .ToListAsync();
        var inventoryByKey = inventories.ToDictionary(
            inventory => $"{inventory.CinemaId}|{inventory.FbItemId}",
            StringComparer.OrdinalIgnoreCase);

        var createdCount = 0;
        var replenishedCount = 0;
        foreach (var cinemaId in activeCinemaIds)
        {
            foreach (var foodItemId in foodItemIds)
            {
                var key = $"{cinemaId}|{foodItemId}";
                if (!inventoryByKey.TryGetValue(key, out var inventory))
                {
                    dbContext.CinemaFbInventories.Add(new CinemaFbInventory
                    {
                        CinemaInventoryId = NewId("CFI"),
                        CinemaId = cinemaId,
                        FbItemId = foodItemId,
                        Quantity = SeedFoodInventoryQuantity
                    });
                    createdCount++;
                    continue;
                }

                if (inventory.Quantity < SeedFoodInventoryQuantity)
                {
                    inventory.Quantity = SeedFoodInventoryQuantity;
                    replenishedCount++;
                }
            }
        }

        if (createdCount == 0 && replenishedCount == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation(
            "Seeded F&B inventory: {CreatedCount} new rows, {ReplenishedCount} replenished rows.",
            createdCount,
            replenishedCount);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
