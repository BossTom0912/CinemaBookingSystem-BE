using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Customers;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Runtime implementation for authenticated profile and customer-history
/// operations reached from <c>CustomersController</c>.
/// </summary>
/// <remarks>
/// Profile/password/email changes use USER, CUSTOMER_PROFILE and
/// EMAIL_VERIFICATION_TOKEN through <c>CinemaDbContext</c>. Email-change OTPs
/// are hashed through <see cref="IPasswordHasher"/> and sent through
/// <see cref="IEmailSender"/>. Booking history follows EF relationships from
/// BOOKING to showtime, movie, cinema, room and seat.
/// </remarks>
public sealed class CustomerService : ICustomerService
{
    private const int OtpExpirySeconds = 120;
    private const string EmailUpdatePurpose = "EMAIL_UPDATE";

    private readonly CinemaDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;

    public CustomerService(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IOtpGenerator otpGenerator,
        IEmailSender emailSender,
        IClock clock)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _otpGenerator = otpGenerator;
        _emailSender = emailSender;
        _clock = clock;
    }

    public async Task<ServiceResult<CustomerProfileResponse>> GetProfileAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            return ServiceResult<CustomerProfileResponse>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        return ServiceResult<CustomerProfileResponse>.Ok(MapToProfileResponse(user));
    }

    public async Task<ServiceResult<CustomerProfileResponse>> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            return ServiceResult<CustomerProfileResponse>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        if (request.FullName is not null) user.FullName = request.FullName;
        
        if (user.CustomerProfile is not null)
        {
            if (request.Address is not null) user.CustomerProfile.Address = request.Address;
            if (request.AvatarUrl is not null) user.CustomerProfile.AvatarUrl = request.AvatarUrl;
            if (request.Gender is not null) user.CustomerProfile.Gender = request.Gender;
            if (request.DateOfBirth is not null) user.CustomerProfile.DateOfBirth = request.DateOfBirth;
        }

        user.UpdatedAt = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CustomerProfileResponse>.Ok(MapToProfileResponse(user), "Profile updated successfully.");
    }

    public async Task<ServiceResult<object>> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);

        if (user is null)
        {
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        if (!_passwordHasher.VerifySecret(request.OldPassword, user.PasswordHash))
        {
            return ServiceResult<object>.Fail(400, "Invalid old password.", "INVALID_OLD_PASSWORD");
        }

        var passwordValidationError = ValidatePassword(request.NewPassword);
        if (passwordValidationError is not null)
        {
            return ServiceResult<object>.Fail(400, passwordValidationError, "WEAK_PASSWORD");
        }

        user.PasswordHash = _passwordHasher.HashSecret(request.NewPassword);
        user.UpdatedAt = _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { success = true }, "Password changed successfully.");
    }

    public async Task<ServiceResult<object>> RequestEmailUpdateAsync(string userId, UpdateEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user is null)
        {
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        var normalizedEmail = request.NewEmail.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && u.UserId != userId, cancellationToken))
        {
            return ServiceResult<object>.Fail(409, "Email already in use.", "DUPLICATE_EMAIL");
        }

        return await SendUpdateOtpAsync(user, normalizedEmail, EmailUpdatePurpose, cancellationToken);
    }

    public async Task<ServiceResult<object>> VerifyEmailUpdateAsync(string userId, VerifyEmailUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user is null)
        {
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        var normalizedEmail = request.NewEmail.Trim().ToLowerInvariant();
        var token = await _dbContext.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.Purpose == EmailUpdatePurpose && !t.IsUsed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null || token.ExpiredAt <= _clock.UtcNow || !_passwordHasher.VerifySecret(request.Otp, token.Token))
        {
            return ServiceResult<object>.Fail(400, "Invalid or expired OTP.", "INVALID_OTP");
        }

        token.IsUsed = true;
        token.VerifiedAt = _clock.UtcNow;
        user.Email = normalizedEmail;
        user.EmailVerified = true;
        user.UpdatedAt = _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { email = normalizedEmail }, "Email updated successfully.");
    }

    public async Task<ServiceResult<List<BookingHistoryResponse>>> GetBookingHistoryAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return ServiceResult<List<BookingHistoryResponse>>.Fail(404, "Customer profile not found.", "PROFILE_NOT_FOUND");
        }

        var bookings = await _dbContext.Bookings
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Room)
                    .ThenInclude(r => r.Cinema)
            .Include(b => b.BookingSeats)
                .ThenInclude(bs => bs.ShowtimeSeat)
                    .ThenInclude(ss => ss.Seat)
                        .ThenInclude(s => s.SeatType)
            .Where(b => b.CustomerProfileId == profile.CustomerProfileId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var response = bookings.Select(b => new BookingHistoryResponse
        {
            BookingId = b.BookingId,
            ShowtimeId = b.ShowtimeId,
            MovieTitle = b.Showtime.Movie.Title,
            MoviePosterUrl = b.Showtime.Movie.PosterUrl,
            CinemaName = b.Showtime.Room.Cinema.CinemaName,
            RoomName = b.Showtime.Room.RoomName,
            StartTime = b.Showtime.StartTime,
            TotalAmount = b.TotalAmount,
            BookingStatus = b.BookingStatus,
            CreatedAt = b.CreatedAt,
            Seats = b.BookingSeats.Select(bs => new BookedSeatResponse
            {
                SeatId = bs.ShowtimeSeat.SeatId,
                SeatNumber = bs.ShowtimeSeat.Seat.SeatNumber.ToString(),
                Row = bs.ShowtimeSeat.Seat.RowLabel,
                SeatType = bs.ShowtimeSeat.Seat.SeatType.TypeName
            }).ToList()
        }).ToList();

        return ServiceResult<List<BookingHistoryResponse>>.Ok(response);
    }

    private async Task<ServiceResult<object>> SendUpdateOtpAsync(User user, string targetEmail, string purpose, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var otp = _otpGenerator.GenerateSixDigitOtp();
        var token = new EmailVerificationToken
        {
            TokenId = $"EVT_{Guid.NewGuid():N}",
            UserId = user.UserId,
            Token = _passwordHasher.HashSecret(otp),
            CreatedAt = now,
            ExpiredAt = now.AddSeconds(OtpExpirySeconds),
            IsUsed = false,
            Purpose = purpose,
            AttemptCount = 1
        };

        _dbContext.EmailVerificationTokens.Add(token);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var body = $"Your verification OTP is: {otp}. It expires in {OtpExpirySeconds / 60} minutes.";

        try
        {
            await _emailSender.SendEmailAsync(targetEmail, "Cinema Booking - Email Update Verification", body, cancellationToken);
        }
        catch
        {
            return ServiceResult<object>.Fail(500, "Failed to send verification email.", "EMAIL_SEND_FAILED");
        }

        return ServiceResult<object>.Ok(new { expiresAt = token.ExpiredAt }, "Verification OTP sent.");
    }

    private static CustomerProfileResponse MapToProfileResponse(User user)
    {
        return new CustomerProfileResponse
        {
            UserId = user.UserId,
            CustomerProfileId = user.CustomerProfile?.CustomerProfileId ?? string.Empty,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Address = user.CustomerProfile?.Address,
            AvatarUrl = user.CustomerProfile?.AvatarUrl,
            Gender = user.CustomerProfile?.Gender,
            DateOfBirth = user.CustomerProfile?.DateOfBirth,
            MemberLevel = user.CustomerProfile?.MemberLevel ?? "STANDARD",
            RewardPoints = user.CustomerProfile?.RewardPoints ?? 0,
            Status = user.Status,
            EmailVerified = user.EmailVerified
        };
    }

    private static string? ValidatePassword(string password)
    {
        if (password.Length < 8) return "Password must be at least 8 characters.";
        if (!password.Any(char.IsUpper)) return "Password must contain an uppercase letter.";
        if (!password.Any(char.IsLower)) return "Password must contain a lowercase letter.";
        if (!password.Any(char.IsDigit)) return "Password must contain a digit.";
        return null;
    }
}
