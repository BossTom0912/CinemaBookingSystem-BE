using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Customers;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Application.Settings;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hangfire;

namespace CinemaSystem.Infrastructure.Services;

public sealed class CustomerService : ICustomerService
{
    // Hằng số đánh dấu mục đích gửi OTP là để cập nhật email
    private const string EmailUpdatePurpose =
        DomainConstants.VerificationTokenPurpose.EmailUpdate;

    // Khai báo biến DbContext để tương tác với cơ sở dữ liệu
    private readonly CinemaDbContext _dbContext;
    // Khai báo dịch vụ hash mật khẩu
    private readonly IPasswordHasher _passwordHasher;
    // Khai báo dịch vụ tạo mã OTP
    private readonly IOtpGenerator _otpGenerator;
    // Khai báo dịch vụ gửi email
    private readonly IEmailSender _emailSender;
    // Khai báo dịch vụ quản lý thời gian
    private readonly IClock _clock;
    // Khai báo biến lưu trữ cấu hình bảo mật/xác thực
    private readonly CinemaSystem.Application.Settings.AuthSettings _authSettings;
    private readonly EmailTemplatesSettings _emailTemplates;
    // Khai báo BackgroundJobClient của Hangfire để thực hiện các tiến trình chạy nền
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;

    // Constructor khởi tạo và tiêm (inject) các phụ thuộc cần thiết
    public CustomerService(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IOtpGenerator otpGenerator,
        IEmailSender emailSender,
        IClock clock,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.AuthSettings> authOptions,
        Microsoft.Extensions.Options.IOptions<EmailTemplatesSettings> emailTemplateOptions,
        Hangfire.IBackgroundJobClient backgroundJobClient)
    {
        // Gán DbContext
        _dbContext = dbContext;
        // Gán bộ băm mật khẩu
        _passwordHasher = passwordHasher;
        // Gán bộ tạo OTP
        _otpGenerator = otpGenerator;
        // Gán dịch vụ gửi email
        _emailSender = emailSender;
        // Gán dịch vụ clock
        _clock = clock;
        // Gán cấu hình xác thực từ tùy chọn
        _authSettings = authOptions.Value;
        _emailTemplates = emailTemplateOptions.Value;
        // Gán Hangfire client
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<ServiceResult<CustomerProfileResponse>> GetProfileAsync(string userId, CancellationToken cancellationToken)
    {
        // Truy vấn thông tin tài khoản User dựa trên userId
        var user = await _dbContext.Users
            // Bao gồm dữ liệu chi tiết của CustomerProfile liên quan
            .Include(u => u.CustomerProfile)
            // Lấy ra bản ghi đầu tiên thỏa mãn
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        // Nếu không tìm thấy người dùng trong hệ thống
        if (user is null)
        {
            // Trả về kết quả lỗi
            return ServiceResult<CustomerProfileResponse>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Map sang DTO và trả về thông tin hồ sơ người dùng thành công
        return ServiceResult<CustomerProfileResponse>.Ok(MapToProfileResponse(user));
    }

    public async Task<ServiceResult<CustomerProfileResponse>> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        // Truy vấn thông tin tài khoản User từ DB
        var user = await _dbContext.Users
            // Lấy kèm thông tin CustomerProfile
            .Include(u => u.CustomerProfile)
            // Tìm theo ID
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        // Nếu người dùng không tồn tại
        if (user is null)
        {
            // Trả về lỗi
            return ServiceResult<CustomerProfileResponse>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Nếu có request thay đổi họ tên, tiến hành gán tên mới
        if (request.FullName is not null) user.FullName = request.FullName;
        
        // Nếu người dùng có tồn tại hồ sơ CustomerProfile
        if (user.CustomerProfile is not null)
        {
            // Cập nhật địa chỉ nếu có
            if (request.Address is not null) user.CustomerProfile.Address = request.Address;
            // Cập nhật URL ảnh đại diện nếu có
            if (request.AvatarUrl is not null) user.CustomerProfile.AvatarUrl = request.AvatarUrl;
            // Cập nhật giới tính nếu có
            if (request.Gender is not null) user.CustomerProfile.Gender = request.Gender;
            // Cập nhật ngày sinh nếu có
            if (request.DateOfBirth is not null) user.CustomerProfile.DateOfBirth = request.DateOfBirth;
        }

        // Đánh dấu thời điểm cập nhật bằng giờ UTC hiện hành
        user.UpdatedAt = _clock.UtcNow;
        // Lưu những thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Map sang DTO và trả về kết quả thành công
        return ServiceResult<CustomerProfileResponse>.Ok(MapToProfileResponse(user), "Profile updated successfully.");
    }

    public async Task<ServiceResult<object>> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        // Tìm thông tin người dùng theo khóa chính userId
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);

        // Nếu không tìm thấy
        if (user is null)
        {
            // Trả về lỗi
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Kiểm tra xem mật khẩu cũ nhập vào có khớp với mật khẩu đã băm trong DB hay không
        if (!_passwordHasher.VerifySecret(request.OldPassword, user.PasswordHash))
        {
            // Nếu không khớp, trả về lỗi mật khẩu sai
            return ServiceResult<object>.Fail(400, "Invalid old password.", "INVALID_OLD_PASSWORD");
        }

        // Kiểm tra xem mật khẩu mới có đạt các tiêu chuẩn an toàn (độ dài, ký tự đặc biệt...) hay không
        var passwordValidationError = PasswordValidator.Validate(request.NewPassword, _authSettings);
        // Nếu mật khẩu mới không hợp lệ
        if (passwordValidationError is not null)
        {
            // Trả về nguyên nhân lỗi mật khẩu yếu
            return ServiceResult<object>.Fail(400, passwordValidationError, "WEAK_PASSWORD");
        }

        // Băm mật khẩu mới để bảo mật và lưu vào đối tượng
        user.PasswordHash = _passwordHasher.HashSecret(request.NewPassword);
        // Cập nhật thời điểm đổi mật khẩu
        user.UpdatedAt = _clock.UtcNow;

        // Lưu thông tin mật khẩu mới vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả thành công
        return ServiceResult<object>.Ok(new { success = true }, "Password changed successfully.");
    }

    public async Task<ServiceResult<object>> RequestEmailUpdateAsync(string userId, UpdateEmailRequest request, CancellationToken cancellationToken)
    {
        // Truy xuất người dùng từ DB
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        // Nếu không tìm thấy
        if (user is null)
        {
            // Trả về lỗi
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Chuẩn hóa chuỗi email mới (Xóa khoảng trắng và chuyển chữ thường)
        var normalizedEmail = request.NewEmail.Trim().ToLowerInvariant();
        // Kiểm tra xem email mới này đã được sử dụng bởi một tài khoản nào khác chưa
        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && u.UserId != userId, cancellationToken))
        {
            // Trả về lỗi email đã tồn tại
            return ServiceResult<object>.Fail(409, "Email already in use.", "DUPLICATE_EMAIL");
        }

        // Gửi OTP về email cũ để xác nhận việc thay đổi (Bảo vệ Email Takeover)
        var oldEmailResult = await SendUpdateOtpAsync(user, user.Email, "EMAIL_UPDATE_OLD", cancellationToken);
        if (!oldEmailResult.Success)
        {
            return oldEmailResult;
        }

        // Thực hiện gửi mã OTP đến email mới
        return await SendUpdateOtpAsync(user, normalizedEmail, "EMAIL_UPDATE_NEW", cancellationToken);
    }

    public async Task<ServiceResult<object>> VerifyEmailUpdateAsync(string userId, VerifyEmailUpdateRequest request, CancellationToken cancellationToken)
    {
        // Truy xuất thông tin người dùng từ DB
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        // Nếu không có người dùng này
        if (user is null)
        {
            // Trả về lỗi
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Chuẩn hóa email tương tự như lúc request đổi
        var normalizedEmail = request.NewEmail.Trim().ToLowerInvariant();

        // 1. Xác thực OTP gửi tới email cũ
        var oldToken = await _dbContext.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.Purpose == "EMAIL_UPDATE_OLD" && !t.IsUsed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (oldToken is null || oldToken.ExpiredAt <= _clock.UtcNow || !_passwordHasher.VerifySecret(request.OldEmailOtp, oldToken.Token))
        {
            return ServiceResult<object>.Fail(400, "Invalid or expired OTP for the old email.", "INVALID_OLD_OTP");
        }

        // 2. Xác thực OTP gửi tới email mới
        var newToken = await _dbContext.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.Purpose == "EMAIL_UPDATE_NEW" && !t.IsUsed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (newToken is null || newToken.ExpiredAt <= _clock.UtcNow || !_passwordHasher.VerifySecret(request.Otp, newToken.Token))
        {
            return ServiceResult<object>.Fail(400, "Invalid or expired OTP for the new email.", "INVALID_NEW_OTP");
        }

        // Đánh dấu cả 2 mã OTP này đã được dùng
        oldToken.IsUsed = true;
        oldToken.VerifiedAt = _clock.UtcNow;
        newToken.IsUsed = true;
        newToken.VerifiedAt = _clock.UtcNow;

        // Tiến hành cập nhật email mới cho người dùng
        user.Email = normalizedEmail;
        // Đánh dấu tài khoản người dùng đã verify email
        user.EmailVerified = true;
        // Ghi lại thời gian thay đổi
        user.UpdatedAt = _clock.UtcNow;

        // Lưu thông tin người dùng và token đã dùng vào Database
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về thành công kèm email mới cập nhật
        return ServiceResult<object>.Ok(new { email = normalizedEmail }, "Email updated successfully.");
    }

    public async Task<ServiceResult<List<BookingHistoryResponse>>> GetBookingHistoryAsync(string userId, CancellationToken cancellationToken)
    {
        // Tìm hồ sơ khách hàng khớp với userId
        var profile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        // Nếu hồ sơ khách hàng không tồn tại
        if (profile is null)
        {
            // Trả về lỗi
            return ServiceResult<List<BookingHistoryResponse>>.Fail(404, "Customer profile not found.", "PROFILE_NOT_FOUND");
        }

        // Truy vấn lịch sử đặt vé của người dùng
        var bookings = await _dbContext.Bookings
            // Lấy kèm thông tin Suất chiếu
            .Include(b => b.Showtime)
                // Lấy kèm thông tin Phim
                .ThenInclude(s => s.Movie)
            // Lấy kèm thông tin Suất chiếu (Nhánh 2)
            .Include(b => b.Showtime)
                // Lấy kèm thông tin Phòng chiếu
                .ThenInclude(s => s.Room)
                    // Lấy kèm thông tin Rạp
                    .ThenInclude(r => r.Cinema)
            // Lấy kèm thông tin danh sách Ghế đã đặt
            .Include(b => b.BookingSeats)
                // Lấy chi tiết thông tin Ghế trong Suất chiếu đó
                .ThenInclude(bs => bs.ShowtimeSeat)
                    // Lấy thông tin thực tế của Ghế
                    .ThenInclude(ss => ss.Seat)
                        // Lấy thông tin Loại Ghế
                        .ThenInclude(s => s.SeatType)
            // Lọc ra các đơn đặt vé của profile khách hàng này
            .Where(b => b.CustomerProfileId == profile.CustomerProfileId)
            // Sắp xếp các đơn đặt vé mới nhất lên trên
            .OrderByDescending(b => b.CreatedAt)
            // Thực thi truy vấn chuyển thành danh sách (List)
            .ToListAsync(cancellationToken);

        // Map danh sách các Entity sang dạng Response (DTO)
        var response = bookings.Select(b => new BookingHistoryResponse
        {
            // Gán ID hóa đơn
            BookingId = b.BookingId,
            // Gán ID suất chiếu
            ShowtimeId = b.ShowtimeId,
            // Gán tựa phim
            MovieTitle = b.Showtime.Movie.Title,
            // Gán đường dẫn poster phim
            MoviePosterUrl = b.Showtime.Movie.PosterUrl,
            // Gán tên rạp
            CinemaName = b.Showtime.Room.Cinema.CinemaName,
            // Gán tên phòng chiếu
            RoomName = b.Showtime.Room.RoomName,
            // Gán thời gian chiếu phim
            StartTime = b.Showtime.StartTime,
            // Gán tổng tiền của hóa đơn
            TotalAmount = b.TotalAmount,
            // Gán trạng thái đặt vé (Thành công, Hủy...)
            BookingStatus = b.BookingStatus,
            // Gán thời gian đặt vé
            CreatedAt = b.CreatedAt,
            // Ánh xạ danh sách ghế ngồi đã chọn sang dạng Response
            Seats = b.BookingSeats.Select(bs => new BookedSeatResponse
            {
                // Gán ID ghế ngồi
                SeatId = bs.ShowtimeSeat.SeatId,
                // Gán số ghế
                SeatNumber = bs.ShowtimeSeat.Seat.SeatNumber.ToString(),
                // Gán tên hàng (Ký tự alphabet)
                Row = bs.ShowtimeSeat.Seat.RowLabel,
                // Gán loại ghế (Thường, VIP, Đôi...)
                SeatType = bs.ShowtimeSeat.Seat.SeatType.TypeName
            }).ToList()
        }).ToList();

        // Trả về kết quả là danh sách lịch sử vé thành công
        return ServiceResult<List<BookingHistoryResponse>>.Ok(response);
    }

    // Hàm tiện ích gửi mã OTP
    private async Task<ServiceResult<object>> SendUpdateOtpAsync(User user, string targetEmail, string purpose, CancellationToken cancellationToken)
    {
        // Lấy thời gian UTC hiện tại
        var now = _clock.UtcNow;
        // Sinh ngẫu nhiên mã OTP 6 chữ số
        var otp = _otpGenerator.GenerateSixDigitOtp();
        // Khởi tạo đối tượng mã xác thực Email
        var token = new EmailVerificationToken
        {
            // Tạo một ID ngẫu nhiên không trùng lặp cho mã xác thực này
            TokenId =
                $"{DomainConstants.EntityIdPrefix.EmailVerificationToken}_{Guid.NewGuid():N}",
            // Khớp mã này vào tài khoản người dùng
            UserId = user.UserId,
            // Băm mã OTP trước khi lưu trữ xuống DB để tăng tính bảo mật
            Token = _passwordHasher.HashSecret(otp),
            // Gán thời gian tạo là lúc này
            CreatedAt = now,
            // Tính toán thời điểm hết hạn dựa vào cấu hình cộng thêm
            ExpiredAt = now.AddSeconds(_authSettings.OtpExpirySeconds),
            // Khởi điểm mã xác thực là chưa dùng
            IsUsed = false,
            // Gán mục đích gửi mã
            Purpose = purpose,
            // Số lần thử ban đầu là 1
            AttemptCount = 1
        };

        // Thêm bản ghi xác thực vào Database Context
        _dbContext.EmailVerificationTokens.Add(token);
        // Lưu xuống Database thực tế
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Soạn nội dung Body của Email
        var body = string.Format(
            _emailTemplates.EmailUpdateBody,
            otp,
            _authSettings.OtpExpirySeconds / 60);

        try
        {
            // Sử dụng Hangfire để đưa Job chạy ngầm: thực thi việc gửi Email không làm treo phiên người dùng
            _backgroundJobClient.Enqueue<IEmailSender>(email => 
                email.SendEmailAsync(
                    targetEmail,
                    _emailTemplates.EmailUpdateSubject,
                    body,
                    CancellationToken.None));
        }
        catch
        {
            // Trả về lỗi nếu quá trình thêm Job thất bại
            return ServiceResult<object>.Fail(500, "Failed to send verification email.", "EMAIL_SEND_FAILED");
        }

        // Trả về kết quả thành công và thời gian hết hạn cho FE
        return ServiceResult<object>.Ok(new { expiresAt = token.ExpiredAt }, "Verification OTP sent.");
    }

    // Hàm tiện ích map User Entity sang CustomerProfileResponse DTO
    private static CustomerProfileResponse MapToProfileResponse(User user)
    {
        // Khởi tạo DTO chứa dữ liệu cần trả về
        return new CustomerProfileResponse
        {
            // Gán UserID
            UserId = user.UserId,
            // Gán Profile ID (Nếu không có thì để chuỗi rỗng)
            CustomerProfileId = user.CustomerProfile?.CustomerProfileId ?? string.Empty,
            // Gán Email
            Email = user.Email,
            // Gán Tên đầy đủ
            FullName = user.FullName,
            // Gán Số điện thoại
            PhoneNumber = user.PhoneNumber,
            // Gán Địa chỉ
            Address = user.CustomerProfile?.Address,
            // Gán ảnh đại diện
            AvatarUrl = user.CustomerProfile?.AvatarUrl,
            // Gán giới tính
            Gender = user.CustomerProfile?.Gender,
            // Gán Ngày sinh
            DateOfBirth = user.CustomerProfile?.DateOfBirth,
            // Gán Hạng thành viên (Hoặc mặc định là STANDARD)
            MemberLevel = user.CustomerProfile?.MemberLevel ?? DomainConstants.MemberLevel.Standard,
            // Gán Điểm thưởng (Hoặc mặc định là 0)
            RewardPoints = user.CustomerProfile?.RewardPoints ?? 0,
            // Gán trạng thái hoạt động tài khoản
            Status = user.Status,
            // Gán cờ kiểm tra tài khoản đã xác thực Email hay chưa
            EmailVerified = user.EmailVerified
        };
    }
}
