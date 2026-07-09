using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Auth;
using CinemaSystem.Infrastructure.Cinemas;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Data;
using CinemaSystem.Infrastructure.Dashboard;
using CinemaSystem.Infrastructure.Email;
using CinemaSystem.Infrastructure.Identity;
using CinemaSystem.Infrastructure.Movies;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Rooms;
using CinemaSystem.Infrastructure.Refunds;
using CinemaSystem.Infrastructure.Security;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Showtimes;
using CinemaSystem.Infrastructure.Time;
using CinemaSystem.Infrastructure.Tickets;
using CinemaSystem.Application.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Infrastructure.Extensions;

/// <summary>
/// Composition root của tầng Infrastructure: ánh xạ Application interface sang class chạy thật.
/// </summary>
/// <remarks>
/// Được gọi từ <c>CinemaSystem/Program.cs</c>. Khi Controller yêu cầu một
/// interface như IAuthService hoặc IBookingService, container tra bảng đăng ký
/// tại đây rồi chuyển tiếp sang class trong Auth/Cinemas/Movies/Rooms/Services/
/// Showtimes. Các class đó kết thúc ở CinemaDbContext hoặc dịch vụ ngoài.
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Configure(options =>
            {
                options.Issuer = ReadString(
                    configuration[$"{JwtSettings.SectionName}:Issuer"],
                    options.Issuer);
                options.Audience = ReadString(
                    configuration[$"{JwtSettings.SectionName}:Audience"],
                    options.Audience);
                options.Secret = configuration[$"{JwtSettings.SectionName}:Secret"] ?? string.Empty;
                options.AccessTokenMinutes = ReadInt(
                    configuration[$"{JwtSettings.SectionName}:AccessTokenMinutes"],
                    options.AccessTokenMinutes);
                options.RefreshTokenDays = ReadInt(
                    configuration[$"{JwtSettings.SectionName}:RefreshTokenDays"],
                    options.RefreshTokenDays);
                options.ClockSkewSeconds = ReadInt(
                    configuration[$"{JwtSettings.SectionName}:ClockSkewSeconds"],
                    options.ClockSkewSeconds);
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
            .Validate(
                options => SecretSettingsValidator.IsConfigured(options.Secret, 32),
                "JWT secret must be configured and contain at least 32 characters.")
            .Validate(options => options.AccessTokenMinutes > 0, "JWT access-token lifetime must be positive.")
            .Validate(options => options.RefreshTokenDays > 0, "JWT refresh-token lifetime must be positive.")
            .Validate(options => options.ClockSkewSeconds >= 0, "JWT clock skew cannot be negative.")
            .ValidateOnStart();

        services.AddOptions<EmailSettings>()
            .Configure(options =>
            {
                options.SmtpHost = ReadString(
                    configuration[$"{EmailSettings.SectionName}:SmtpHost"],
                    options.SmtpHost);
                options.SmtpPort = ReadInt(
                    configuration[$"{EmailSettings.SectionName}:SmtpPort"],
                    options.SmtpPort);
                options.SenderEmail = configuration[$"{EmailSettings.SectionName}:SenderEmail"] ?? string.Empty;
                options.SenderName = ReadString(
                    configuration[$"{EmailSettings.SectionName}:SenderName"],
                    options.SenderName);
                options.Password = configuration[$"{EmailSettings.SectionName}:Password"] ?? string.Empty;
                options.UseMock = ReadBool(
                    configuration[$"{EmailSettings.SectionName}:UseMock"],
                    options.UseMock);
                options.AutoConfirmEmail = ReadBool(
                    configuration[$"{EmailSettings.SectionName}:AutoConfirmEmail"],
                    options.AutoConfirmEmail);
            })
            .Validate(options => options.SmtpPort is > 0 and <= 65535, "SMTP port is invalid.");

        services.AddOptions<BookingSettings>()
            .Configure(options =>
            {
                options.OnlineSaleCutoffMinutes = ReadInt(
                    configuration[$"{BookingSettings.SectionName}:OnlineSaleCutoffMinutes"],
                    options.OnlineSaleCutoffMinutes);
                options.MaxSeatsPerCheckout = ReadInt(
                    configuration[$"{BookingSettings.SectionName}:MaxSeatsPerCheckout"],
                    options.MaxSeatsPerCheckout);
                options.PendingPaymentExpiryMinutes = ReadInt(
                    configuration[$"{BookingSettings.SectionName}:PendingPaymentExpiryMinutes"],
                    options.PendingPaymentExpiryMinutes);
                options.PendingPaymentCleanupIntervalSeconds = ReadInt(
                    configuration[$"{BookingSettings.SectionName}:PendingPaymentCleanupIntervalSeconds"],
                    options.PendingPaymentCleanupIntervalSeconds);
                options.PendingPaymentCleanupBatchSize = ReadInt(
                    configuration[$"{BookingSettings.SectionName}:PendingPaymentCleanupBatchSize"],
                    options.PendingPaymentCleanupBatchSize);
            })
            .Validate(options => options.OnlineSaleCutoffMinutes >= 0, "Online-sale cutoff cannot be negative.")
            .Validate(options => options.MaxSeatsPerCheckout > 0, "Maximum seats per checkout must be positive.")
            .Validate(options => options.PendingPaymentExpiryMinutes > 0, "Pending-payment expiry must be positive.")
            .Validate(options => options.PendingPaymentCleanupIntervalSeconds > 0, "Cleanup interval must be positive.")
            .Validate(options => options.PendingPaymentCleanupBatchSize > 0, "Cleanup batch size must be positive.")
            .ValidateOnStart();

        services.AddOptions<InitialAdminSettings>()
            .Configure(options =>
            {
                options.Email = configuration[$"{InitialAdminSettings.SectionName}:Email"] ?? string.Empty;
                options.Password = configuration[$"{InitialAdminSettings.SectionName}:Password"] ?? string.Empty;
                options.FullName = configuration[$"{InitialAdminSettings.SectionName}:FullName"] ?? string.Empty;
            });

        services.AddOptions<FileStorageSettings>()
            .Configure(options =>
            {
                options.WebRootFolder = ReadString(
                    configuration[$"{FileStorageSettings.SectionName}:WebRootFolder"],
                    options.WebRootFolder);
                options.UploadRootFolder = ReadString(
                    configuration[$"{FileStorageSettings.SectionName}:UploadRootFolder"],
                    options.UploadRootFolder);
                options.PosterFolder = ReadString(
                    configuration[$"{FileStorageSettings.SectionName}:PosterFolder"],
                    options.PosterFolder);
                options.GeneralImageFolder = ReadString(
                    configuration[$"{FileStorageSettings.SectionName}:GeneralImageFolder"],
                    options.GeneralImageFolder);

                var configuredExtensions = configuration
                    .GetSection($"{FileStorageSettings.SectionName}:AllowedImageExtensions")
                    .GetChildren()
                    .Select(item => item.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim().ToLowerInvariant())
                    .ToArray();
                if (configuredExtensions.Length > 0)
                {
                    options.AllowedImageExtensions = configuredExtensions;
                }
            })
            .Validate(
                options => options.AllowedImageExtensions.Length > 0,
                "At least one image extension must be allowed.")
            .Validate(
                options => options.AllowedImageExtensions.All(extension => extension.StartsWith('.')),
                "Image extensions must start with a period.")
            .ValidateOnStart();

        services.AddOptions<RefundSettings>()
            .Configure(options =>
            {
                options.FrontendBaseUrl = configuration[
                    $"{RefundSettings.SectionName}:FrontendBaseUrl"] ?? string.Empty;
                options.ClaimTokenMinutes = ReadInt(
                    configuration[$"{RefundSettings.SectionName}:ClaimTokenMinutes"],
                    options.ClaimTokenMinutes);
            })
            .Validate(
                options => Uri.TryCreate(options.FrontendBaseUrl, UriKind.Absolute, out _),
                "Refund frontend base URL must be an absolute URL.")
            .Validate(
                options => options.ClaimTokenMinutes >= RefundSettings.MinimumClaimTokenMinutes,
                $"Refund claim-token lifetime must be at least {RefundSettings.MinimumClaimTokenMinutes} minute.")
            .ValidateOnStart();

        services.AddOptions<TicketScanSettings>()
            .Configure(options =>
            {
                options.OpenBeforeStartMinutes = ReadNullableInt(
                    configuration[
                        $"{TicketScanSettings.SectionName}:OpenBeforeStartMinutes"]);
                options.CloseAfterEndMinutes = ReadNullableInt(
                    configuration[
                        $"{TicketScanSettings.SectionName}:CloseAfterEndMinutes"]);
            })
            .Validate(
                options => options.OpenBeforeStartMinutes.HasValue,
                "Ticket scan opening window must be configured.")
            .Validate(
                options => options.OpenBeforeStartMinutes >= 0,
                "Ticket scan opening window cannot be negative.")
            .Validate(
                options => options.CloseAfterEndMinutes.HasValue,
                "Ticket scan closing window must be configured.")
            .Validate(
                options => options.CloseAfterEndMinutes >= 0,
                "Ticket scan closing window cannot be negative.")
            .ValidateOnStart();

        services.AddDataProtection();

        // Read connection string and fail fast with clear error if missing
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            // fallback: attempt to read raw configuration key
            defaultConnection = configuration["ConnectionStrings:DefaultConnection"];
        }

        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            throw new InvalidOperationException(
                "Missing connection string 'DefaultConnection'. Add 'ConnectionStrings: { \"DefaultConnection\": \"...\" }' to appsettings.json or appsettings.{Environment}.json in the CinemaSystem project.");
        }

        services.AddDbContext<CinemaDbContext>(options =>
        {
            options.UseSqlServer(defaultConnection);
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICinemaService, CinemaService>();
        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<IBookingService, BookingService>();
        var redisConnectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<ISeatLockStore, InMemorySeatLockStore>();
        }
        else
        {
            services.AddSingleton<ISeatLockStore>(
                new RedisSeatLockStore(redisConnectionString));
        }

        services.AddScoped<ISeatService, SeatService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IShowtimeService, ShowtimeService>();
        services.AddScoped<ICinemaScopeAuthorizationService, CinemaScopeAuthorizationService>();
        services.AddScoped<IShowtimeCancellationService, ShowtimeCancellationService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<IRefundClaimService, RefundClaimService>();
        services.AddScoped<IManualRefundService, ManualRefundService>();
        services.AddScoped<IRefundProcessor, RefundProcessor>();
        services.AddScoped<IManagerDashboardService, ManagerDashboardService>();
        services.AddScoped<ITicketScanService, TicketScanService>();
        services.AddSingleton<IRefundClaimIssuer, RefundClaimIssuer>();
        services.AddSingleton<ISensitiveDataProtector, SensitiveDataProtector>();
        services.AddSingleton<IPaymentRefundGateway, UnsupportedPaymentRefundGateway>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IOtpGenerator, CryptoOtpGenerator>();
        services.AddSingleton<IClock, SystemClock>();

        // Register Sepay settings and payment related services
        var sepaySection = configuration.GetSection(SepaySettings.SectionName);
        // Manually read Sepay settings and register so project doesn't rely on IConfiguration binder extensions
        var sepaySettings = new SepaySettings();
        // Lightweight binding without IConfigurationBinder extension to avoid extra package references
        sepaySettings.WebhookSecret = sepaySection["WebhookSecret"] ?? string.Empty;
        sepaySettings.BankName = sepaySection["BankName"] ?? string.Empty;
        sepaySettings.BankAccount = sepaySection["BankAccount"] ?? string.Empty;
        sepaySettings.DevelopmentPaymentAmountOverride = ReadDecimal(
            sepaySection["DevelopmentPaymentAmountOverride"]);
        services.AddOptions<SepaySettings>()
            .Configure(options =>
            {
                options.WebhookSecret = sepaySettings.WebhookSecret;
                options.BankName = sepaySettings.BankName;
                options.BankAccount = sepaySettings.BankAccount;
                options.DevelopmentPaymentAmountOverride =
                    sepaySettings.DevelopmentPaymentAmountOverride;
            })
            .Validate(
                options => SecretSettingsValidator.IsConfigured(options.WebhookSecret, 16),
                "SePay webhook secret must be configured and contain at least 16 characters.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BankName), "SePay bank name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BankAccount), "SePay bank account is required.")
            .ValidateOnStart();

        services.AddSingleton<HmacVerifyHelper>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<ICinemaDiagnosticsService, CinemaDiagnosticsService>();
        services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
        services.AddSingleton<IWebhookSignatureVerifier, HmacVerifyHelper>();

        services.AddOptions<GeminiSettings>()
            .Configure(options =>
            {
                options.ApiKey = configuration[$"{GeminiSettings.SectionName}:ApiKey"] ?? string.Empty;
                options.ApiBaseUrl = ReadString(
                    configuration[$"{GeminiSettings.SectionName}:ApiBaseUrl"],
                    options.ApiBaseUrl);
                options.Model = ReadString(
                    configuration[$"{GeminiSettings.SectionName}:Model"],
                    options.Model);
                options.ContextMovieLimit = ReadInt(
                    configuration[$"{GeminiSettings.SectionName}:ContextMovieLimit"],
                    options.ContextMovieLimit);
            })
            .Validate(
                options => Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _),
                "Gemini API base URL must be absolute.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Gemini model is required.")
            .Validate(options => options.ContextMovieLimit > 0, "Gemini movie-context limit must be positive.");
        services.AddScoped<IChatbotService, GeminiChatbotService>();
        services.AddScoped<IAiEmailService, GeminiAiEmailService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IAiModerationService, GeminiModerationService>();
        services.AddScoped<IAdminRefundService, AdminRefundService>();
        services.AddScoped<IFbItemService, FbItemService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
        

        services.AddHostedService<CinemaSystem.Infrastructure.Jobs.MovieHighlightClassificationJob>();

        return services;
    }

    private static int ReadInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int? ReadNullableInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool ReadBool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string ReadString(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static decimal? ReadDecimal(string? value)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : null;
    }
}
