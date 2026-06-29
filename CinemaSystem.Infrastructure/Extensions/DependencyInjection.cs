using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Auth;
using CinemaSystem.Infrastructure.Bookings;
using CinemaSystem.Infrastructure.Cinemas;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Data;
using CinemaSystem.Infrastructure.Dashboard;
using CinemaSystem.Infrastructure.Email;
using CinemaSystem.Infrastructure.Identity;
using CinemaSystem.Infrastructure.Movies;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Refunds;
using CinemaSystem.Infrastructure.Rooms;
using CinemaSystem.Infrastructure.Security;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Showtimes;
using CinemaSystem.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;

namespace CinemaSystem.Infrastructure.Extensions;

public static class DependencyInjection
{
    /// <summary>
    /// Composition root for Infrastructure implementations.
    /// </summary>
    /// <remarks>
    /// Controllers depend only on Application interfaces. This method defines
    /// which concrete class receives each call at runtime, for example
    /// IAuthService -> AuthService -> CinemaDbContext and
    /// IPaymentWebhookService -> PaymentWebhookService -> IPaymentService.
    /// Change an implementation here rather than constructing Infrastructure
    /// services inside controllers.
    /// </remarks>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(options =>
        {
            options.Issuer = configuration["JwtSettings:Issuer"] ?? "CinemaSystem";
            options.Audience = configuration["JwtSettings:Audience"] ?? "CinemaSystem.Api";
            options.Secret = configuration["JwtSettings:Secret"] ?? "CHANGE_ME_LOCAL_DEVELOPMENT_SECRET_32_CHARS_MINIMUM";
            options.AccessTokenMinutes = ReadInt(configuration["JwtSettings:AccessTokenMinutes"], 15);
            options.RefreshTokenDays = ReadInt(configuration["JwtSettings:RefreshTokenDays"], 7);
        });
        services.Configure<EmailSettings>(options =>
        {
            options.SmtpHost = configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            options.SmtpPort = ReadInt(configuration["EmailSettings:SmtpPort"], 587);
            options.SenderEmail = configuration["EmailSettings:SenderEmail"] ?? string.Empty;
            options.SenderName = configuration["EmailSettings:SenderName"] ?? "Cinema Booking System";
            options.Password = configuration["EmailSettings:Password"] ?? string.Empty;
        });
        services.Configure<BookingSettings>(options =>
        {
            options.OnlineSaleCutoffMinutes = ReadInt(
                configuration["BookingSettings:OnlineSaleCutoffMinutes"],
                15);
            options.MaxSeatsPerCheckout = ReadInt(
                configuration["BookingSettings:MaxSeatsPerCheckout"],
                10);
            options.PendingPaymentExpiryMinutes = ReadInt(
                configuration["BookingSettings:PendingPaymentExpiryMinutes"],
                10);
            options.PendingPaymentCleanupIntervalSeconds = ReadInt(
                configuration["BookingSettings:PendingPaymentCleanupIntervalSeconds"],
                60);
        });
        services.Configure<RefundSettings>(options =>
        {
            options.FrontendBaseUrl = configuration["RefundSettings:FrontendBaseUrl"]
                ?? "http://localhost:5173";
            options.ClaimTokenMinutes = ReadInt(
                configuration["RefundSettings:ClaimTokenMinutes"],
                5);
        });
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

        // Controller-to-service handoff map. Each interface below is injected
        // into the matching controller; the concrete class performs the use
        // case and continues to CinemaDbContext or an external adapter.
        // AuthController -> IAuthService -> Infrastructure/Auth/AuthService:
        // tách HTTP khỏi account/OTP/JWT/refresh-token persistence.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICinemaScopeAuthorizationService, CinemaScopeAuthorizationService>();

        // BookingsController.Checkout -> ICheckoutService ->
        // Infrastructure/Bookings/CheckoutService: gom checkout transaction.
        services.AddScoped<ICheckoutService, CheckoutService>();

        // AdminController -> IAdminService -> Infrastructure/Auth/AdminService:
        // cấp tài khoản nội bộ và gửi invitation.
        services.AddScoped<IAdminService, AdminService>();

        // CustomersController -> ICustomerService ->
        // Infrastructure/Services/CustomerService: profile/credential/history.
        services.AddScoped<ICustomerService, CustomerService>();

        // Catalogue controllers -> Infrastructure catalogue services:
        // giữ EF Core query và public visibility rule ngoài API layer.
        services.AddScoped<ICinemaService, CinemaService>();
        services.AddScoped<IMovieService, MovieService>();

        // BookingsController create/detail/history -> BookingService; checkout
        // dùng CheckoutService phía trên vì hai luồng có phạm vi rule khác nhau.
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

        // Rooms/Seats/Showtimes controllers -> các service theo module; concrete
        // service tiếp tục làm việc với CinemaDbContext.
        services.AddScoped<ISeatService, SeatService>();
        services.AddScoped<SeatService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<RoomService>();
        services.AddScoped<IShowtimeService, ShowtimeService>();
        services.AddScoped<ShowtimeService>();
        services.AddScoped<IShowtimeCancellationService, ShowtimeCancellationService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<IRefundClaimService, RefundClaimService>();
        services.AddScoped<IManualRefundService, ManualRefundService>();
        services.AddSingleton<IRefundClaimIssuer, RefundClaimIssuer>();
        services.AddSingleton<ISensitiveDataProtector, SensitiveDataProtector>();
        services.AddScoped<IRefundProcessor, RefundProcessor>();
        services.AddSingleton<IPaymentRefundGateway, UnsupportedPaymentRefundGateway>();
        services.AddScoped<IManagerDashboardService, ManagerDashboardService>();

        // Auth/Admin/Customer services -> adapter email/JWT/security. Các adapter
        // được tách để đổi SMTP/crypto implementation mà không sửa controller.
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IOtpGenerator, CryptoOtpGenerator>();
        services.AddSingleton<IClock, SystemClock>();

        // Register Sepay settings and payment related services
        var sepaySection = configuration.GetSection("SepaySettings");
        // Manually read Sepay settings and register so project doesn't rely on IConfiguration binder extensions
        var sepaySettings = new SepaySettings();
        // Lightweight binding without IConfigurationBinder extension to avoid extra package references
        sepaySettings.WebhookSecret = sepaySection["WebhookSecret"] ?? string.Empty;
        sepaySettings.BankName = sepaySection["BankName"] ?? string.Empty;
        sepaySettings.BankAccount = sepaySection["BankAccount"] ?? string.Empty;
        sepaySettings.DevelopmentPaymentAmountOverride = ReadDecimal(
            sepaySection["DevelopmentPaymentAmountOverride"]);
        services.AddSingleton(sepaySettings);
        services.Configure<SepaySettings>(options =>
        {
            options.WebhookSecret = sepaySettings.WebhookSecret;
            options.BankName = sepaySettings.BankName;
            options.BankAccount = sepaySettings.BankAccount;
            options.DevelopmentPaymentAmountOverride = sepaySettings.DevelopmentPaymentAmountOverride;
        });

        // PaymentController -> PaymentWebhookService -> HmacVerifyHelper ->
        // PaymentService. Chỉ PaymentService sở hữu transaction thay đổi DB.
        services.AddSingleton<HmacVerifyHelper>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<ICinemaDiagnosticsService, CinemaDiagnosticsService>();
        services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
        services.AddSingleton<IWebhookSignatureVerifier, HmacVerifyHelper>();

        services.Configure<GeminiSettings>(options =>
        {
            options.ApiKey = configuration["GeminiSettings:ApiKey"] ?? string.Empty;
        });
        // ChatbotController -> GeminiChatbotService -> MovieService/ShowtimeService
        // -> Gemini API; chatbot tái sử dụng catalogue rule qua interface.
        services.AddScoped<IChatbotService, GeminiChatbotService>();

        return services;
    }

    private static int ReadInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static decimal? ReadDecimal(string? value)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : null;
    }
}
