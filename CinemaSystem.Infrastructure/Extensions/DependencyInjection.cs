using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Auth;
using CinemaSystem.Infrastructure.Bookings;
using CinemaSystem.Infrastructure.Cinemas;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Data;
using CinemaSystem.Infrastructure.Email;
using CinemaSystem.Infrastructure.Identity;
using CinemaSystem.Infrastructure.Movies;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Rooms;
using CinemaSystem.Infrastructure.Security;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Showtimes;
using CinemaSystem.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Infrastructure.Extensions;

public static class DependencyInjection
{
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
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICinemaService, CinemaService>();
        services.AddScoped<IMovieService, MovieService>();
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
        services.AddScoped<SeatService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<RoomService>();
        services.AddScoped<IShowtimeService, ShowtimeService>();
        services.AddScoped<ShowtimeService>();
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

        services.AddSingleton<HmacVerifyHelper>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<ICinemaDiagnosticsService, CinemaDiagnosticsService>();
        services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
        services.AddSingleton<IWebhookSignatureVerifier, HmacVerifyHelper>();

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
