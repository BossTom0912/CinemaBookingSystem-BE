using System.Text;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Data;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Extensions;
using CinemaSystem.Middlewares;
using CinemaSystem.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

var useMockEmail = builder.Configuration.GetValue<bool>("EmailSettings:UseMock");
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

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCors", policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var jwtSettings = new JwtSettings
{
    Issuer = builder.Configuration["JwtSettings:Issuer"] ?? "CinemaSystem",
    Audience = builder.Configuration["JwtSettings:Audience"] ?? "CinemaSystem.Api",
    Secret = builder.Configuration["JwtSettings:Secret"] ?? string.Empty
};
var jwtSecret = string.IsNullOrWhiteSpace(jwtSettings.Secret)
    ? "CHANGE_ME_LOCAL_DEVELOPMENT_SECRET_32_CHARS_MINIMUM"
    : jwtSettings.Secret;
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

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
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.Policies.CanBookTicket, policy =>
        policy.RequireRole(AuthConstants.Roles.Customer));
    options.AddPolicy(AuthConstants.Policies.CanScanTicket, policy =>
        policy.RequireRole(AuthConstants.Roles.Staff, AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanManageShowtime, policy =>
        policy.RequireRole(AuthConstants.Roles.Manager, AuthConstants.Roles.Admin));
    options.AddPolicy(AuthConstants.Policies.CanManageSystem, policy =>
        policy.RequireRole(AuthConstants.Roles.Admin));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevCors");

    try
    {
        await DbInitializer.SeedAsync(app.Services, app.Environment);
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
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();

public partial class Program
{
}
