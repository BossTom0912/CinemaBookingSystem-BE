using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Data;

/// <summary>
/// Seeds invariant role/payment-provider definitions and an optional initial
/// administrator. Demo cinema, user, and F&amp;B records belong in the canonical
/// database script, not in application startup code.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, bool _)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var initialAdminSettings = scope.ServiceProvider
            .GetRequiredService<IOptions<InitialAdminSettings>>()
            .Value;
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DbInitializer");

        await SeedRolesAsync(dbContext, logger);
        await SeedPaymentProvidersAsync(dbContext, logger);
        await SeedAdminAsync(
            dbContext,
            passwordHasher,
            clock,
            initialAdminSettings,
            logger);
    }

    private static async Task SeedPaymentProvidersAsync(
        CinemaDbContext dbContext,
        ILogger logger)
    {
        var exists = await dbContext.PaymentProviders.AnyAsync(
            item => item.PaymentProviderId == DomainConstants.PaymentProvider.VnPayId);
        if (exists)
        {
            return;
        }

        dbContext.PaymentProviders.Add(new PaymentProvider
        {
            PaymentProviderId = DomainConstants.PaymentProvider.VnPayId,
            ProviderName = DomainConstants.PaymentProvider.VnPayName,
            ApiEndpoint = null,
            ProviderStatus = DomainConstants.EntityStatus.Active
        });
        await dbContext.SaveChangesAsync();
        logger.LogInformation(
            "Seeded payment provider {ProviderName}.",
            DomainConstants.PaymentProvider.VnPayName);
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
                    logger.LogInformation(
                        "Normalized role {RoleId} to {RoleName}.",
                        roleId,
                        roleName);
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
        InitialAdminSettings settings,
        ILogger logger)
    {
        var hasAdmin = await dbContext.Users
            .AnyAsync(user => user.RoleId == AuthConstants.RoleIds.Admin);
        if (hasAdmin)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning(
                "Admin seeding skipped because InitialAdmin:Password is not configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Email)
            || string.IsNullOrWhiteSpace(settings.FullName))
        {
            logger.LogWarning(
                "Admin seeding skipped because InitialAdmin:Email or InitialAdmin:FullName is missing.");
            return;
        }

        var normalizedEmail = settings.Email.Trim().ToLowerInvariant();
        var adminRole = await dbContext.Roles.SingleAsync(
            role => role.RoleId == AuthConstants.RoleIds.Admin);

        dbContext.Users.Add(new User
        {
            UserId = NewId(DomainConstants.EntityIdPrefix.User),
            RoleId = adminRole.RoleId,
            Email = normalizedEmail,
            PasswordHash = passwordHasher.HashSecret(settings.Password),
            FullName = settings.FullName.Trim(),
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = clock.UtcNow
        });

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded admin user {Email}.", normalizedEmail);
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
