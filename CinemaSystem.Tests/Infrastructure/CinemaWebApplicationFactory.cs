using System.Security.Claims;
using System.Text;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Identity;
using CinemaSystem.Infrastructure.Persistence;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CinemaSystem.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory dùng cho integration tests.
///
/// Thay thế:
///   • SQL Server DbContext  →  EF Core InMemory (tên DB riêng biệt mỗi instance)
///   • SmtpEmailSender       →  FakeEmailCapture (bắt email trong bộ nhớ)
///   • CryptoOtpGenerator    →  FixedOtpGenerator (luôn trả "123456")
///   • JWT validation        →  PostConfigure override để tắt ValidateLifetime
///                               (tránh token hết hạn giữa chừng trong test suite)
///
/// QUAN TRỌNG về JWT claim type:
///   Các controllers dùng [Authorize(Roles = "Admin,Manager,...")]  — cơ chế này
///   check ClaimTypes.Role theo mặc định của ASP.NET Core.
///   JwtSecurityTokenHandler (MapInboundClaims = true, mặc định) map JWT claim "role"
///   thành ClaimTypes.Role khi đọc token.
///   Do đó KHÔNG được set MapInboundClaims = false hoặc thay đổi RoleClaimType —
///   nếu không [Authorize(Roles=...)] sẽ luôn trả 403.
/// </summary>
public sealed class CinemaWebApplicationFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    // ── JWT constants — phải khớp với appsettings.json và TestAuthTokens ──────
    internal const string TestJwtSecret = "integration-test-jwt-secret-with-at-least-32-characters";
    internal const string TestJwtIssuer   = "CinemaSystem";
    internal const string TestJwtAudience = "CinemaSystem.Api";
    internal const string TestSepayWebhookSecret = "test-sepay-webhook-secret";
    internal const string TestConfirmationTokenSecret =
        "integration-test-confirmation-secret-with-at-least-32-characters";

    public FakeEmailCapture EmailCapture { get; } = new();
    public string FixedOtp => "123456";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConfigureRequiredSettings(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection()
                .UseEphemeralDataProtectionProvider();

            // ── 1. Thay SQL Server DbContext bằng InMemory ────────────────────
            services.RemoveAll<DbContextOptions<CinemaDbContext>>();
            services.RemoveAll<CinemaDbContext>();

            services.AddDbContext<CinemaDbContext>(opts =>
                opts.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(w =>
                        w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // ── 2. Thay email sender bằng FakeEmailCapture ────────────────────
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailSender>(EmailCapture);
            services.AddSingleton<IEmailService>(EmailCapture);
            services.AddSingleton<IAiEmailService, FakeAiEmailService>();
            services.RemoveAll<Hangfire.IBackgroundJobClient>();
            services.AddSingleton<Hangfire.IBackgroundJobClient>(
                new InlineEmailBackgroundJobClient(EmailCapture));

            // ── 3. Thay OTP generator bằng giá trị cố định ───────────────────
            services.RemoveAll<IOtpGenerator>();
            services.AddSingleton<IOtpGenerator>(new FixedOtpGenerator(FixedOtp));

            // ── 4. Execute Hangfire jobs immediately for deterministic tests ─
            services.RemoveAll<IBackgroundJobClient>();
            services.AddSingleton<IBackgroundJobClient, ImmediateBackgroundJobClient>();

            // ── 5. Override JWT TokenValidationParameters ─────────────────────
            //
            // appsettings.json đã cấu hình cùng Secret/Issuer/Audience với TestAuthTokens
            // nên token sẽ validate đúng ngay cả không có override.
            //
            // PostConfigure chỉ cần tắt ValidateLifetime để token không hết hạn
            // trong quá trình chạy test suite dài.
            //
            // KHÔNG thay đổi MapInboundClaims (giữ mặc định = true):
            //   true  → JwtSecurityTokenHandler map JWT "role" claim → ClaimTypes.Role
            //   Sau đó [Authorize(Roles="Admin,...")]  check ClaimTypes.Role → PASS ✓
            //
            // KHÔNG thay đổi RoleClaimType (giữ mặc định = ClaimTypes.Role):
            //   Nếu đổi sang "role" thì [Authorize(Roles=...)] sẽ fail vì nó
            //   vẫn check ClaimTypes.Role bất kể RoleClaimType trong TokenValidationParameters.
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                opts =>
                {
                    var key = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(TestJwtSecret));

                    opts.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer           = true,
                        ValidIssuer              = TestJwtIssuer,
                        ValidateAudience         = true,
                        ValidAudience            = TestJwtAudience,
                        // Tắt lifetime validation — token test dùng 120 phút
                        // nhưng tránh fail do lệch clock giữa các test runner.
                        ValidateLifetime         = false,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey         = key,
                        ClockSkew                = TimeSpan.Zero,
                        // GIỮ NGUYÊN mặc định:
                        //   RoleClaimType = ClaimTypes.Role (URI dài)
                        //   NameClaimType = ClaimTypes.Name
                        // Để [Authorize(Roles=...)] hoạt động đúng với MapInboundClaims=true.
                    };
                    // GIỮ NGUYÊN MapInboundClaims = true (mặc định).
                    // Không set opts.MapInboundClaims = false.
                });
        });
    }

    internal static void ConfigureRequiredSettings(IWebHostBuilder builder)
    {
        // "Testing" → Program.cs bỏ qua block IsDevelopment() (migration/seeding).
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Server=localhost;Database=CinemaSystemTests;User Id=test;Password=test;TrustServerCertificate=True");
        builder.UseSetting("JwtSettings:Issuer", TestJwtIssuer);
        builder.UseSetting("JwtSettings:Audience", TestJwtAudience);
        builder.UseSetting("JwtSettings:Secret", TestJwtSecret);
        builder.UseSetting(
            "SecuritySettings:ConfirmationTokenSecret",
            TestConfirmationTokenSecret);
        builder.UseSetting("SepaySettings:WebhookSecret", TestSepayWebhookSecret);
        builder.UseSetting("SepaySettings:BankName", "Test Bank");
        builder.UseSetting("SepaySettings:BankAccount", "0000000000");
        builder.UseSetting("RefundSettings:FrontendBaseUrl", "https://frontend.test");
        builder.UseSetting("RefundSettings:ClaimTokenMinutes", "5");
        builder.UseSetting("TicketScanSettings:OpenBeforeStartMinutes", "30");
        builder.UseSetting("TicketScanSettings:CloseAfterEndMinutes", "0");
        builder.UseSetting("Redis:ConnectionString", string.Empty);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=CinemaSystemTests;User Id=test;Password=test;TrustServerCertificate=True",
                ["JwtSettings:Issuer"] = TestJwtIssuer,
                ["JwtSettings:Audience"] = TestJwtAudience,
                ["JwtSettings:Secret"] = TestJwtSecret,
                ["SecuritySettings:ConfirmationTokenSecret"] =
                    TestConfirmationTokenSecret,
                ["JwtSettings:AccessTokenMinutes"] = "120",
                ["JwtSettings:RefreshTokenDays"] = "7",
                ["SepaySettings:WebhookSecret"] = TestSepayWebhookSecret,
                ["SepaySettings:BankName"] = "Test Bank",
                ["SepaySettings:BankAccount"] = "0000000000",
                ["RefundSettings:FrontendBaseUrl"] = "https://frontend.test",
                ["RefundSettings:ClaimTokenMinutes"] = "5",
                ["TicketScanSettings:OpenBeforeStartMinutes"] = "30",
                ["TicketScanSettings:CloseAfterEndMinutes"] = "0",
                ["Redis:ConnectionString"] = string.Empty,
                ["SecuritySettings:ConfirmationTokenSecret"] =
                    "test-confirmation-token-secret-32-characters",
                ["EmailTemplates:SeatMaintenanceSubject"] = "Seat Maintenance Notification",
                ["EmailTemplates:SeatMaintenanceBody"] =
                    "Seat {0} is unavailable for {1}. Booking {2}. Token {3}.",
                ["EmailTemplates:ShowtimeTimeChangeSubject"] =
                    "Unexpected Update Notification",
                ["EmailTemplates:ShowtimeTimeChangeBody"] =
                    "Movie {0}. Start time changed to {1}. Booking {2}. Token {3}. Please wait for the cinema to handle it.",
                ["EmailTemplates:ShowtimeTimeChangeNoticeSubject"] =
                    "Showtime Update Notification",
                ["EmailTemplates:ShowtimeTimeChangeNoticeBody"] =
                    "Movie {0}. Start time changed to {1}. {2}.",
                ["EmailTemplates:ShowtimeRoomChangeSubject"] =
                    "Showtime Room Change",
                ["EmailTemplates:ShowtimeRoomChangeBody"] =
                    "The showtime moved to room {0}.",
                ["EmailTemplates:ShowtimeCancellationSubject"] =
                    "Showtime Cancellation",
                ["EmailTemplates:ShowtimeCancellationBody"] =
                    "The showtime was cancelled: {0}."
            });
        });
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

public sealed class ImmediateBackgroundJobClient : IBackgroundJobClient
{
    private readonly IServiceProvider _services;

    public ImmediateBackgroundJobClient(IServiceProvider services)
    {
        _services = services;
    }

    public string Create(Job job, IState state)
    {
        using var scope = _services.CreateScope();
        var target = scope.ServiceProvider.GetRequiredService(job.Type);

        try
        {
            var result = job.Method.Invoke(target, job.Args.ToArray());
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
            }
        }
        catch (System.Reflection.TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();
        }

        return Guid.NewGuid().ToString("N");
    }

    public bool ChangeState(string jobId, IState state, string expectedState)
    {
        return true;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// Email capture
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Bắt email gửi đi trong bộ nhớ.
/// Implement cả IEmailSender và IEmailService để cover mọi DI registration.
/// </summary>
public sealed class FakeEmailCapture : IEmailSender, IEmailService
{
    private readonly List<CapturedEmail> _sent = [];

    public IReadOnlyList<CapturedEmail> Emails => _sent;

    public Task SendEmailAsync(
        string toEmail, string subject, string body,
        CancellationToken cancellationToken = default)
    {
        _sent.Add(new CapturedEmail(toEmail, subject, body));
        return Task.CompletedTask;
    }

    public Task SendInvitationAsync(
        string toEmail, string invitationToken,
        CancellationToken cancellationToken = default)
    {
        _sent.Add(new CapturedEmail(toEmail, "Staff Invitation", invitationToken));
        return Task.CompletedTask;
    }
}

public sealed record CapturedEmail(string ToEmail, string Subject, string Body);

internal sealed class InlineEmailBackgroundJobClient : Hangfire.IBackgroundJobClient
{
    private readonly FakeEmailCapture _emailCapture;

    public InlineEmailBackgroundJobClient(FakeEmailCapture emailCapture)
    {
        _emailCapture = emailCapture;
    }

    public string Create(Hangfire.Common.Job job, Hangfire.States.IState state)
    {
        if (job.Type == typeof(IEmailSender) || job.Type == typeof(IEmailService))
        {
            var task = job.Method.Invoke(_emailCapture, job.Args.ToArray()) as Task;
            task?.GetAwaiter().GetResult();
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

// ═════════════════════════════════════════════════════════════════════════════
// OTP
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>OTP cố định — test xác thực email/reset password biết trước OTP.</summary>
public sealed class FixedOtpGenerator : IOtpGenerator
{
    private readonly string _otp;
    public FixedOtpGenerator(string otp) => _otp = otp;
    public string GenerateSixDigitOtp() => _otp;
}

// ═════════════════════════════════════════════════════════════════════════════
// JWT helper cho integration tests
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Tạo JWT đã ký sẵn cho từng role — dùng trong HTTP Authorization header của tests.
///
/// Dùng cùng Secret/Issuer/Audience với CinemaWebApplicationFactory.TestJwt*
/// và appsettings.json để token được chấp nhận bởi test host.
///
/// AccessTokenMinutes = 120 để token không hết hạn trong suốt test run.
/// </summary>
public static class TestAuthTokens
{
    public static string Customer(string userId = "USR_TEST_CUSTOMER")
        => Generate(userId, "customer@test.com", AuthConstants.Roles.Customer);

    public static string Staff(string userId = "USR_TEST_STAFF")
        => Generate(userId, "staff@test.com", AuthConstants.Roles.Staff);

    public static string Manager(string userId = "USR_TEST_MANAGER")
        => Generate(userId, "manager@test.com", AuthConstants.Roles.Manager);

    public static string Admin(string userId = "USR_TEST_ADMIN")
        => Generate(userId, "admin@test.com", AuthConstants.Roles.Admin);

    private static string Generate(string userId, string email, string role)
    {
        var opts = Options.Create(new JwtSettings
        {
            Issuer             = CinemaWebApplicationFactory.TestJwtIssuer,
            Audience           = CinemaWebApplicationFactory.TestJwtAudience,
            Secret             = CinemaWebApplicationFactory.TestJwtSecret,
            AccessTokenMinutes = 120,
            RefreshTokenDays   = 7
        });
        return new JwtTokenService(opts, new WallClock())
            .GenerateAccessToken(userId, email, role)
            .AccessToken;
    }

    private sealed class WallClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}

public sealed class FakeAiEmailService : IAiEmailService
{
    private readonly IEmailService _emailService;

    public FakeAiEmailService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public Task SendAiApologyEmailAsync(
        string toEmail, 
        string subject, 
        string reason, 
        string details, 
        CancellationToken cancellationToken)
    {
        var body = $"Apology Email. Reason: {reason}. Details: {details}.";
        return _emailService.SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendAiTimeChangeEmailAsync(
        string toEmail,
        string subject,
        string movieTitle,
        string oldTime,
        string newTime,
        string cutoffTime,
        string bookingId,
        string token,
        CancellationToken cancellationToken,
        string? compensationVoucherCode = null,
        string? compensationNote = null,
        string? targetSeatType = null)
    {
        var body = $"Movie {movieTitle}. Old time: {oldTime}, New time: {newTime}. Cutoff: {cutoffTime}. Booking {bookingId}. Token {token}. Voucher {compensationVoucherCode}. Seat {targetSeatType}. Note {compensationNote}.";
        return _emailService.SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendAiRoomChangeEmailAsync(
        string toEmail,
        string subject,
        string movieTitle,
        string oldRoomName,
        string newRoomName,
        string timeStr,
        string cutoffTime,
        string bookingId,
        string token,
        CancellationToken cancellationToken,
        string? compensationVoucherCode = null,
        string? compensationNote = null,
        string? targetSeatType = null)
    {
        var body = $"Movie {movieTitle}. Time: {timeStr}, Old Room: {oldRoomName}, New Room: {newRoomName}. Cutoff: {cutoffTime}. Booking {bookingId}. Token {token}. Voucher {compensationVoucherCode}. Seat {targetSeatType}. Note {compensationNote}.";
        return _emailService.SendEmailAsync(toEmail, subject, body, cancellationToken);
    }
}
