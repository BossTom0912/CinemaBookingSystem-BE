using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace CinemaSystem.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private const int OtpExpirySeconds = 120;
    private const int OtpResendCooldownSeconds = 60;
    private const int OtpMaxSendAttempts = 5;
    private const int OtpSendLockHours = 2;
    private const string EmailVerificationPurpose = "EMAIL_VERIFICATION";
    private const string PasswordResetPurpose = "PASSWORD_RESET";

    private readonly CinemaDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IClock _clock;
    private readonly JwtSettings _jwtSettings;
    private readonly bool _autoConfirmEmail;

    public AuthService(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IOtpGenerator otpGenerator,
        IEmailSender emailSender,
        IJwtTokenService jwtTokenService,
        IClock clock,
        IOptions<JwtSettings> jwtOptions,
        IConfiguration? configuration = null)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _otpGenerator = otpGenerator;
        _emailSender = emailSender;
        _jwtTokenService = jwtTokenService;
        _clock = clock;
        _jwtSettings = jwtOptions.Value;
        _autoConfirmEmail = bool.TryParse(
            configuration?["EmailSettings:AutoConfirmEmail"],
            out var autoConfirmEmail) && autoConfirmEmail;
    }

    public async Task<ServiceResult<object>> RegisterCustomerAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var now = _clock.UtcNow;
        var existingUser = await _dbContext.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            var canResendPendingRegistration =
                existingUser.Role?.RoleName == AuthConstants.Roles.Customer &&
                !existingUser.EmailVerified &&
                existingUser.Status == AuthConstants.UserStatus.PendingVerification;

            if (canResendPendingRegistration)
            {
                return await ResendPendingCustomerVerificationAsync(
                    existingUser,
                    now,
                    cancellationToken);
            }

            return ServiceResult<object>.Fail(409, "Email already exists.", "DUPLICATE_EMAIL");
        }

        var passwordValidationError = ValidatePassword(request.Password);
        if (passwordValidationError is not null)
        {
            return ServiceResult<object>.Fail(400, passwordValidationError, "WEAK_PASSWORD");
        }

        var customerRole = await GetOrCreateRoleAsync(
            AuthConstants.RoleIds.Customer,
            AuthConstants.Roles.Customer,
            "Customer account",
            cancellationToken);

        var user = new User
        {
            UserId = NewId("USR"),
            RoleId = customerRole.RoleId,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashSecret(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Status = AuthConstants.UserStatus.PendingVerification,
            EmailVerified = false,
            CreatedAt = now
        };

        var customerProfile = new CustomerProfile
        {
            CustomerProfileId = NewId("CUS"),
            UserId = user.UserId,
            MemberLevel = "STANDARD",
            RewardPoints = 0
        };

        _dbContext.Users.Add(user);
        _dbContext.CustomerProfiles.Add(customerProfile);

        if (_autoConfirmEmail)
        {
            user.EmailVerified = true;
            user.Status = AuthConstants.UserStatus.Active;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<object>.Ok(
                new { email = normalizedEmail },
                "Registration successful. Email auto-confirmed for development.",
                201);
        }

        var otp = _otpGenerator.GenerateSixDigitOtp();
        var verificationToken = new EmailVerificationToken
        {
            TokenId = NewId("EVT"),
            UserId = user.UserId,
            Token = _passwordHasher.HashSecret(otp),
            CreatedAt = now,
            ExpiredAt = now.AddSeconds(OtpExpirySeconds),
            IsUsed = false,
            Purpose = EmailVerificationPurpose,
            AttemptCount = 1
        };

        _dbContext.EmailVerificationTokens.Add(verificationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var emailResult = await TrySendVerificationOtpEmailAsync(
            normalizedEmail,
            otp,
            verificationToken.ExpiredAt,
            cancellationToken);
        if (!emailResult.Success)
        {
            await CleanupFailedRegistrationAsync(user.UserId, cancellationToken);
            return emailResult;
        }

        return ServiceResult<object>.Ok(
            new
            {
                email = normalizedEmail,
                expiresAt = verificationToken.ExpiredAt,
                attemptsRemaining = OtpMaxSendAttempts - verificationToken.AttemptCount
            },
            "Registration successful. Please verify your email with the OTP sent to your inbox.",
            201);
    }

    public async Task<ServiceResult<object>> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        if (user.EmailVerified && user.Status == AuthConstants.UserStatus.Active)
        {
            return ServiceResult<object>.Fail(409, "Email is already verified.", "EMAIL_ALREADY_VERIFIED");
        }

        var token = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose && !item.IsUsed)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null)
        {
            return ServiceResult<object>.Fail(400, "Verification OTP was not found.", "OTP_NOT_FOUND");
        }

        if (token.ExpiredAt <= _clock.UtcNow)
        {
            return ServiceResult<object>.Fail(400, "Verification OTP has expired.", "OTP_EXPIRED");
        }

        if (!_passwordHasher.VerifySecret(request.Otp, token.Token))
        {
            return ServiceResult<object>.Fail(400, "Verification OTP is invalid.", "INVALID_OTP");
        }

        token.IsUsed = true;
        token.VerifiedAt = _clock.UtcNow;
        user.EmailVerified = true;
        user.Status = AuthConstants.UserStatus.Active;
        user.UpdatedAt = _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { email = normalizedEmail }, "Email verified successfully.");
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.Users
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        if (user is null || !_passwordHasher.VerifySecret(request.Password, user.PasswordHash))
        {
            return ServiceResult<AuthResponse>.Fail(401, "Invalid email or password.", "INVALID_CREDENTIALS");
        }

        if (!_autoConfirmEmail && !user.EmailVerified)
        {
            return ServiceResult<AuthResponse>.Fail(403, "Email has not been verified.", "EMAIL_NOT_VERIFIED");
        }

        if (user.Status != AuthConstants.UserStatus.Active)
        {
            return ServiceResult<AuthResponse>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Role.RoleName);
        var refreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        var refreshToken = CreateRefreshToken(user.UserId, refreshTokenValue);

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AuthResponse>.Ok(
            new AuthResponse
            {
                AccessToken = accessToken.AccessToken,
                RefreshToken = refreshTokenValue,
                ExpiresAt = accessToken.ExpiresAt,
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role.RoleName
            },
            "Login successful.");
    }

    public async Task<ServiceResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var refreshTokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .Include(item => item.User)
            .ThenInclude(user => user.Role)
            .FirstOrDefaultAsync(item => item.TokenHash == refreshTokenHash, cancellationToken);

        if (storedToken is null)
        {
            return ServiceResult<TokenResponse>.Fail(401, "Refresh token is invalid.", "INVALID_REFRESH_TOKEN");
        }

        if (storedToken.IsRevoked)
        {
            return ServiceResult<TokenResponse>.Fail(401, "Refresh token has been revoked.", "REFRESH_TOKEN_REVOKED");
        }

        if (storedToken.ExpiresAt <= _clock.UtcNow)
        {
            return ServiceResult<TokenResponse>.Fail(401, "Refresh token has expired.", "REFRESH_TOKEN_EXPIRED");
        }

        if (storedToken.User.Status != AuthConstants.UserStatus.Active)
        {
            return ServiceResult<TokenResponse>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = _clock.UtcNow;

        var newRefreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        var newRefreshToken = CreateRefreshToken(storedToken.UserId, newRefreshTokenValue);
        _dbContext.RefreshTokens.Add(newRefreshToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(
            storedToken.User.UserId,
            storedToken.User.Email,
            storedToken.User.Role.RoleName);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<TokenResponse>.Ok(
            new TokenResponse
            {
                AccessToken = accessToken.AccessToken,
                RefreshToken = newRefreshTokenValue,
                ExpiresAt = accessToken.ExpiresAt
            },
            "Token refreshed successfully.");
    }

    public async Task<ServiceResult<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var refreshTokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(item => item.TokenHash == refreshTokenHash, cancellationToken);

        if (storedToken is not null && !storedToken.IsRevoked)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = _clock.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<object>.Ok(new { loggedOut = true }, "Logout successful.");
    }

    public async Task<ServiceResult<object>> ResendVerificationOtpAsync(ResendVerificationOtpRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        if (user.EmailVerified && user.Status == AuthConstants.UserStatus.Active)
        {
            return ServiceResult<object>.Fail(409, "Email is already verified.", "EMAIL_ALREADY_VERIFIED");
        }

        return await SendVerificationOtpAsync(
            user,
            normalizedEmail,
            "Verification OTP resent successfully.",
            cancellationToken);
    }

    private async Task<ServiceResult<object>> SendVerificationOtpAsync(
        User user,
        string normalizedEmail,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var latestToken = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var sendPolicyFailure = GetOtpSendPolicyFailure(latestToken, now);

        if (sendPolicyFailure is not null)
        {
            return sendPolicyFailure;
        }

        var oldTokens = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose && !item.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var oldToken in oldTokens)
        {
            oldToken.IsUsed = true;
        }

        var otp = _otpGenerator.GenerateSixDigitOtp();
        var verificationToken = new EmailVerificationToken
        {
            TokenId = NewId("EVT"),
            UserId = user.UserId,
            Token = _passwordHasher.HashSecret(otp),
            CreatedAt = now,
            ExpiredAt = now.AddSeconds(OtpExpirySeconds),
            IsUsed = false,
            Purpose = EmailVerificationPurpose,
            AttemptCount = GetNextOtpAttemptCount(latestToken, now)
        };

        _dbContext.EmailVerificationTokens.Add(verificationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var emailResult = await TrySendVerificationOtpEmailAsync(
            normalizedEmail,
            otp,
            verificationToken.ExpiredAt,
            cancellationToken);
        if (!emailResult.Success)
        {
            verificationToken.IsUsed = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return emailResult;
        }

        return ServiceResult<object>.Ok(
            new
            {
                email = normalizedEmail,
                expiresAt = verificationToken.ExpiredAt,
                attemptsRemaining = OtpMaxSendAttempts - verificationToken.AttemptCount
            },
            successMessage);
    }

    public async Task<ServiceResult<object>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        if (!user.EmailVerified)
        {
            return ServiceResult<object>.Fail(403, "Email has not been verified.", "EMAIL_NOT_VERIFIED");
        }

        if (user.Status != AuthConstants.UserStatus.Active)
        {
            return ServiceResult<object>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        var now = _clock.UtcNow;
        var latestToken = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == PasswordResetPurpose)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var sendPolicyFailure = GetOtpSendPolicyFailure(latestToken, now);

        if (sendPolicyFailure is not null)
        {
            return sendPolicyFailure;
        }

        var oldTokens = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == PasswordResetPurpose && !item.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var oldToken in oldTokens)
        {
            oldToken.IsUsed = true;
        }

        var otp = _otpGenerator.GenerateSixDigitOtp();
        var resetToken = new EmailVerificationToken
        {
            TokenId = NewId("EVT"),
            UserId = user.UserId,
            Token = _passwordHasher.HashSecret(otp),
            CreatedAt = now,
            ExpiredAt = now.AddSeconds(OtpExpirySeconds),
            IsUsed = false,
            Purpose = PasswordResetPurpose,
            AttemptCount = GetNextOtpAttemptCount(latestToken, now)
        };

        _dbContext.EmailVerificationTokens.Add(resetToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var emailResult = await TrySendPasswordResetOtpEmailAsync(
            normalizedEmail,
            otp,
            resetToken.ExpiredAt,
            cancellationToken);
        if (!emailResult.Success)
        {
            resetToken.IsUsed = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return emailResult;
        }

        return ServiceResult<object>.Ok(
            new
            {
                email = normalizedEmail,
                expiresAt = resetToken.ExpiredAt,
                attemptsRemaining = OtpMaxSendAttempts - resetToken.AttemptCount
            },
            "Password reset OTP sent successfully.");
    }

    public async Task<ServiceResult<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var passwordValidationError = ValidatePassword(request.NewPassword);
        if (passwordValidationError is not null)
        {
            return ServiceResult<object>.Fail(400, passwordValidationError, "WEAK_PASSWORD");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        if (user.Status != AuthConstants.UserStatus.Active)
        {
            return ServiceResult<object>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        var token = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == PasswordResetPurpose && !item.IsUsed)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null)
        {
            return ServiceResult<object>.Fail(400, "Password reset OTP was not found.", "OTP_NOT_FOUND");
        }

        if (token.ExpiredAt <= _clock.UtcNow)
        {
            return ServiceResult<object>.Fail(400, "Password reset OTP has expired.", "OTP_EXPIRED");
        }

        if (!_passwordHasher.VerifySecret(request.Otp, token.Token))
        {
            return ServiceResult<object>.Fail(400, "Password reset OTP is invalid.", "INVALID_OTP");
        }

        token.IsUsed = true;
        token.VerifiedAt = _clock.UtcNow;
        user.PasswordHash = _passwordHasher.HashSecret(request.NewPassword);
        user.UpdatedAt = _clock.UtcNow;

        if (!user.EmailVerified)
        {
            user.EmailVerified = true;
            if (user.Status == AuthConstants.UserStatus.PendingVerification)
            {
                user.Status = AuthConstants.UserStatus.Active;
            }
        }

        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(item => item.UserId == user.UserId && !item.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = _clock.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { email = normalizedEmail }, "Password reset successfully.");
    }

    private async Task<ServiceResult<object>> ResendPendingCustomerVerificationAsync(
        User user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (_autoConfirmEmail)
        {
            user.EmailVerified = true;
            user.Status = AuthConstants.UserStatus.Active;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<object>.Ok(
                new { email = user.Email },
                "Registration email auto-confirmed for development.");
        }

        var latestToken = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var resendAvailableAt = latestToken?.CreatedAt.AddSeconds(OtpResendCooldownSeconds);
        if (latestToken is not null &&
            !latestToken.IsUsed &&
            latestToken.ExpiredAt > now &&
            resendAvailableAt > now)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<object>.Ok(
                new
                {
                    email = user.Email,
                    expiresAt = latestToken.ExpiredAt,
                    attemptsRemaining = OtpMaxSendAttempts - GetEffectiveOtpAttemptCount(latestToken)
                },
                "Account is pending email verification. Use the OTP already sent to your email.");
        }

        var sendPolicyFailure = GetOtpSendPolicyFailure(latestToken, now);
        if (sendPolicyFailure is not null)
        {
            return sendPolicyFailure;
        }

        var oldTokens = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose && !item.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var oldToken in oldTokens)
        {
            oldToken.IsUsed = true;
        }

        var otp = _otpGenerator.GenerateSixDigitOtp();
        var verificationToken = new EmailVerificationToken
        {
            TokenId = NewId("EVT"),
            UserId = user.UserId,
            Token = _passwordHasher.HashSecret(otp),
            CreatedAt = now,
            ExpiredAt = now.AddSeconds(OtpExpirySeconds),
            IsUsed = false,
            Purpose = EmailVerificationPurpose,
            AttemptCount = GetNextOtpAttemptCount(latestToken, now)
        };

        _dbContext.EmailVerificationTokens.Add(verificationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var emailResult = await TrySendVerificationOtpEmailAsync(
            user.Email,
            otp,
            verificationToken.ExpiredAt,
            cancellationToken);
        if (!emailResult.Success)
        {
            verificationToken.IsUsed = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return emailResult;
        }

        return ServiceResult<object>.Ok(
            new
            {
                email = user.Email,
                expiresAt = verificationToken.ExpiredAt,
                attemptsRemaining = OtpMaxSendAttempts - verificationToken.AttemptCount
            },
            "Account is pending email verification. A new verification OTP has been sent.");
    }

    private static ServiceResult<object>? GetOtpSendPolicyFailure(
        EmailVerificationToken? latestToken,
        DateTime now)
    {
        if (latestToken is null)
        {
            return null;
        }

        if (GetEffectiveOtpAttemptCount(latestToken) >= OtpMaxSendAttempts)
        {
            var unlockAt = latestToken.CreatedAt.AddHours(OtpSendLockHours);
            if (unlockAt > now)
            {
                return CreateOtpRateLimitFailure(
                    "OTP send limit reached. Please try again later.",
                    "OTP_SEND_LIMIT_REACHED",
                    unlockAt,
                    now);
            }
        }

        var resendAvailableAt = latestToken.CreatedAt.AddSeconds(OtpResendCooldownSeconds);
        if (resendAvailableAt > now)
        {
            return CreateOtpRateLimitFailure(
                "Please wait before requesting another OTP.",
                "OTP_RESEND_COOLDOWN",
                resendAvailableAt,
                now);
        }

        return null;
    }

    private static ServiceResult<object> CreateOtpRateLimitFailure(
        string message,
        string errorCode,
        DateTime retryAt,
        DateTime now)
    {
        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((retryAt - now).TotalSeconds));

        return ServiceResult<object>.Fail(
            429,
            message,
            errorCode,
            new Dictionary<string, string[]>
            {
                ["retryAfterSeconds"] = [retryAfterSeconds.ToString()]
            });
    }

    private static int GetNextOtpAttemptCount(EmailVerificationToken? latestToken, DateTime now)
    {
        if (latestToken is null)
        {
            return 1;
        }

        if (GetEffectiveOtpAttemptCount(latestToken) >= OtpMaxSendAttempts &&
            latestToken.CreatedAt.AddHours(OtpSendLockHours) <= now)
        {
            return 1;
        }

        return GetEffectiveOtpAttemptCount(latestToken) + 1;
    }

    private static int GetEffectiveOtpAttemptCount(EmailVerificationToken token)
    {
        return Math.Max(1, token.AttemptCount);
    }

    private async Task<Role> GetOrCreateRoleAsync(
        string roleId,
        string roleName,
        string description,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(
            item => item.RoleName == roleName || item.RoleId == roleId,
            cancellationToken);

        if (role is not null)
        {
            return role;
        }

        role = new Role
        {
            RoleId = roleId,
            RoleName = roleName,
            Description = description
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }

    private RefreshToken CreateRefreshToken(string userId, string token)
    {
        var now = _clock.UtcNow;
        return new RefreshToken
        {
            RefreshTokenId = NewId("RFT"),
            UserId = userId,
            TokenHash = HashRefreshToken(token),
            IssuedAt = now,
            ExpiresAt = now.AddDays(Math.Max(1, _jwtSettings.RefreshTokenDays)),
            IsRevoked = false
        };
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }

    private async Task CleanupFailedRegistrationAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var tokens = await _dbContext.EmailVerificationTokens
                .Where(item => item.UserId == userId)
                .ToListAsync(cancellationToken);
            var customerProfile = await _dbContext.CustomerProfiles
                .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

            _dbContext.EmailVerificationTokens.RemoveRange(tokens);
            if (customerProfile is not null)
            {
                _dbContext.CustomerProfiles.Remove(customerProfile);
            }

            if (user is not null)
            {
                _dbContext.Users.Remove(user);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Keep the public error focused on SMTP failure. A cleanup failure should be diagnosed from server logs.
        }
    }

    private async Task<ServiceResult<object>> TrySendVerificationOtpEmailAsync(
        string email,
        string otp,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        var body = $"""
            Hello,

            Your Cinema Booking email verification OTP is: {otp}

            This code expires at {expiresAt:yyyy-MM-dd HH:mm:ss} UTC.

            If you did not request this registration, please ignore this email.
            """;

        try
        {
            await _emailSender.SendEmailAsync(
                email,
                "Cinema Booking - Email Verification",
                body,
                cancellationToken);
        }
        catch (Exception)
        {
            return ServiceResult<object>.Fail(
                500,
                "Unable to send verification email. Please check Gmail SMTP configuration.",
                "EMAIL_SEND_FAILED");
        }

        return ServiceResult<object>.Ok(new { email, expiresAt }, "Verification email sent.");
    }

    private async Task<ServiceResult<object>> TrySendPasswordResetOtpEmailAsync(
        string email,
        string otp,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        var body = $"""
            Hello,

            Your Cinema Booking password reset OTP is: {otp}

            This code expires at {expiresAt:yyyy-MM-dd HH:mm:ss} UTC.

            If you did not request a password reset, please ignore this email.
            """;

        try
        {
            await _emailSender.SendEmailAsync(
                email,
                "Cinema Booking - Password Reset",
                body,
                cancellationToken);
        }
        catch (Exception)
        {
            return ServiceResult<object>.Fail(
                500,
                "Unable to send password reset email. Please check Gmail SMTP configuration.",
                "EMAIL_SEND_FAILED");
        }

        return ServiceResult<object>.Ok(new { email, expiresAt }, "Password reset email sent.");
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string NewId(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }

    private static string? ValidatePassword(string password)
    {
        if (password.Length < 8)
        {
            return "Password must contain at least 8 characters.";
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            return "Password must contain uppercase, lowercase, and numeric characters.";
        }

        return null;
    }
}
