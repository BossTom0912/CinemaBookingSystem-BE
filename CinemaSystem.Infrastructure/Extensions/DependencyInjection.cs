using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Auth;
using CinemaSystem.Infrastructure.Cinemas;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Email;
using CinemaSystem.Infrastructure.Identity;
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

        services.AddDbContext<CinemaDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICinemaService, CinemaService>();
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

        return services;
    }

    private static int ReadInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
