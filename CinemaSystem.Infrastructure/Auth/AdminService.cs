using System.Security.Cryptography;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using Hangfire;

namespace CinemaSystem.Infrastructure.Auth;

public sealed class AdminService : IAdminService
{
    private const string PasswordResetPurpose = "PASSWORD_RESET";

    private readonly CinemaDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IEmailService _emailService;
    private readonly IClock _clock;
    private readonly CinemaSystem.Application.Settings.AuthSettings _authSettings;
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;

    public AdminService(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IOtpGenerator otpGenerator,
        IEmailService emailService,
        IClock clock,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.AuthSettings> authOptions,
        Hangfire.IBackgroundJobClient backgroundJobClient)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _otpGenerator = otpGenerator;
        _emailService = emailService;
        _clock = clock;
        _authSettings = authOptions.Value;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<ServiceResult<object>> CreateStaffAsync(
        CreateStaffRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var duplicateEmail = await _dbContext.Users
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (duplicateEmail)
        {
            return ServiceResult<object>.Fail(
                409,
                "Email already exists.",
                "DUPLICATE_EMAIL");
        }

        var cinema = await _dbContext.Cinemas
            .OrderBy(item => item.CinemaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (cinema is null)
        {
            return ServiceResult<object>.Fail(
                400,
                "No cinema found. Seed at least one cinema before creating staff.",
                "CINEMA_NOT_FOUND");
        }

        var staffRole = await _dbContext.Roles
            .FirstOrDefaultAsync(
                role => role.RoleName == AuthConstants.Roles.Staff || role.RoleId == AuthConstants.RoleIds.Staff,
                cancellationToken);

        if (staffRole is null)
        {
            return ServiceResult<object>.Fail(
                400,
                "Staff role was not found.",
                "ROLE_NOT_FOUND");
        }

        var now = _clock.UtcNow;
        var userId = NewId("USR");
        var invitationOtp = _otpGenerator.GenerateSixDigitOtp();
        var placeholderPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var staffUser = new User
        {
            UserId = userId,
            RoleId = staffRole.RoleId,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashSecret(placeholderPassword),
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? "New Staff" : request.FullName.Trim(),
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = false,
            CreatedAt = now
        };

        var staffProfile = new StaffProfile
        {
            StaffProfileId = NewId("STF"),
            UserId = userId,
            CinemaId = cinema.CinemaId,
            Position = "Staff",
            EmploymentStatus = "ACTIVE"
        };

        var invitationToken = new EmailVerificationToken
        {
            TokenId = NewId("EVT"),
            UserId = userId,
            Token = _passwordHasher.HashSecret(invitationOtp),
            CreatedAt = now,
            ExpiredAt = now.AddMinutes(_authSettings.InvitationTokenExpiryMinutes),
            IsUsed = false,
            Purpose = PasswordResetPurpose
        };

        _dbContext.Users.Add(staffUser);
        _dbContext.StaffProfiles.Add(staffProfile);
        _dbContext.EmailVerificationTokens.Add(invitationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _backgroundJobClient.Enqueue<IEmailService>(email =>
                email.SendInvitationAsync(normalizedEmail, invitationOtp, CancellationToken.None));
        }
        catch (Exception)
        {
            _dbContext.Users.Remove(staffUser);
            _dbContext.StaffProfiles.Remove(staffProfile);
            _dbContext.EmailVerificationTokens.Remove(invitationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<object>.Fail(
                500,
                "Unable to send staff invitation email.",
                "EMAIL_SEND_FAILED");
        }

        return ServiceResult<object>.Ok(
            new { email = normalizedEmail, expiresAt = invitationToken.ExpiredAt },
            "Staff account created. Invitation email sent.",
            201);
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
