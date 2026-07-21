using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Infrastructure.Auth;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Identity;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Security;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterSuccess_CreatesPendingCustomerUser()
    {
        var fixture = TestFixture.Create();

        var result = await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);

        var user = await fixture.DbContext.Users.Include(item => item.Role).SingleAsync();
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal(AuthConstants.Roles.Customer, user.Role.RoleName);
        Assert.Equal(AuthConstants.UserStatus.PendingVerification, user.Status);
        Assert.False(user.EmailVerified);
        Assert.NotNull(await fixture.DbContext.CustomerProfiles.SingleOrDefaultAsync(item => item.UserId == user.UserId));
        Assert.Single(fixture.EmailSender.SentEmails);
    }

    [Fact]
    public async Task RegisterPendingEmail_UsesExistingOtpWithoutUpdatingAccount()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var result = await fixture.Service.RegisterCustomerAsync(
            new RegisterRequest
            {
                Email = "alice@example.com",
                Password = "DifferentPassword1",
                FullName = "Alice Updated",
                PhoneNumber = "0911111111"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        var user = await fixture.DbContext.Users.SingleAsync();
        Assert.Equal("Alice Nguyen", user.FullName);
        Assert.Equal("0900000000", user.PhoneNumber);
        Assert.True(fixture.PasswordHasher.VerifySecret("Password1", user.PasswordHash));
        Assert.False(fixture.PasswordHasher.VerifySecret("DifferentPassword1", user.PasswordHash));

        var token = Assert.Single(await fixture.DbContext.EmailVerificationTokens.ToListAsync());
        Assert.Equal("EMAIL_VERIFICATION", token.Purpose);
        Assert.False(token.IsUsed);
        Assert.Single(fixture.EmailSender.SentEmails);
    }

    [Fact]
    public async Task RegisterVerifiedEmail_ReturnsConflict()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();

        var result = await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("DUPLICATE_EMAIL", result.ErrorCode);
    }

    [Fact]
    public async Task RegisterDuplicatePendingEmail_ResendsVerificationOtpWithoutCreatingNewUser()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);

        var result = await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, fixture.EmailSender.SentEmails.Count);
        Assert.Single(await fixture.DbContext.Users.ToListAsync());
        Assert.Single(await fixture.DbContext.CustomerProfiles.ToListAsync());

        var tokens = await fixture.DbContext.EmailVerificationTokens
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, token => Assert.Equal("EMAIL_VERIFICATION", token.Purpose));
        Assert.True(tokens[0].IsUsed);
        Assert.False(tokens[1].IsUsed);
    }

    [Fact]
    public async Task Register_DoesNotAllowRoleInjection()
    {
        var fixture = TestFixture.Create();

        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var user = await fixture.DbContext.Users.Include(item => item.Role).SingleAsync();
        Assert.Equal(AuthConstants.RoleIds.Customer, user.RoleId);
        Assert.Equal(AuthConstants.Roles.Customer, user.Role.RoleName);
        Assert.DoesNotContain(await fixture.DbContext.Roles.ToListAsync(), role => role.RoleName == AuthConstants.Roles.Admin);
    }

    [Fact]
    public async Task Register_SendsOtpEmailThroughEmailService()
    {
        var fixture = TestFixture.Create();

        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var sentEmail = Assert.Single(fixture.EmailSender.SentEmails);
        Assert.Equal("alice@example.com", sentEmail.ToEmail);
        Assert.Contains("CinemaSystem", sentEmail.Subject, StringComparison.Ordinal);
        Assert.Contains("123456", sentEmail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_DoesNotStorePasswordPlainText()
    {
        var fixture = TestFixture.Create();

        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var user = await fixture.DbContext.Users.SingleAsync();
        Assert.NotEqual("Password1", user.PasswordHash);
        Assert.StartsWith("PBKDF2-SHA256.", user.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterEmailFailure_DoesNotLeavePendingUserOrOtp()
    {
        var fixture = TestFixture.Create(emailShouldFail: true);

        var result = await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("EMAIL_SEND_FAILED", result.ErrorCode);
        Assert.Empty(await fixture.DbContext.Users.ToListAsync());
        Assert.Empty(await fixture.DbContext.CustomerProfiles.ToListAsync());
        Assert.Empty(await fixture.DbContext.EmailVerificationTokens.ToListAsync());
    }

    [Fact]
    public async Task VerifyOtpSuccess_ActivatesUser()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var result = await fixture.Service.VerifyEmailAsync(
            new VerifyEmailRequest { Email = "alice@example.com", Otp = "123456" },
            CancellationToken.None);

        Assert.True(result.Success);
        var user = await fixture.DbContext.Users.SingleAsync();
        var token = await fixture.DbContext.EmailVerificationTokens.SingleAsync();
        Assert.True(user.EmailVerified);
        Assert.Equal(AuthConstants.UserStatus.Active, user.Status);
        Assert.True(token.IsUsed);
        Assert.NotNull(token.VerifiedAt);
    }

    [Fact]
    public async Task VerifyWrongOtp_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var result = await fixture.Service.VerifyEmailAsync(
            new VerifyEmailRequest { Email = "alice@example.com", Otp = "999999" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_OTP", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyExpiredOtp_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddSeconds(121);

        var result = await fixture.Service.VerifyEmailAsync(
            new VerifyEmailRequest { Email = "alice@example.com", Otp = "123456" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("OTP_EXPIRED", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyUsedOtp_Fails()
    {
        var fixture = TestFixture.Create();
        var role = await fixture.SeedCustomerRoleAsync();
        var user = fixture.CreateUser(role.RoleId, verified: false);
        fixture.DbContext.Users.Add(user);
        fixture.DbContext.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            TokenId = "EVT_USED",
            UserId = user.UserId,
            Token = fixture.PasswordHasher.HashSecret("123456"),
            CreatedAt = fixture.Clock.UtcNow,
            ExpiredAt = fixture.Clock.UtcNow.AddMinutes(10),
            IsUsed = true,
            Purpose = "EMAIL_VERIFICATION"
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.VerifyEmailAsync(
            new VerifyEmailRequest { Email = user.Email, Otp = "123456" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("OTP_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyAlreadyVerifiedUser_ReturnsConflict()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);
        await fixture.Service.VerifyEmailAsync(
            new VerifyEmailRequest { Email = "alice@example.com", Otp = "123456" },
            CancellationToken.None);

        var result = await fixture.Service.VerifyEmailAsync(
            new VerifyEmailRequest { Email = "alice@example.com", Otp = "123456" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("EMAIL_ALREADY_VERIFIED", result.ErrorCode);
    }

    [Fact]
    public async Task ResendVerificationOtp_EnforcesCooldownAndFiveSendLimit()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var cooldownResult = await fixture.Service.ResendVerificationOtpAsync(
            new ResendVerificationOtpRequest { Email = "alice@example.com" },
            CancellationToken.None);

        Assert.False(cooldownResult.Success);
        Assert.Equal(429, cooldownResult.StatusCode);
        Assert.Equal("OTP_RESEND_COOLDOWN", cooldownResult.ErrorCode);

        for (var attempt = 2; attempt <= 5; attempt++)
        {
            fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddSeconds(61);
            var resendResult = await fixture.Service.ResendVerificationOtpAsync(
                new ResendVerificationOtpRequest { Email = "alice@example.com" },
                CancellationToken.None);

            Assert.True(resendResult.Success);
        }

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddSeconds(61);
        var limitedResult = await fixture.Service.ResendVerificationOtpAsync(
            new ResendVerificationOtpRequest { Email = "alice@example.com" },
            CancellationToken.None);

        Assert.False(limitedResult.Success);
        Assert.Equal(429, limitedResult.StatusCode);
        Assert.Equal("OTP_SEND_LIMIT_REACHED", limitedResult.ErrorCode);

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(2);
        var unlockedResult = await fixture.Service.ResendVerificationOtpAsync(
            new ResendVerificationOtpRequest { Email = "alice@example.com" },
            CancellationToken.None);

        Assert.True(unlockedResult.Success);
        var latestToken = await fixture.DbContext.EmailVerificationTokens
            .OrderByDescending(item => item.CreatedAt)
            .FirstAsync();
        Assert.Equal(1, latestToken.AttemptCount);
    }

    [Fact]
    public async Task LoginSuccess_ReturnsJwtAndRefreshToken()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();

        var result = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RefreshToken));
        Assert.Equal(AuthConstants.Roles.Customer, result.Data.Role);
        var storedToken = Assert.Single(await fixture.DbContext.RefreshTokens.ToListAsync());
        Assert.Equal(HashRefreshToken(result.Data.RefreshToken), storedToken.TokenHash);
        Assert.NotEqual(result.Data.RefreshToken, storedToken.TokenHash);
    }

    [Fact]
    public async Task LoginWrongPassword_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();

        var result = await fixture.Service.LoginAsync(
            new LoginRequest { Email = "alice@example.com", Password = "WrongPassword1" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task LoginUnverifiedAccount_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var result = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("EMAIL_NOT_VERIFIED", result.ErrorCode);
    }

    [Fact]
    public async Task LoginInactiveOrBannedAccount_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        var user = await fixture.DbContext.Users.SingleAsync();
        user.Status = AuthConstants.UserStatus.Banned;
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ACCOUNT_NOT_ACTIVE", result.ErrorCode);
    }

    [Fact]
    public async Task JwtContainsRequiredClaims()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();

        var result = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);

        var payloadSegment = result.Data!.AccessToken.Split('.')[1];
        var payloadJson = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(payloadSegment);
        using var payload = JsonDocument.Parse(payloadJson);

        Assert.Equal(result.Data.UserId, payload.RootElement.GetProperty(JwtRegisteredClaimNames.Sub).GetString());
        Assert.Equal(result.Data.UserId, payload.RootElement.GetProperty("userId").GetString());
        Assert.Equal("alice@example.com", payload.RootElement.GetProperty(JwtRegisteredClaimNames.Email).GetString());
        Assert.Equal(AuthConstants.Roles.Customer, payload.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task ValidRefreshToken_ReturnsNewAccessAndRefreshToken()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        var login = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);

        var result = await fixture.Service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = login.Data!.RefreshToken },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEqual(login.Data.RefreshToken, result.Data!.RefreshToken);
        var oldToken = await fixture.DbContext.RefreshTokens.SingleAsync(item => item.TokenHash == HashRefreshToken(login.Data.RefreshToken));
        Assert.True(oldToken.IsRevoked);
    }

    [Fact]
    public async Task RevokedRefreshToken_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        var login = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);
        await fixture.Service.LogoutAsync(new LogoutRequest { RefreshToken = login.Data!.RefreshToken }, CancellationToken.None);

        var result = await fixture.Service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = login.Data.RefreshToken },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("REFRESH_TOKEN_REVOKED", result.ErrorCode);
    }

    [Fact]
    public async Task ExpiredRefreshToken_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        var login = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddDays(8);

        var result = await fixture.Service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = login.Data!.RefreshToken },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("REFRESH_TOKEN_EXPIRED", result.ErrorCode);
    }

    [Fact]
    public async Task LogoutRevokesRefreshToken()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        var login = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);

        var result = await fixture.Service.LogoutAsync(
            new LogoutRequest { RefreshToken = login.Data!.RefreshToken },
            CancellationToken.None);

        Assert.True(result.Success);
        var storedToken = await fixture.DbContext.RefreshTokens.SingleAsync();
        Assert.True(storedToken.IsRevoked);
        Assert.NotNull(storedToken.RevokedAt);
    }

    [Fact]
    public async Task LogoutUnknownToken_DoesNotCrash()
    {
        var fixture = TestFixture.Create();

        var result = await fixture.Service.LogoutAsync(
            new LogoutRequest { RefreshToken = "unknown" },
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ForgotPassword_ForVerifiedActiveUser_SendsOtpEmail()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();

        var result = await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "Alice@Example.com" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, fixture.EmailSender.SentEmails.Count);
        var sentEmail = fixture.EmailSender.SentEmails.Last();
        Assert.Equal("alice@example.com", sentEmail.ToEmail);
        Assert.Contains("CinemaSystem", sentEmail.Subject, StringComparison.Ordinal);
        Assert.Contains("123456", sentEmail.Body, StringComparison.Ordinal);
        var token = Assert.Single(await fixture.DbContext.EmailVerificationTokens.Where(item => !item.IsUsed).ToListAsync());
        Assert.Equal("PASSWORD_RESET", token.Purpose);
    }

    [Fact]
    public async Task ForgotPassword_EnforcesCooldown()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "alice@example.com" },
            CancellationToken.None);

        var result = await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "alice@example.com" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(429, result.StatusCode);
        Assert.Equal("OTP_RESEND_COOLDOWN", result.ErrorCode);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsNotFoundAndDoesNotCreateOtp()
    {
        var fixture = TestFixture.Create();

        var result = await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "missing@example.com" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("USER_NOT_FOUND", result.ErrorCode);
        Assert.Empty(fixture.EmailSender.SentEmails);
        Assert.Empty(await fixture.DbContext.EmailVerificationTokens.ToListAsync());
    }

    [Fact]
    public async Task ForgotPassword_UnverifiedEmail_ReturnsForbiddenAndDoesNotCreatePasswordResetOtp()
    {
        var fixture = TestFixture.Create();
        await fixture.Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);

        var result = await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "alice@example.com" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("EMAIL_NOT_VERIFIED", result.ErrorCode);
        Assert.Single(fixture.EmailSender.SentEmails);
        Assert.Empty(await fixture.DbContext.EmailVerificationTokens
            .Where(item => item.Purpose == "PASSWORD_RESET")
            .ToListAsync());
        var verificationToken = Assert.Single(await fixture.DbContext.EmailVerificationTokens.ToListAsync());
        Assert.Equal("EMAIL_VERIFICATION", verificationToken.Purpose);
    }

    [Fact]
    public async Task ResetPassword_WithValidOtp_UpdatesPasswordAndRevokesRefreshTokens()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        var login = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "alice@example.com" },
            CancellationToken.None);

        var result = await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = "alice@example.com",
                Otp = "123456",
                NewPassword = "NewPassword1"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        var oldRefreshToken = await fixture.DbContext.RefreshTokens.SingleAsync(item => item.TokenHash == HashRefreshToken(login.Data!.RefreshToken));
        Assert.True(oldRefreshToken.IsRevoked);
        Assert.NotNull(oldRefreshToken.RevokedAt);

        var oldPasswordLogin = await fixture.Service.LoginAsync(LoginRequest(), CancellationToken.None);
        Assert.False(oldPasswordLogin.Success);

        var newPasswordLogin = await fixture.Service.LoginAsync(
            new LoginRequest { Email = "alice@example.com", Password = "NewPassword1" },
            CancellationToken.None);
        Assert.True(newPasswordLogin.Success);
    }

    [Fact]
    public async Task ResetPassword_WrongOtp_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "alice@example.com" },
            CancellationToken.None);

        var result = await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = "alice@example.com",
                Otp = "999999",
                NewPassword = "NewPassword1"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_OTP", result.ErrorCode);
    }

    [Fact]
    public async Task ResetPassword_ExpiredOtp_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "alice@example.com" },
            CancellationToken.None);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddSeconds(121);

        var result = await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = "alice@example.com",
                Otp = "123456",
                NewPassword = "NewPassword1"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("OTP_EXPIRED", result.ErrorCode);
    }

    [Fact]
    public async Task ResetPassword_WeakNewPassword_Fails()
    {
        var fixture = TestFixture.Create();
        await fixture.CreateVerifiedCustomerAsync();
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "alice@example.com" },
            CancellationToken.None);

        var result = await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = "alice@example.com",
                Otp = "123456",
                NewPassword = "weak"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("WEAK_PASSWORD", result.ErrorCode);
    }

    [Fact]
    public async Task CustomerToken_CanAccessCustomerPolicyEndpoint()
    {
        await using var factory = CreateApiFactory();
        var client = factory.CreateClient();
        var token = GenerateCurrentAccessToken(AuthConstants.Roles.Customer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/api/auth-test/customer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CustomerToken_CannotAccessAdminPolicyEndpoint()
    {
        await using var factory = CreateApiFactory();
        var client = factory.CreateClient();
        var token = GenerateCurrentAccessToken(AuthConstants.Roles.Customer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/api/auth-test/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static RegisterRequest RegisterRequest()
    {
        return new RegisterRequest
        {
            Email = "Alice@Example.com",
            Password = "Password1",
            FullName = "Alice Nguyen",
            PhoneNumber = "0900000000"
        };
    }

    private static CinemaWebApplicationFactory CreateApiFactory()
    {
        return new CinemaWebApplicationFactory();
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }

    private static LoginRequest LoginRequest()
    {
        return new LoginRequest
        {
            Email = "alice@example.com",
            Password = "Password1"
        };
    }

    private static CinemaSystem.Application.Auth.GeneratedToken GenerateCurrentAccessToken(string role)
    {
        var jwtOptions = Options.Create(new JwtSettings
        {
            Issuer = "CinemaSystem",
            Audience = "CinemaSystem.Api",
            Secret = CinemaWebApplicationFactory.TestJwtSecret,
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });
        var tokenService = new JwtTokenService(jwtOptions, new FakeClock(DateTime.UtcNow));
        return tokenService.GenerateAccessToken("USR_TEST", "customer@example.com", role);
    }

    private sealed class TestFixture
    {
        private TestFixture(
            CinemaDbContext dbContext,
            FakeEmailSender emailSender,
            FakeOtpGenerator otpGenerator,
            FakeClock clock,
            Pbkdf2PasswordHasher passwordHasher,
            JwtTokenService tokenService,
            AuthService service)
        {
            DbContext = dbContext;
            EmailSender = emailSender;
            OtpGenerator = otpGenerator;
            Clock = clock;
            PasswordHasher = passwordHasher;
            TokenService = tokenService;
            Service = service;
        }

        public CinemaDbContext DbContext { get; }

        public FakeEmailSender EmailSender { get; }

        public FakeOtpGenerator OtpGenerator { get; }

        public FakeClock Clock { get; }

        public Pbkdf2PasswordHasher PasswordHasher { get; }

        public JwtTokenService TokenService { get; }

        public AuthService Service { get; }

        public static TestFixture Create(bool emailShouldFail = false)
        {
            var dbOptions = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var dbContext = new CinemaDbContext(dbOptions);
            dbContext.Roles.Add(new Role
            {
                RoleId = AuthConstants.RoleIds.Customer,
                RoleName = AuthConstants.Roles.Customer,
                Description = "Customer account"
            });
            dbContext.RoleProvisioningPolicies.Add(new RoleProvisioningPolicy
            {
                RoleId = AuthConstants.RoleIds.Customer,
                ProfileKind = DomainConstants.AccountProfileKind.Customer,
                RequiresCinema = false,
                IsActive = true,
                IsPublicRegistrationAllowed = true
            });
            dbContext.SaveChanges();
            var emailSender = new FakeEmailSender { ShouldFail = emailShouldFail };
            var otpGenerator = new FakeOtpGenerator("123456");
            var clock = new FakeClock(new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc));
            var passwordHasher = new Pbkdf2PasswordHasher();
            var jwtOptions = Options.Create(new JwtSettings
            {
                Issuer = "CinemaSystem",
                Audience = "CinemaSystem.Api",
                Secret = "unit-test-jwt-secret-with-at-least-32-characters",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 7
            });
            var tokenService = new JwtTokenService(jwtOptions, clock);
            var authOptions = Options.Create(new AuthSettings());
            var templateOptions = Options.Create(new EmailTemplatesSettings());
            var emailOptions = Options.Create(new EmailSettings());
            var backgroundJobClient = new InlineBackgroundJobClient(emailSender);
            var service = new AuthService(
                dbContext,
                passwordHasher,
                otpGenerator,
                emailSender,
                tokenService,
                clock,
                jwtOptions,
                authOptions,
                templateOptions,
                emailOptions,
                backgroundJobClient);

            return new TestFixture(dbContext, emailSender, otpGenerator, clock, passwordHasher, tokenService, service);
        }

        public async Task CreateVerifiedCustomerAsync()
        {
            await Service.RegisterCustomerAsync(RegisterRequest(), CancellationToken.None);
            await Service.VerifyEmailAsync(
                new VerifyEmailRequest { Email = "alice@example.com", Otp = "123456" },
                CancellationToken.None);
        }

        public async Task<Role> SeedCustomerRoleAsync()
        {
            var existingRole = await DbContext.Roles.SingleOrDefaultAsync(
                role => role.RoleId == AuthConstants.RoleIds.Customer);
            if (existingRole is not null)
            {
                return existingRole;
            }

            var role = new Role
            {
                RoleId = AuthConstants.RoleIds.Customer,
                RoleName = AuthConstants.Roles.Customer,
                Description = "Customer account"
            };
            DbContext.Roles.Add(role);
            DbContext.RoleProvisioningPolicies.Add(new RoleProvisioningPolicy
            {
                RoleId = role.RoleId,
                ProfileKind = DomainConstants.AccountProfileKind.Customer,
                RequiresCinema = false,
                IsActive = true,
                IsPublicRegistrationAllowed = true
            });
            await DbContext.SaveChangesAsync();
            return role;
        }

        public User CreateUser(string roleId, bool verified)
        {
            return new User
            {
                UserId = "USR_TEST",
                RoleId = roleId,
                Email = "alice@example.com",
                PasswordHash = PasswordHasher.HashSecret("Password1"),
                FullName = "Alice Nguyen",
                Status = verified ? AuthConstants.UserStatus.Active : AuthConstants.UserStatus.PendingVerification,
                EmailVerified = verified,
                CreatedAt = Clock.UtcNow
            };
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<SentEmail> SentEmails { get; } = [];

        public bool ShouldFail { get; init; }

        public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("SMTP is not configured.");
            }

            SentEmails.Add(new SentEmail(toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class InlineBackgroundJobClient : Hangfire.IBackgroundJobClient
    {
        private readonly IEmailSender _emailSender;

        public InlineBackgroundJobClient(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        public string Create(Hangfire.Common.Job job, Hangfire.States.IState state)
        {
            if (job.Type == typeof(IEmailSender)
                && job.Method.Name == nameof(IEmailSender.SendEmailAsync))
            {
                _emailSender.SendEmailAsync(
                        (string)job.Args[0],
                        (string)job.Args[1],
                        (string)job.Args[2],
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }

            return Guid.NewGuid().ToString("N");
        }

        public bool ChangeState(
            string jobId,
            Hangfire.States.IState state,
            string? expectedState)
        {
            return true;
        }
    }

    private sealed record SentEmail(string ToEmail, string Subject, string Body);

    private sealed class FakeOtpGenerator : IOtpGenerator
    {
        private readonly string _otp;

        public FakeOtpGenerator(string otp)
        {
            _otp = otp;
        }

        public string GenerateSixDigitOtp()
        {
            return _otp;
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }
}
