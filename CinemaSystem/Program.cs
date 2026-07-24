using System.Text;
using CinemaSystem;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Configuration;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Infrastructure.Data;
using CinemaSystem.Infrastructure.Email;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Extensions;
using CinemaSystem.Services;
using CinemaSystem.Filters;
using CinemaSystem.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Hangfire;
using Hangfire.InMemory;

// COMPOSITION ROOT:
// 1) Nhận configuration và đăng ký API/Swagger/auth/policy tại file này.
// 2) Hạ tầng nghiệp vụ được chuyển tiếp sang AddInfrastructureServices trong
//    CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs.
// 3) Request runtime đi tiếp: middleware -> Controller trong
//    CinemaSystem/Controllers -> Application interface -> Infrastructure service.

Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args
});

// Xóa nguồn config mặc định và vô hiệu hóa reloadOnChange để tránh lỗi inotify trên Linux của Render
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(
                item => item.Key,
                item => item.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

        return new BadRequestObjectResult(ApiResponse<object>.Fail(
            "Validation failed.",
            "VALIDATION_ERROR",
            errors));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<SepayWebhookExampleOperationFilter>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT access token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.Configure<CinemaProcessingSettings>(
    builder.Configuration.GetSection(CinemaProcessingSettings.SectionName));
builder.Services.AddOptions<CinemaProcessingSettings>()
    .Validate(options => options.PreShowtimeBlockingMinutes >= 0, "Pre-showtime blocking window cannot be negative.")
    .Validate(options => options.ScreeningRoomCleaningMinutes >= 0, "Room-cleaning window cannot be negative.")
    .Validate(options => options.ShowtimeMaterialChangeThresholdMinutes >= 0, "Showtime change threshold cannot be negative.")
    .Validate(options => options.MovieNewReleaseWindowDays >= 0, "Movie new-release window cannot be negative.")
    .Validate(options => options.MovieClassificationIntervalMinutes > 0, "Movie classification interval must be positive.")
    .Validate(options => options.MovieHotViewThreshold >= 0, "Movie hot-view threshold cannot be negative.")
    .Validate(options => options.MovieTrendingViewThreshold >= 0, "Movie trending-view threshold cannot be negative.")
    .Validate(options => options.MovieHotTotalViewThreshold >= 0, "Movie total-view threshold cannot be negative.")
    .Validate(options => options.MovieHotDailyViewThreshold >= 0, "Movie daily-view threshold cannot be negative.")
    .Validate(options => options.MaxRoomCapacity > 0, "Maximum room capacity must be positive.")
    .Validate(options => options.ReviewMaxEditCount >= 0, "Review edit limit cannot be negative.")
    .Validate(options => options.ReviewSpamLockoutMinutes > 0, "Review spam lockout must be positive.")
    .ValidateOnStart();

builder.Services.Configure<AuthSettings>(
    builder.Configuration.GetSection(AuthSettings.SectionName));
builder.Services.AddOptions<AuthSettings>()
    .Validate(options => options.OtpExpirySeconds > 0, "OTP expiry must be positive.")
    .Validate(options => options.OtpResendCooldownSeconds >= 0, "OTP resend cooldown cannot be negative.")
    .Validate(options => options.OtpMaxSendAttempts > 0, "OTP send-attempt limit must be positive.")
    .Validate(options => options.PasswordMinLength > 0, "Password minimum length must be positive.")
    .Validate(
        options => options.PasswordMaxLength >= options.PasswordMinLength,
        "Password maximum length must be greater than or equal to its minimum length.")
    .ValidateOnStart();

builder.Services.Configure<SecuritySettings>(
    builder.Configuration.GetSection(SecuritySettings.SectionName));
builder.Services.AddOptions<SecuritySettings>()
    .Validate(
        options => SecretSettingsValidator.IsConfigured(options.ConfirmationTokenSecret, 32),
        "Confirmation-token secret must be configured and contain at least 32 characters.")
    .ValidateOnStart();

builder.Services.Configure<EmailTemplatesSettings>(
    builder.Configuration.GetSection(EmailTemplatesSettings.SectionName));
builder.Services.AddHostedService<PendingPaymentCleanupHostedService>();

var configuredEmailSettings = builder.Configuration
    .GetSection(EmailSettings.SectionName)
    .Get<EmailSettings>() ?? new EmailSettings();
var useMockEmail = configuredEmailSettings.UseMock;
if (useMockEmail)
{
    builder.Services.RemoveAll<IEmailSender>();
    builder.Services.AddScoped<IEmailSender, MockEmailService>();
    builder.Services.AddScoped<IEmailService, MockEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, SmtpEmailServiceAdapter>();
}

builder.Services.AddCors(options =>
{
    var corsSettings = builder.Configuration
        .GetSection(CorsSettings.SectionName)
        .Get<CorsSettings>() ?? new CorsSettings();

    foreach (var origin in corsSettings.AllowedOrigins)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"CORS origin '{origin}' is not an absolute URI.");
        }
    }

    options.AddPolicy(ApiConstants.FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(corsSettings.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());
builder.Services.AddHangfireServer();

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>() ?? new JwtSettings();
if (!SecretSettingsValidator.IsConfigured(jwtSettings.Secret, 32))
{
    throw new InvalidOperationException(
        $"{JwtSettings.SectionName}:Secret must be configured and contain at least 32 characters.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(jwtSettings.ClockSkewSeconds)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.Policies.CanViewMoviesAndShowtimes, policy =>
        policy.RequireAssertion(_ => true));
    options.AddPolicy(AuthConstants.Policies.CanRegisterOrLogin, policy =>
        policy.RequireAssertion(_ => true));
    options.AddPolicy(AuthConstants.Policies.CanBookTicket, policy =>
        policy.RequireRole(AuthConstants.Roles.Customer));
    options.AddPolicy(AuthConstants.Policies.CanSelectSeat, policy =>
    policy.RequireRole(
        AuthConstants.Roles.Customer,
        AuthConstants.Roles.Staff,
        AuthConstants.Roles.Manager,
        AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanBuyFoodAndBeverageInCheckout, policy =>
        policy.RequireRole(AuthConstants.Roles.Customer));
    options.AddPolicy(AuthConstants.Policies.CanApplyVoucher, policy =>
        policy.RequireRole(AuthConstants.Roles.Customer));
    options.AddPolicy(AuthConstants.Policies.CanPayOnline, policy =>
        policy.RequireRole(AuthConstants.Roles.Customer));
    options.AddPolicy(AuthConstants.Policies.CanViewBookingHistory, policy =>
        policy.RequireRole(AuthConstants.Roles.Customer));
    options.AddPolicy(AuthConstants.Policies.CanReviewAndFeedback, policy =>
        policy.RequireRole(AuthConstants.Roles.Customer));
    options.AddPolicy(AuthConstants.Policies.CanScanTicket, policy =>
        policy.RequireRole(AuthConstants.Roles.Staff, AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanManageMovie, policy =>
        policy.RequireRole(AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(
    AuthConstants.Policies.CanManageCinemaRoomSeat,
    policy => policy.RequireRole(
        AuthConstants.Roles.Manager,
        AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanManageShowtime, policy =>
        policy.RequireRole(AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanManageFoodAndBeverage, policy =>
        policy.RequireRole(AuthConstants.Roles.Staff, AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanManageVoucher, policy =>
        policy.RequireRole(AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanCancelShowtimeAndRefund, policy =>
        policy.RequireRole(AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanViewBranchDashboard, policy =>
        policy.RequireRole(AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanViewStaffShiftReport, policy =>
        policy.RequireRole(AuthConstants.Roles.Staff, AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanViewSystemDashboard, policy =>
        policy.RequireRole(AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanManageUserAndRole, policy =>
        policy.RequireRole(AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanManageSystem, policy =>
        policy.RequireRole(AuthConstants.Roles.Admin));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    try
    {
        using var scope = app.Services.CreateScope();
        var databaseMaintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceService>();
        await databaseMaintenance.MigrateAsync();
    }
    catch (Exception ex)
    {
        var migLogger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Program");
        migLogger.LogWarning(ex, "Database migration skipped because the database is unavailable.");
    }

    try
    {
        using var scope = app.Services.CreateScope();
        var databaseMaintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceService>();
        await databaseMaintenance.SeedAsync(app.Environment.IsDevelopment());
    }
    catch (Exception ex)
    {
        var seedLogger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Program");
        seedLogger.LogWarning(ex, "Database seeding skipped because the database is unavailable.");
    }
}

app.UseMiddleware<GlobalExceptionMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments(
            ApiConstants.SepayWebhookPath,
            StringComparison.OrdinalIgnoreCase),
        branch => branch.UseHttpsRedirection());
}

app.UseCors(ApiConstants.FrontendCorsPolicy);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

app.MapControllers();
app.Run();

public partial class Program
{
}