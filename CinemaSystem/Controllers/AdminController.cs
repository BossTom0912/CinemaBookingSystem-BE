using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthConstants.Policies.CanManageUserAndRole)]
public sealed class AdminController : ControllerBase
{
    private const int InvitationTokenExpiryMinutes = 60;
    private const string PasswordResetPurpose = "PASSWORD_RESET";

    private readonly CinemaDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IEmailService _emailService;
    private readonly IClock _clock;

    public AdminController(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IOtpGenerator otpGenerator,
        IEmailService emailService,
        IClock clock)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _otpGenerator = otpGenerator;
        _emailService = emailService;
        _clock = clock;
    }

    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", "VALIDATION_ERROR"));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var duplicateEmail = await _dbContext.Users
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (duplicateEmail)
        {
            return Conflict(ApiResponse<object>.Fail("Email already exists.", "DUPLICATE_EMAIL"));
        }

        var cinema = await _dbContext.Cinemas
            .OrderBy(item => item.CinemaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (cinema is null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "No cinema found. Seed at least one cinema before creating staff.",
                "CINEMA_NOT_FOUND"));
        }

        var staffRole = await _dbContext.Roles
            .FirstOrDefaultAsync(
                role => role.RoleName == AuthConstants.Roles.Staff || role.RoleId == AuthConstants.RoleIds.Staff,
                cancellationToken);

        if (staffRole is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Staff role was not found.", "ROLE_NOT_FOUND"));
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
            ExpiredAt = now.AddMinutes(InvitationTokenExpiryMinutes),
            IsUsed = false,
            Purpose = PasswordResetPurpose
        };

        _dbContext.Users.Add(staffUser);
        _dbContext.StaffProfiles.Add(staffProfile);
        _dbContext.EmailVerificationTokens.Add(invitationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _emailService.SendInvitationAsync(normalizedEmail, invitationOtp, cancellationToken);
        }
        catch (Exception)
        {
            _dbContext.Users.Remove(staffUser);
            _dbContext.StaffProfiles.Remove(staffProfile);
            _dbContext.EmailVerificationTokens.Remove(invitationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail(
                    "Unable to send staff invitation email.",
                    "EMAIL_SEND_FAILED"));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(
                new { email = normalizedEmail, expiresAt = invitationToken.ExpiredAt },
                "Staff account created. Invitation email sent."));
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
