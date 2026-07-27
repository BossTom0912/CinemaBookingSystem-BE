using System.Security.Cryptography;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Auth;

/// <summary>
/// Creates managed accounts from database-backed role provisioning policies.
/// </summary>
public sealed class AccountProvisioningService : IAccountProvisioningService
{
    private const string PasswordResetPurpose =
        DomainConstants.VerificationTokenPurpose.PasswordReset;

    private readonly CinemaDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IClock _clock;
    private readonly AuthSettings _authSettings;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IUserHeartbeatTracker? _heartbeatTracker;

    public AccountProvisioningService(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IOtpGenerator otpGenerator,
        IClock clock,
        IOptions<AuthSettings> authOptions,
        IBackgroundJobClient backgroundJobClient,
        IUserHeartbeatTracker? heartbeatTracker = null)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _otpGenerator = otpGenerator;
        _clock = clock;
        _authSettings = authOptions.Value;
        _backgroundJobClient = backgroundJobClient;
        _heartbeatTracker = heartbeatTracker;
    }

    public async Task<ServiceResult<IReadOnlyList<AssignableAccountRoleResponse>>> GetAssignableRolesAsync(
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var actorRoleId = await GetActorRoleIdAsync(actorUserId, cancellationToken);
        if (actorRoleId is null)
        {
            return ServiceResult<IReadOnlyList<AssignableAccountRoleResponse>>.Fail(
                403,
                "The authenticated account is not available for account provisioning.",
                "ACCOUNT_PROVISIONING_ACTOR_NOT_FOUND");
        }

        var roles = await (
                from rule in _dbContext.RoleAssignmentRules.AsNoTracking()
                join policy in _dbContext.RoleProvisioningPolicies.AsNoTracking()
                    on rule.GranteeRoleId equals policy.RoleId
                join role in _dbContext.Roles.AsNoTracking()
                    on policy.RoleId equals role.RoleId
                where rule.GrantorRoleId == actorRoleId
                      && rule.IsActive
                      && policy.IsActive
                orderby role.RoleName
                select new AssignableAccountRoleResponse
                {
                    RoleId = role.RoleId,
                    RoleName = role.RoleName,
                    Description = role.Description,
                    ProfileKind = policy.ProfileKind,
                    RequiresCinema = policy.RequiresCinema
                })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<AssignableAccountRoleResponse>>.Ok(roles);
    }

    public async Task<ServiceResult<ProvisionedAccountResponse>> ProvisionAsync(
        string actorUserId,
        ProvisionManagedAccountRequest request,
        CancellationToken cancellationToken)
    {
        var actorRoleId = await GetActorRoleIdAsync(actorUserId, cancellationToken);
        if (actorRoleId is null)
        {
            return ServiceResult<ProvisionedAccountResponse>.Fail(
                403,
                "The authenticated account is not available for account provisioning.",
                "ACCOUNT_PROVISIONING_ACTOR_NOT_FOUND");
        }

        var roleId = request.RoleId.Trim();
        var definition = await GetProvisioningDefinitionAsync(
            actorRoleId,
            roleId,
            cancellationToken);
        if (definition is null)
        {
            return ServiceResult<ProvisionedAccountResponse>.Fail(
                403,
                "You are not allowed to provision the selected role.",
                "ROLE_ASSIGNMENT_NOT_ALLOWED");
        }

        var profileValidation = await ValidateProfileInputsAsync(
            definition,
            request.CinemaId,
            cancellationToken);
        if (profileValidation is not null)
        {
            return ServiceResult<ProvisionedAccountResponse>.Fail(
                profileValidation.StatusCode,
                profileValidation.Message,
                profileValidation.ErrorCode);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            return ServiceResult<ProvisionedAccountResponse>.Fail(
                409,
                "Email already exists.",
                "DUPLICATE_EMAIL");
        }

        var now = _clock.UtcNow;
        var userId = NewId(DomainConstants.EntityIdPrefix.User);
        var invitationOtp = _otpGenerator.GenerateSixDigitOtp();
        var invitationExpiresAt = now.AddMinutes(_authSettings.InvitationTokenExpiryMinutes);
        var cinemaId = NormalizeOptional(request.CinemaId);
        var user = new User
        {
            UserId = userId,
            RoleId = definition.RoleId,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashSecret(
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            FullName = request.FullName.Trim(),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = false,
            CreatedAt = now
        };

        var invitationToken = new EmailVerificationToken
        {
            TokenId = NewId(DomainConstants.EntityIdPrefix.EmailVerificationToken),
            UserId = userId,
            Token = _passwordHasher.HashSecret(invitationOtp),
            CreatedAt = now,
            ExpiredAt = invitationExpiresAt,
            IsUsed = false,
            Purpose = PasswordResetPurpose
        };

        _dbContext.Users.Add(user);
        AddProfile(userId, definition, cinemaId);
        _dbContext.EmailVerificationTokens.Add(invitationToken);
        _dbContext.AuditLogs.Add(new AuditLog
        {
            AuditLogId = NewId(DomainConstants.EntityIdPrefix.AuditLog),
            UserId = actorUserId,
            Action = "ACCOUNT_PROVISIONED",
            EntityName = "USER",
            EntityId = userId,
            NewValue = JsonSerializer.Serialize(new
            {
                user.Email,
                user.RoleId,
                cinemaId,
                definition.ProfileKind
            }),
            CreatedAt = now
        });

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        _backgroundJobClient.Enqueue<IEmailService>(email =>
            email.SendAccountInvitationAsync(normalizedEmail, invitationOtp, CancellationToken.None));

        return ServiceResult<ProvisionedAccountResponse>.Ok(
            new ProvisionedAccountResponse
            {
                UserId = userId,
                Email = normalizedEmail,
                RoleId = definition.RoleId,
                RoleName = definition.RoleName,
                CinemaId = cinemaId,
                InvitationExpiresAt = invitationExpiresAt
            },
            "Account created. Invitation email queued.",
            201);
    }

    public async Task<ServiceResult<IReadOnlyList<ManagedUserResponse>>> GetManagedUsersAsync(
        CancellationToken cancellationToken)
    {
        var usersData = await (
            from user in _dbContext.Users.AsNoTracking()
            join role in _dbContext.Roles.AsNoTracking() on user.RoleId equals role.RoleId
            join staff in _dbContext.StaffProfiles.AsNoTracking() on user.UserId equals staff.UserId into staffGroup
            from staff in staffGroup.DefaultIfEmpty()
            join cinema in _dbContext.Cinemas.AsNoTracking() on staff.CinemaId equals cinema.CinemaId into cinemaGroup
            from cinema in cinemaGroup.DefaultIfEmpty()
            orderby user.CreatedAt descending
            select new
            {
                user.UserId,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                user.RoleId,
                RoleName = role.RoleName,
                CinemaId = staff != null ? staff.CinemaId : null,
                CinemaName = cinema != null ? cinema.CinemaName : null,
                user.Status,
                user.CreatedAt
            }
        ).ToListAsync(cancellationToken);

        var list = usersData.Select(u => new ManagedUserResponse
        {
            UserId = u.UserId,
            Email = u.Email,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            RoleId = u.RoleId,
            RoleName = u.RoleName,
            CinemaId = u.CinemaId,
            CinemaName = u.CinemaName,
            Status = u.Status,
            CreatedAt = u.CreatedAt,
            IsOnline = _heartbeatTracker?.IsUserOnline(u.UserId, u.Email) ?? false
        }).ToList();

        return ServiceResult<IReadOnlyList<ManagedUserResponse>>.Ok(list);
    }

    public async Task<ServiceResult<ManagedUserResponse>> UpdateUserRoleCinemaAsync(
        string actorUserId,
        string targetUserId,
        UpdateUserRoleCinemaRequest request,
        CancellationToken cancellationToken)
    {
        var actorRoleId = await GetActorRoleIdAsync(actorUserId, cancellationToken);
        if (actorRoleId is null)
        {
            return ServiceResult<ManagedUserResponse>.Fail(
                403,
                "The authenticated account is not available for user management.",
                "ACCOUNT_PROVISIONING_ACTOR_NOT_FOUND");
        }

        var targetUser = await _dbContext.Users
            .Include(u => u.StaffProfile)
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.UserId == targetUserId, cancellationToken);

        if (targetUser is null)
        {
            return ServiceResult<ManagedUserResponse>.Fail(
                404,
                "Target user was not found.",
                "USER_NOT_FOUND");
        }

        var roleId = request.RoleId.Trim();
        var definition = await GetProvisioningDefinitionAsync(
            actorRoleId,
            roleId,
            cancellationToken);

        if (definition is null)
        {
            return ServiceResult<ManagedUserResponse>.Fail(
                403,
                "You are not allowed to assign the selected role.",
                "ROLE_ASSIGNMENT_NOT_ALLOWED");
        }

        var profileValidation = await ValidateProfileInputsAsync(
            definition,
            request.CinemaId,
            cancellationToken);

        if (profileValidation is not null)
        {
            return ServiceResult<ManagedUserResponse>.Fail(
                profileValidation.StatusCode,
                profileValidation.Message,
                profileValidation.ErrorCode);
        }

        var now = _clock.UtcNow;
        var cinemaId = NormalizeOptional(request.CinemaId);

        targetUser.RoleId = definition.RoleId;
        targetUser.UpdatedAt = now;

        if (definition.ProfileKind == DomainConstants.AccountProfileKind.Staff)
        {
            var defaultPos = definition.DefaultStaffPosition
                ?? (definition.RoleName.Contains("Manager", StringComparison.OrdinalIgnoreCase) ? "Manager" : "Staff");

            if (targetUser.StaffProfile is not null)
            {
                targetUser.StaffProfile.CinemaId = cinemaId!;
                targetUser.StaffProfile.Position = defaultPos;
                targetUser.StaffProfile.EmploymentStatus = DomainConstants.StaffEmploymentStatus.Active;
            }
            else
            {
                targetUser.StaffProfile = new StaffProfile
                {
                    StaffProfileId = NewId(DomainConstants.EntityIdPrefix.StaffProfile),
                    UserId = targetUser.UserId,
                    CinemaId = cinemaId!,
                    Position = defaultPos,
                    HireDate = DateOnly.FromDateTime(now),
                    EmploymentStatus = DomainConstants.StaffEmploymentStatus.Active
                };
                _dbContext.StaffProfiles.Add(targetUser.StaffProfile);
            }
        }
        else if (definition.ProfileKind == DomainConstants.AccountProfileKind.Customer)
        {
            if (targetUser.CustomerProfile is null)
            {
                targetUser.CustomerProfile = new CustomerProfile
                {
                    CustomerProfileId = NewId(DomainConstants.EntityIdPrefix.CustomerProfile),
                    UserId = targetUser.UserId,
                    MemberLevel = DomainConstants.MemberLevel.Standard,
                    RewardPoints = 0
                };
                _dbContext.CustomerProfiles.Add(targetUser.CustomerProfile);
            }
        }

        _dbContext.AuditLogs.Add(new AuditLog
        {
            AuditLogId = NewId(DomainConstants.EntityIdPrefix.AuditLog),
            UserId = actorUserId,
            Action = "USER_ROLE_CINEMA_UPDATED",
            EntityName = "USER",
            EntityId = targetUser.UserId,
            NewValue = JsonSerializer.Serialize(new
            {
                targetUser.Email,
                targetUser.RoleId,
                cinemaId,
                definition.ProfileKind
            }),
            CreatedAt = now
        });

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        string? cinemaName = null;
        if (cinemaId is not null)
        {
            cinemaName = await _dbContext.Cinemas
                .AsNoTracking()
                .Where(c => c.CinemaId == cinemaId)
                .Select(c => c.CinemaName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return ServiceResult<ManagedUserResponse>.Ok(
            new ManagedUserResponse
            {
                UserId = targetUser.UserId,
                Email = targetUser.Email,
                FullName = targetUser.FullName,
                PhoneNumber = targetUser.PhoneNumber,
                RoleId = targetUser.RoleId,
                RoleName = definition.RoleName,
                CinemaId = cinemaId,
                CinemaName = cinemaName,
                Status = targetUser.Status,
                CreatedAt = targetUser.CreatedAt,
                IsOnline = _heartbeatTracker?.IsUserOnline(targetUser.UserId, targetUser.Email) ?? false
            },
            "User role and cinema assignment updated successfully.");
    }

    private void AddProfile(
        string userId,
        ProvisioningDefinition definition,
        string? cinemaId)
    {
        switch (definition.ProfileKind)
        {
            case DomainConstants.AccountProfileKind.Customer:
                _dbContext.CustomerProfiles.Add(new CustomerProfile
                {
                    CustomerProfileId = NewId(DomainConstants.EntityIdPrefix.CustomerProfile),
                    UserId = userId,
                    MemberLevel = DomainConstants.MemberLevel.Standard,
                    RewardPoints = 0
                });
                break;

            case DomainConstants.AccountProfileKind.Staff:
                _dbContext.StaffProfiles.Add(new StaffProfile
                {
                    StaffProfileId = NewId(DomainConstants.EntityIdPrefix.StaffProfile),
                    UserId = userId,
                    CinemaId = cinemaId!,
                    Position = definition.DefaultStaffPosition!,
                    HireDate = DateOnly.FromDateTime(_clock.UtcNow),
                    EmploymentStatus = DomainConstants.StaffEmploymentStatus.Active
                });
                break;
        }
    }

    private async Task<ProvisioningInputFailure?> ValidateProfileInputsAsync(
        ProvisioningDefinition definition,
        string? requestedCinemaId,
        CancellationToken cancellationToken)
    {
        var cinemaId = NormalizeOptional(requestedCinemaId);

        if (definition.ProfileKind == DomainConstants.AccountProfileKind.None)
        {
            return new ProvisioningInputFailure(
                409,
                "The selected role cannot be provisioned as an account.",
                "ROLE_PROVISIONING_INVALID");
        }

        if (definition.RequiresCinema && cinemaId is null)
        {
            return new ProvisioningInputFailure(
                400,
                "CinemaId is required for the selected role.",
                "CINEMA_REQUIRED");
        }

        if (!definition.RequiresCinema && cinemaId is not null)
        {
            return new ProvisioningInputFailure(
                400,
                "CinemaId is not allowed for the selected role.",
                "CINEMA_NOT_ALLOWED");
        }

        if (definition.ProfileKind == DomainConstants.AccountProfileKind.Staff
            && string.IsNullOrWhiteSpace(definition.DefaultStaffPosition))
        {
            return new ProvisioningInputFailure(
                409,
                "The selected role has an invalid staff-profile policy.",
                "ROLE_PROVISIONING_INVALID");
        }

        if (cinemaId is not null
            && !await _dbContext.Cinemas.AnyAsync(
                cinema => cinema.CinemaId == cinemaId
                          && cinema.CinemaStatus == DomainConstants.EntityStatus.Active,
                cancellationToken))
        {
            return new ProvisioningInputFailure(
                404,
                "Active cinema was not found.",
                "CINEMA_NOT_FOUND");
        }

        return null;
    }

    private async Task<string?> GetActorRoleIdAsync(string actorUserId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.UserId == actorUserId
                           && user.Status == AuthConstants.UserStatus.Active
                           && user.EmailVerified
                           && !user.IsBlocked
                           && (user.BlockedUntil == null || user.BlockedUntil <= now))
            .Select(user => user.RoleId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ProvisioningDefinition?> GetProvisioningDefinitionAsync(
        string actorRoleId,
        string requestedRoleId,
        CancellationToken cancellationToken)
    {
        return await (
                from rule in _dbContext.RoleAssignmentRules.AsNoTracking()
                join policy in _dbContext.RoleProvisioningPolicies.AsNoTracking()
                    on rule.GranteeRoleId equals policy.RoleId
                join role in _dbContext.Roles.AsNoTracking()
                    on policy.RoleId equals role.RoleId
                where rule.GrantorRoleId == actorRoleId
                      && rule.GranteeRoleId == requestedRoleId
                      && rule.IsActive
                      && policy.IsActive
                select new ProvisioningDefinition(
                    role.RoleId,
                    role.RoleName,
                    policy.ProfileKind,
                    policy.RequiresCinema,
                    policy.DefaultStaffPosition))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private sealed record ProvisioningDefinition(
        string RoleId,
        string RoleName,
        string ProfileKind,
        bool RequiresCinema,
        string? DefaultStaffPosition);

    private sealed record ProvisioningInputFailure(
        int StatusCode,
        string Message,
        string ErrorCode);
}
