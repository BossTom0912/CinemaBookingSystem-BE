using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Hangfire;

namespace CinemaSystem.Infrastructure.Auth;

/// <summary>
/// Thực thi đăng ký, OTP, login thường/Google, refresh token và khôi phục mật khẩu.
/// </summary>
/// <remarks>
/// Được gọi từ <c>CinemaSystem/Controllers/AuthController.cs</c>. Luồng đi qua
/// USER/ROLE/CUSTOMER_PROFILE/EMAIL_VERIFICATION_TOKEN/REFRESH_TOKEN trong
/// <see cref="CinemaDbContext"/>, rồi sang PasswordHasher, JwtTokenService và
/// EmailSender. <c>ServiceResult</c> quay lại AuthController để trả HTTP.
/// </remarks>
public sealed class AuthService : IAuthService
{
    // Hằng số định nghĩa mục đích xác thực email
    private const string EmailVerificationPurpose =
        DomainConstants.VerificationTokenPurpose.EmailVerification;
    // Hằng số định nghĩa mục đích đặt lại mật khẩu
    private const string PasswordResetPurpose =
        DomainConstants.VerificationTokenPurpose.PasswordReset;

    // Khai báo các dependency được tiêm vào (Dependency Injection)
    private readonly CinemaDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IClock _clock;
    private readonly JwtSettings _jwtSettings;
    private readonly CinemaSystem.Application.Settings.AuthSettings _authSettings;
    private readonly EmailTemplatesSettings _emailTemplates;
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;
    private readonly bool _autoConfirmEmail;

    // Constructor khởi tạo AuthService với các dependency
    public AuthService(
        CinemaDbContext dbContext,
        IPasswordHasher passwordHasher,
        IOtpGenerator otpGenerator,
        IEmailSender emailSender,
        IJwtTokenService jwtTokenService,
        IClock clock,
        IOptions<JwtSettings> jwtOptions,
        IOptions<CinemaSystem.Application.Settings.AuthSettings> authOptions,
        IOptions<EmailTemplatesSettings> emailTemplateOptions,
        IOptions<EmailSettings> emailOptions,
        Hangfire.IBackgroundJobClient backgroundJobClient)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _otpGenerator = otpGenerator;
        _emailSender = emailSender;
        _jwtTokenService = jwtTokenService;
        _clock = clock;
        _jwtSettings = jwtOptions.Value;
        _authSettings = authOptions.Value;
        _emailTemplates = emailTemplateOptions.Value;
        _backgroundJobClient = backgroundJobClient;
        // Đọc cấu hình AutoConfirmEmail từ appsettings để tự động xác nhận email (thường dùng cho môi trường dev)
        _autoConfirmEmail = emailOptions.Value.AutoConfirmEmail;
    }

    public async Task<ServiceResult<object>> RegisterCustomerAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var customerRole = await GetPublicRegistrationRoleAsync(cancellationToken);
        if (customerRole is null)
        {
            return ServiceResult<object>.Fail(
                500,
                "Public registration role policy is not configured.",
                "PUBLIC_REGISTRATION_POLICY_MISSING");
        }

        // Chuẩn hóa địa chỉ email đầu vào (viết thường, xóa khoảng trắng thừa)
        var normalizedEmail = NormalizeEmail(request.Email);
        // Lấy thời gian hiện tại chuẩn UTC
        var now = _clock.UtcNow;
        // Truy vấn tìm người dùng trong cơ sở dữ liệu dựa trên email, bao gồm cả thông tin Role
        var existingUser = await _dbContext.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        // Kiểm tra nếu người dùng đã tồn tại
        if (existingUser is not null)
        {
            // Kiểm tra xem có thể gửi lại email xác nhận cho đăng ký đang chờ xử lý hay không
            var canResendPendingRegistration =
                existingUser.RoleId == customerRole.RoleId &&
                !existingUser.EmailVerified &&
                existingUser.Status == AuthConstants.UserStatus.PendingVerification;

            // Nếu thỏa mãn điều kiện gửi lại email xác nhận
            if (canResendPendingRegistration)
            {
                // Gọi hàm xử lý gửi lại mã xác nhận và trả về kết quả
                return await ResendPendingCustomerVerificationAsync(
                    existingUser,
                    now,
                    cancellationToken);
            }

            // Trả về lỗi nếu email đã được sử dụng và không thuộc trường hợp chờ xác nhận
            return ServiceResult<object>.Fail(409, "Email already exists.", "DUPLICATE_EMAIL");
        }

        // Thực hiện kiểm tra tính hợp lệ của mật khẩu dựa trên cấu hình
        var passwordValidationError = PasswordValidator.Validate(request.Password, _authSettings);
        // Nếu mật khẩu không hợp lệ
        if (passwordValidationError is not null)
        {
            // Trả về lỗi mật khẩu yếu
            return ServiceResult<object>.Fail(400, passwordValidationError, "WEAK_PASSWORD");
        }

        // Lấy thông tin Role của Khách hàng, nếu chưa có thì tạo mới
        // Khởi tạo đối tượng User mới
        var user = new User
        {
            // Tạo ID người dùng ngẫu nhiên với tiền tố "USR"
            UserId = NewId(DomainConstants.EntityIdPrefix.User),
            // Gán RoleId của Khách hàng
            RoleId = customerRole.RoleId,
            // Gán email đã được chuẩn hóa
            Email = normalizedEmail,
            // Băm mật khẩu người dùng trước khi lưu vào DB
            PasswordHash = _passwordHasher.HashSecret(request.Password),
            // Chuẩn hóa và gán họ tên
            FullName = request.FullName.Trim(),
            // Cập nhật số điện thoại nếu có, ngược lại để null
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            // Gán trạng thái tài khoản là Đang chờ xác minh
            Status = AuthConstants.UserStatus.PendingVerification,
            // Đánh dấu email chưa được xác minh
            EmailVerified = false,
            // Ghi nhận thời gian tạo tài khoản
            CreatedAt = now
        };

        // Khởi tạo đối tượng Hồ sơ khách hàng (CustomerProfile) mới
        var customerProfile = new CustomerProfile
        {
            // Tạo ID hồ sơ ngẫu nhiên với tiền tố "CUS"
            CustomerProfileId = NewId(DomainConstants.EntityIdPrefix.CustomerProfile),
            // Map với ID của người dùng vừa tạo
            UserId = user.UserId,
            // Khởi tạo cấp bậc thành viên cơ bản
            MemberLevel = DomainConstants.MemberLevel.Standard,
            // Khởi tạo điểm thưởng ban đầu là 0
            RewardPoints = 0
        };

        // Đưa đối tượng User vào theo dõi của EF Core DbContext
        _dbContext.Users.Add(user);
        // Đưa đối tượng CustomerProfile vào theo dõi
        _dbContext.CustomerProfiles.Add(customerProfile);

        // Nếu hệ thống được cấu hình tự động xác nhận email (thường ở môi trường phát triển)
        if (_autoConfirmEmail)
        {
            // Đánh dấu email đã được xác minh
            user.EmailVerified = true;
            // Chuyển trạng thái người dùng sang Hoạt động (Active)
            user.Status = AuthConstants.UserStatus.Active;

            // Lưu thay đổi vào Cơ sở dữ liệu
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Trả về kết quả đăng ký thành công kèm thông báo tự động xác minh
            return ServiceResult<object>.Ok(
                new { email = normalizedEmail },
                "Registration successful. Email auto-confirmed for development.",
                201);
        }

        // Tạo mã OTP gồm 6 chữ số
        var otp = _otpGenerator.GenerateSixDigitOtp();
        // Khởi tạo bản ghi lưu trữ mã OTP xác minh email
        var verificationToken = new EmailVerificationToken
        {
            // Tạo ID token ngẫu nhiên với tiền tố "EVT"
            TokenId = NewId(DomainConstants.EntityIdPrefix.EmailVerificationToken),
            // Map với ID của người dùng
            UserId = user.UserId,
            // Băm mã OTP trước khi lưu vào cơ sở dữ liệu
            Token = _passwordHasher.HashSecret(otp),
            // Ghi nhận thời gian tạo OTP
            CreatedAt = now,
            // Tính toán thời gian hết hạn của mã OTP dựa trên cấu hình
            ExpiredAt = now.AddSeconds(_authSettings.OtpExpirySeconds),
            // Đánh dấu mã OTP này chưa được sử dụng
            IsUsed = false,
            // Gán mục đích là Xác minh email
            Purpose = EmailVerificationPurpose,
            // Đặt số lần thử gửi OTP là 1
            AttemptCount = 1
        };

        // Đưa bản ghi Token vào EF Core
        _dbContext.EmailVerificationTokens.Add(verificationToken);
        // Lưu toàn bộ dữ liệu (User, Profile, Token) vào Cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Tiến hành gửi email chứa mã OTP xác minh cho người dùng
        var emailResult = await TrySendVerificationOtpEmailAsync(
            normalizedEmail,
            otp,
            verificationToken.ExpiredAt,
            cancellationToken);
        // Nếu việc gửi email thất bại
        if (!emailResult.Success)
        {
            // Xóa bỏ các dữ liệu đăng ký lỗi để giữ tính nhất quán cho DB
            await CleanupFailedRegistrationAsync(user.UserId, cancellationToken);
            // Trả về lỗi gửi email
            return emailResult;
        }

        // Trả về kết quả đăng ký thành công, yêu cầu người dùng kiểm tra email
        return ServiceResult<object>.Ok(
            new
            {
                email = normalizedEmail,
                expiresAt = verificationToken.ExpiredAt,
                attemptsRemaining = _authSettings.OtpMaxSendAttempts - verificationToken.AttemptCount
            },
            "Registration successful. Please verify your email with the OTP sent to your inbox.",
            201);
    }

    public async Task<ServiceResult<object>> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        // Chuẩn hóa địa chỉ email đầu vào
        var normalizedEmail = NormalizeEmail(request.Email);
        // Tìm kiếm người dùng theo email đã chuẩn hóa
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        // Kiểm tra nếu không tìm thấy người dùng
        if (user is null)
        {
            // Trả về lỗi người dùng không tồn tại
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Kiểm tra nếu tài khoản đã được xác minh và đang ở trạng thái hoạt động
        if (user.EmailVerified && user.Status == AuthConstants.UserStatus.Active)
        {
            // Trả về lỗi báo email đã được xác minh trước đó
            return ServiceResult<object>.Fail(409, "Email is already verified.", "EMAIL_ALREADY_VERIFIED");
        }

        // Lấy ra bản ghi OTP xác minh email mới nhất, chưa được sử dụng của người dùng này
        var token = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose && !item.IsUsed)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Kiểm tra nếu không tìm thấy bản ghi OTP nào hợp lệ
        if (token is null)
        {
            // Trả về lỗi không tìm thấy mã OTP
            return ServiceResult<object>.Fail(400, "Verification OTP was not found.", "OTP_NOT_FOUND");
        }

        // Kiểm tra xem thời gian hiện tại đã vượt qua thời gian hết hạn của OTP chưa
        if (token.ExpiredAt <= _clock.UtcNow)
        {
            // Trả về lỗi OTP đã hết hạn
            return ServiceResult<object>.Fail(400, "Verification OTP has expired.", "OTP_EXPIRED");
        }

        // Xác thực mã OTP người dùng nhập vào có khớp với mã Hash trong cơ sở dữ liệu không
        if (!_passwordHasher.VerifySecret(request.Otp, token.Token))
        {
            // Trả về lỗi OTP không hợp lệ
            return ServiceResult<object>.Fail(400, "Verification OTP is invalid.", "INVALID_OTP");
        }

        // Đánh dấu mã OTP đã được sử dụng thành công
        token.IsUsed = true;
        // Cập nhật thời điểm xác minh thành công
        token.VerifiedAt = _clock.UtcNow;
        // Cập nhật trạng thái email của người dùng thành Đã xác minh
        user.EmailVerified = true;
        // Cập nhật trạng thái tài khoản thành Đang hoạt động (Active)
        user.Status = AuthConstants.UserStatus.Active;
        // Ghi nhận thời gian cập nhật thông tin người dùng
        user.UpdatedAt = _clock.UtcNow;

        // Lưu các thay đổi vào Cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả xác minh email thành công
        return ServiceResult<object>.Ok(new { email = normalizedEmail }, "Email verified successfully.");
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        // Chuẩn hóa địa chỉ email đầu vào
        var normalizedEmail = NormalizeEmail(request.Email);
        // Truy vấn lấy thông tin người dùng và Role tương ứng
        var user = await _dbContext.Users
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        // Kiểm tra nếu không tìm thấy người dùng hoặc sai mật khẩu
        if (user is null || !_passwordHasher.VerifySecret(request.Password, user.PasswordHash))
        {
            // Trả về lỗi sai thông tin đăng nhập
            return ServiceResult<AuthResponse>.Fail(401, "Invalid email or password.", "INVALID_CREDENTIALS");
        }

        // Kiểm tra nếu email chưa được xác minh (và hệ thống không bật chế độ tự động xác nhận email)
        if (!_autoConfirmEmail && !user.EmailVerified)
        {
            // Trả về lỗi yêu cầu xác minh email trước khi đăng nhập
            return ServiceResult<AuthResponse>.Fail(403, "Email has not been verified.", "EMAIL_NOT_VERIFIED");
        }

        // Kiểm tra nếu tài khoản đang bị khóa hoặc không ở trạng thái Active
        if (user.Status != AuthConstants.UserStatus.Active)
        {
            // Trả về lỗi tài khoản không khả dụng
            return ServiceResult<AuthResponse>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        // Tạo chuỗi Access Token (JWT) cho phiên đăng nhập
        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Role.RoleName);
        // Tạo mã Refresh Token mới
        var refreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        // Khởi tạo bản ghi lưu trữ Refresh Token vào cơ sở dữ liệu
        var refreshToken = CreateRefreshToken(user.UserId, refreshTokenValue);

        // Đưa Refresh Token vào DbContext để lưu lại
        _dbContext.RefreshTokens.Add(refreshToken);
        // Lưu thông tin phiên đăng nhập vào Cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về thông tin đăng nhập thành công bao gồm các loại Token và thông tin User
        return ServiceResult<AuthResponse>.Ok(
            new AuthResponse
            {
                AccessToken = accessToken.AccessToken,
                RefreshToken = refreshTokenValue,
                ExpiresAt = accessToken.ExpiresAt,
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = AuthConstants.Roles.Normalize(user.Role.RoleName)
            },
            "Login successful.");
    }

    public async Task<ServiceResult<AuthResponse>> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        // Khai báo biến lưu trữ thông tin Payload sau khi giải mã token của Google
        GoogleJsonWebSignature.Payload payload;
        try
        {
            // Validate (Xác thực) ID Token do Google trả về từ Client
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
        }
        catch (InvalidJwtException)
        {
            // Bắt lỗi Token không hợp lệ và trả về 401
            return ServiceResult<AuthResponse>.Fail(401, "Invalid Google ID token.", "INVALID_GOOGLE_TOKEN");
        }

        // Lấy địa chỉ email từ Payload và chuẩn hóa
        var normalizedEmail = NormalizeEmail(payload.Email);
        // Truy vấn tìm kiếm người dùng trong hệ thống qua email này
        var user = await _dbContext.Users
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        // Nếu người dùng đăng nhập bằng Google lần đầu (chưa tồn tại tài khoản)
        if (user is null)
        {
            // Lấy hoặc tạo mới Role Khách hàng
            var customerRole = await GetPublicRegistrationRoleAsync(cancellationToken);
            if (customerRole is null)
            {
                return ServiceResult<AuthResponse>.Fail(
                    500,
                    "Public registration role policy is not configured.",
                    "PUBLIC_REGISTRATION_POLICY_MISSING");
            }

            // Khởi tạo đối tượng User mới cho tài khoản Google này
            user = new User
            {
                // Sinh mã ID người dùng mới
                UserId = NewId(DomainConstants.EntityIdPrefix.User),
                // Gán RoleId Khách hàng
                RoleId = customerRole.RoleId,
                // Gán email đã chuẩn hóa
                Email = normalizedEmail,
                // Tạo một mật khẩu ngẫu nhiên để lưu (vì đăng nhập bằng Google không cần pass)
                PasswordHash = _passwordHasher.HashSecret(Guid.NewGuid().ToString()),
                // Gán Tên hiển thị từ Google, nếu không có để mặc định "Google User"
                FullName = payload.Name?.Trim() ?? "Google User",
                // Đặt trạng thái Active ngay lập tức
                Status = AuthConstants.UserStatus.Active,
                // Đánh dấu email đã được xác minh (Google đã làm việc này)
                EmailVerified = true,
                // Ghi nhận thời điểm tạo tài khoản
                CreatedAt = _clock.UtcNow
            };

            // Khởi tạo đối tượng Hồ sơ khách hàng (CustomerProfile) mới
            var customerProfile = new CustomerProfile
            {
                // Tạo ID hồ sơ ngẫu nhiên
                CustomerProfileId = NewId(DomainConstants.EntityIdPrefix.CustomerProfile),
                // Map với ID người dùng vừa tạo
                UserId = user.UserId,
                // Đặt cấp độ thành viên cơ bản
                MemberLevel = DomainConstants.MemberLevel.Standard,
                // Gán điểm thưởng khởi điểm
                RewardPoints = 0
            };

            // Thêm người dùng mới vào Database
            _dbContext.Users.Add(user);
            // Thêm Hồ sơ người dùng vào Database
            _dbContext.CustomerProfiles.Add(customerProfile);
            // Lưu lại những thay đổi này
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Gán lại tham chiếu Role để dùng cho quá trình sinh Token bên dưới
            user.Role = customerRole;
        }

        // Kiểm tra xem tài khoản này có đang bị khóa hay không
        if (user.Status != AuthConstants.UserStatus.Active)
        {
            // Trả về lỗi nếu tài khoản không hoạt động
            return ServiceResult<AuthResponse>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        // Sinh JWT Access Token cho phiên đăng nhập Google
        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Role.RoleName);
        // Sinh Refresh Token ngẫu nhiên
        var refreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        // Khởi tạo đối tượng lưu trữ Refresh Token
        var refreshToken = CreateRefreshToken(user.UserId, refreshTokenValue);

        // Lưu Refresh Token vào Database
        _dbContext.RefreshTokens.Add(refreshToken);
        // Lưu các thao tác vào Database
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về dữ liệu phiên đăng nhập
        return ServiceResult<AuthResponse>.Ok(
            new AuthResponse
            {
                AccessToken = accessToken.AccessToken,
                RefreshToken = refreshTokenValue,
                ExpiresAt = accessToken.ExpiresAt,
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = AuthConstants.Roles.Normalize(user.Role.RoleName)
            },
            "Google login successful.");
    }

    public async Task<ServiceResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        // Băm chuỗi Refresh Token được gửi lên để so sánh với Database
        var refreshTokenHash = HashRefreshToken(request.RefreshToken);
        // Truy vấn tìm bản ghi Refresh Token kèm thông tin Người dùng và Phân quyền
        var storedToken = await _dbContext.RefreshTokens
            .Include(item => item.User)
            .ThenInclude(user => user.Role)
            .FirstOrDefaultAsync(item => item.TokenHash == refreshTokenHash, cancellationToken);

        // Nếu không tìm thấy Token trong Database
        if (storedToken is null)
        {
            // Báo lỗi Refresh Token không hợp lệ
            return ServiceResult<TokenResponse>.Fail(401, "Refresh token is invalid.", "INVALID_REFRESH_TOKEN");
        }

        // Nếu Token này đã bị thu hồi (Revoked) do đăng xuất hoặc cấp lại mật khẩu
        if (storedToken.IsRevoked)
        {
            // Trả về lỗi báo Token đã bị thu hồi
            return ServiceResult<TokenResponse>.Fail(401, "Refresh token has been revoked.", "REFRESH_TOKEN_REVOKED");
        }

        // Kiểm tra nếu thời gian hiện tại đã qua thời điểm hết hạn của Token
        if (storedToken.ExpiresAt <= _clock.UtcNow)
        {
            // Báo lỗi Token đã hết hạn
            return ServiceResult<TokenResponse>.Fail(401, "Refresh token has expired.", "REFRESH_TOKEN_EXPIRED");
        }

        // Kiểm tra xem trạng thái tài khoản của chủ Token có đang Active không
        if (storedToken.User.Status != AuthConstants.UserStatus.Active)
        {
            // Từ chối cấp Token mới nếu tài khoản đang bị khóa
            return ServiceResult<TokenResponse>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        // Đánh dấu Refresh Token cũ đã bị thu hồi (để tránh sử dụng lại - Reuse Detection)
        storedToken.IsRevoked = true;
        // Ghi nhận thời điểm thu hồi Token
        storedToken.RevokedAt = _clock.UtcNow;

        // Sinh ra một Refresh Token hoàn toàn mới
        var newRefreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        // Lưu trữ Token mới vào cấu trúc Database
        var newRefreshToken = CreateRefreshToken(storedToken.UserId, newRefreshTokenValue);
        _dbContext.RefreshTokens.Add(newRefreshToken);

        // Sinh ra JWT Access Token mới
        var accessToken = _jwtTokenService.GenerateAccessToken(
            storedToken.User.UserId,
            storedToken.User.Email,
            storedToken.User.Role.RoleName);

        // Lưu thông tin thu hồi Token cũ và thêm Token mới vào CSDL
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả cấp mới Token thành công
        return ServiceResult<TokenResponse>.Ok(
            new TokenResponse
            {
                AccessToken = accessToken.AccessToken,
                RefreshToken = newRefreshTokenValue,
                ExpiresAt = accessToken.ExpiresAt
            },
            "Token refreshed successfully.");
    }

    public async Task<ServiceResult<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        // Băm chuỗi Refresh Token để tra cứu trong Database
        var refreshTokenHash = HashRefreshToken(request.RefreshToken);
        // Tìm Token tương ứng trong Database
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(item => item.TokenHash == refreshTokenHash, cancellationToken);

        // Nếu tìm thấy Token và Token này chưa bị thu hồi
        if (storedToken is not null && !storedToken.IsRevoked)
        {
            // Tiến hành thu hồi (vô hiệu hóa) Token
            storedToken.IsRevoked = true;
            // Ghi lại thời điểm thu hồi là lúc Đăng xuất
            storedToken.RevokedAt = _clock.UtcNow;
            // Cập nhật sự thay đổi này vào Database
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Luôn trả về thành công cho dù tìm thấy hay không tìm thấy Token (bảo mật)
        return ServiceResult<object>.Ok(new { loggedOut = true }, "Logout successful.");
    }

    public async Task<ServiceResult<object>> ResendVerificationOtpAsync(ResendVerificationOtpRequest request, CancellationToken cancellationToken)
    {
        // Chuẩn hóa email
        var normalizedEmail = NormalizeEmail(request.Email);
        // Truy vấn người dùng bằng email
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        // Nếu người dùng không tồn tại
        if (user is null)
        {
            // Trả về lỗi 404
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Kiểm tra xem email có đang trong trạng thái đã xác minh hay không
        if (user.EmailVerified && user.Status == AuthConstants.UserStatus.Active)
        {
            // Trả về lỗi do không cần xác minh nữa
            return ServiceResult<object>.Fail(409, "Email is already verified.", "EMAIL_ALREADY_VERIFIED");
        }

        // Thực thi hành động tạo và gửi OTP xác minh mới
        return await SendVerificationOtpAsync(
            user,
            normalizedEmail,
            "Verification OTP resent successfully.",
            cancellationToken);
    }

    private async Task<ServiceResult<object>> SendVerificationOtpAsync(
        User user,
        string normalizedEmail,
        string successMessage,
        CancellationToken cancellationToken)
    {
        // Lấy thời điểm hiện tại
        var now = _clock.UtcNow;
        // Truy vấn mã OTP cuối cùng mà hệ thống đã tạo ra với mục đích xác minh Email
        var latestToken = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose)
            // Sắp xếp giảm dần theo thời gian tạo để lấy cái mới nhất
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        // Kiểm tra xem việc gửi thêm một OTP nữa có vi phạm chính sách gửi (Rate Limit) hay không
        var sendPolicyFailure = GetOtpSendPolicyFailure(latestToken, now);

        // Nếu có vi phạm chính sách (gửi quá nhiều, gửi liên tục)
        if (sendPolicyFailure is not null)
        {
            // Trả về lỗi vi phạm Rate Limit
            return sendPolicyFailure;
        }

        // Lấy tất cả các mã OTP cũ chưa được sử dụng của người dùng này
        var oldTokens = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose && !item.IsUsed)
            .ToListAsync(cancellationToken);

        // Duyệt qua và đánh dấu tất cả các mã OTP cũ là đã sử dụng (để vô hiệu hóa chúng)
        foreach (var oldToken in oldTokens)
        {
            oldToken.IsUsed = true;
        }

        // Sinh mã OTP 6 số mới
        var otp = _otpGenerator.GenerateSixDigitOtp();
        // Khởi tạo bản ghi lưu OTP mới
        var verificationToken = new EmailVerificationToken
        {
            // Sinh ID token
            TokenId = NewId(DomainConstants.EntityIdPrefix.EmailVerificationToken),
            // Map với ID người dùng
            UserId = user.UserId,
            // Băm OTP trước khi lưu
            Token = _passwordHasher.HashSecret(otp),
            // Ghi nhận thời gian tạo
            CreatedAt = now,
            // Cộng thêm thời gian hết hạn
            ExpiredAt = now.AddSeconds(_authSettings.OtpExpirySeconds),
            // Khởi tạo trạng thái chưa sử dụng
            IsUsed = false,
            // Đặt mục đích là xác minh Email
            Purpose = EmailVerificationPurpose,
            // Tăng biến đếm số lần gửi OTP (để quản lý Rate Limit)
            AttemptCount = GetNextOtpAttemptCount(latestToken, now)
        };

        // Thêm bản ghi mới vào CSDL
        _dbContext.EmailVerificationTokens.Add(verificationToken);
        // Lưu thay đổi
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Tiến hành gửi email chứa mã OTP đến hòm thư người dùng
        var emailResult = await TrySendVerificationOtpEmailAsync(
            normalizedEmail,
            otp,
            verificationToken.ExpiredAt,
            cancellationToken);
        // Nếu quá trình gửi email thất bại
        if (!emailResult.Success)
        {
            // Hủy bỏ mã OTP vừa tạo (đánh dấu đã sử dụng) để ngăn chặn phát sinh lỗi
            verificationToken.IsUsed = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Trả về lỗi của quá trình gửi email
            return emailResult;
        }

        // Trả về thành công kèm theo thông tin phiên OTP
        return ServiceResult<object>.Ok(
            new
            {
                email = normalizedEmail,
                expiresAt = verificationToken.ExpiredAt,
                attemptsRemaining = _authSettings.OtpMaxSendAttempts - verificationToken.AttemptCount
            },
            successMessage);
    }

    public async Task<ServiceResult<object>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        // Chuẩn hóa địa chỉ email do người dùng cung cấp
        var normalizedEmail = NormalizeEmail(request.Email);
        // Tìm người dùng trong DB dựa trên email
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        // Nếu email không tồn tại trong hệ thống
        if (user is null)
        {
            // Báo lỗi 404 Not Found
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Nếu tài khoản này tồn tại nhưng chưa từng xác minh email
        if (!user.EmailVerified)
        {
            // Từ chối cấp lại mật khẩu cho tài khoản chưa xác minh
            return ServiceResult<object>.Fail(403, "Email has not been verified.", "EMAIL_NOT_VERIFIED");
        }

        // Kiểm tra xem tài khoản có đang bị vô hiệu hóa hay không
        if (user.Status != AuthConstants.UserStatus.Active)
        {
            // Trả về lỗi tài khoản bị khóa
            return ServiceResult<object>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        // Lấy thời gian hiện tại
        var now = _clock.UtcNow;
        // Tìm bản ghi OTP cấp lại mật khẩu gần nhất
        var latestToken = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == PasswordResetPurpose)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        // Đánh giá xem có vi phạm Rate Limit khi gửi OTP mới hay không
        var sendPolicyFailure = GetOtpSendPolicyFailure(latestToken, now);

        // Nếu vi phạm Rate Limit
        if (sendPolicyFailure is not null)
        {
            // Trả lỗi Rate Limit
            return sendPolicyFailure;
        }

        // Truy vấn tất cả các OTP quên mật khẩu cũ chưa sử dụng
        var oldTokens = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == PasswordResetPurpose && !item.IsUsed)
            .ToListAsync(cancellationToken);

        // Đánh dấu hủy toàn bộ OTP quên mật khẩu cũ
        foreach (var oldToken in oldTokens)
        {
            oldToken.IsUsed = true;
        }

        // Sinh OTP 6 số mới cho mục đích Đặt lại mật khẩu
        var otp = _otpGenerator.GenerateSixDigitOtp();
        // Khởi tạo bản ghi Token OTP mới
        var resetToken = new EmailVerificationToken
        {
            // ID bản ghi
            TokenId = NewId(DomainConstants.EntityIdPrefix.EmailVerificationToken),
            // Map với User
            UserId = user.UserId,
            // Mã Hash của OTP
            Token = _passwordHasher.HashSecret(otp),
            // Thời gian bắt đầu có hiệu lực
            CreatedAt = now,
            // Hạn sử dụng của OTP
            ExpiredAt = now.AddSeconds(_authSettings.OtpExpirySeconds),
            // Trạng thái chưa dùng
            IsUsed = false,
            // Mục đích Cấp lại mật khẩu
            Purpose = PasswordResetPurpose,
            // Tăng Attempt Count để kiểm soát tần suất gửi
            AttemptCount = GetNextOtpAttemptCount(latestToken, now)
        };

        // Đưa vào tracking của EF Core
        _dbContext.EmailVerificationTokens.Add(resetToken);
        // Lưu thay đổi
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi email chứa OTP cấp lại mật khẩu cho người dùng
        var emailResult = await TrySendPasswordResetOtpEmailAsync(
            normalizedEmail,
            otp,
            resetToken.ExpiredAt,
            cancellationToken);
        // Nếu lỗi xảy ra khi gửi email
        if (!emailResult.Success)
        {
            // Hủy mã OTP vừa tạo
            resetToken.IsUsed = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Trả về lỗi
            return emailResult;
        }

        // Trả kết quả thành công cho Controller
        return ServiceResult<object>.Ok(
            new
            {
                email = normalizedEmail,
                expiresAt = resetToken.ExpiredAt,
                attemptsRemaining = _authSettings.OtpMaxSendAttempts - resetToken.AttemptCount
            },
            "Password reset OTP sent successfully.");
    }

    public async Task<ServiceResult<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        // Xác thực độ mạnh của mật khẩu mới theo cấu hình hệ thống
        var passwordValidationError = PasswordValidator.Validate(request.NewPassword, _authSettings);
        // Nếu mật khẩu mới không đáp ứng tiêu chuẩn
        if (passwordValidationError is not null)
        {
            // Trả về lỗi Weak Password
            return ServiceResult<object>.Fail(400, passwordValidationError, "WEAK_PASSWORD");
        }

        // Chuẩn hóa địa chỉ email
        var normalizedEmail = NormalizeEmail(request.Email);
        // Tìm thông tin người dùng trong DB
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        // Nếu không có người dùng này
        if (user is null)
        {
            // Trả lỗi 404
            return ServiceResult<object>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        // Kiểm tra xem tài khoản có bị vô hiệu hóa không
        if (user.Status != AuthConstants.UserStatus.Active)
        {
            // Trả lỗi nếu tài khoản không Active
            return ServiceResult<object>.Fail(403, "Account is not active.", "ACCOUNT_NOT_ACTIVE");
        }

        // Tìm kiếm mã OTP Cấp lại mật khẩu hợp lệ mới nhất của người dùng này
        var token = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == PasswordResetPurpose && !item.IsUsed)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Nếu không có OTP nào khớp hoặc đã bị sử dụng hết
        if (token is null)
        {
            // Trả lỗi 400
            return ServiceResult<object>.Fail(400, "Password reset OTP was not found.", "OTP_NOT_FOUND");
        }

        // Kiểm tra nếu thời gian của mã OTP đã quá hạn
        if (token.ExpiredAt <= _clock.UtcNow)
        {
            // Trả lỗi quá hạn
            return ServiceResult<object>.Fail(400, "Password reset OTP has expired.", "OTP_EXPIRED");
        }

        // Đối chiếu mã OTP người dùng nhập vào với mã băm trong cơ sở dữ liệu
        if (!_passwordHasher.VerifySecret(request.Otp, token.Token))
        {
            // Nếu không khớp, trả về lỗi mã OTP không đúng
            return ServiceResult<object>.Fail(400, "Password reset OTP is invalid.", "INVALID_OTP");
        }

        // Đánh dấu OTP này đã được dùng thành công
        token.IsUsed = true;
        // Lưu thời điểm xác thực thành công
        token.VerifiedAt = _clock.UtcNow;
        // Mã hóa mật khẩu mới và thay thế mật khẩu cũ trong DB
        user.PasswordHash = _passwordHasher.HashSecret(request.NewPassword);
        // Cập nhật lại thời gian thay đổi dữ liệu của User
        user.UpdatedAt = _clock.UtcNow;

        // Nếu tài khoản này chưa từng được xác minh email, thực hiện xác minh tự động luôn (vì đã nhận OTP qua email đó)
        if (!user.EmailVerified)
        {
            // Đánh dấu đã xác minh
            user.EmailVerified = true;
            // Nếu trạng thái đang là Pending Verification, chuyển thành Active
            if (user.Status == AuthConstants.UserStatus.PendingVerification)
            {
                user.Status = AuthConstants.UserStatus.Active;
            }
        }

        // Tìm tất cả các phiên đăng nhập (Refresh Tokens) hiện tại của người dùng
        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(item => item.UserId == user.UserId && !item.IsRevoked)
            .ToListAsync(cancellationToken);

        // Vô hiệu hóa (đăng xuất) toàn bộ các phiên đăng nhập trên tất cả thiết bị để đảm bảo an toàn sau khi đổi mật khẩu
        foreach (var refreshToken in activeRefreshTokens)
        {
            // Đánh dấu thu hồi token
            refreshToken.IsRevoked = true;
            // Ghi nhận thời điểm thu hồi
            refreshToken.RevokedAt = _clock.UtcNow;
        }

        // Lưu toàn bộ sửa đổi trên (User, Tokens) vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả thông báo thành công
        return ServiceResult<object>.Ok(new { email = normalizedEmail }, "Password reset successfully.");
    }

    private async Task<ServiceResult<object>> ResendPendingCustomerVerificationAsync(
        User user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Kiểm tra xem hệ thống có thiết lập tự động xác nhận email hay không (thường là chế độ DEV)
        if (_autoConfirmEmail)
        {
            // Xác nhận email ngay lập tức
            user.EmailVerified = true;
            // Kích hoạt tài khoản
            user.Status = AuthConstants.UserStatus.Active;
            // Lưu trạng thái vào Database
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Phản hồi về việc tự động xác nhận đã thành công
            return ServiceResult<object>.Ok(
                new { email = user.Email },
                "Registration email auto-confirmed for development.");
        }

        // Truy xuất Token OTP xác nhận đăng ký mới nhất từ CSDL
        var latestToken = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Tính toán khoảng thời gian xem người dùng có được quyền yêu cầu gửi lại OTP không
        var resendAvailableAt = latestToken?.CreatedAt.AddSeconds(_authSettings.OtpResendCooldownSeconds);
        // Nếu mã OTP vẫn đang còn hiệu lực và chưa tới thời gian cho phép yêu cầu mới
        if (latestToken is not null &&
            !latestToken.IsUsed &&
            latestToken.ExpiredAt > now &&
            resendAvailableAt > now)
        {
            // Thực hiện ghi nhận trạng thái vào DB (nếu có update gì)
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Báo cho người dùng tiếp tục sử dụng mã OTP đã gửi
            return ServiceResult<object>.Ok(
                new
                {
                    email = user.Email,
                    expiresAt = latestToken.ExpiredAt,
                    attemptsRemaining = _authSettings.OtpMaxSendAttempts - GetEffectiveOtpAttemptCount(latestToken)
                },
                "Account is pending email verification. Use the OTP already sent to your email.");
        }

        // Kiểm tra logic ngăn chặn SPAM (Rate Limit - Gửi quá nhiều)
        var sendPolicyFailure = GetOtpSendPolicyFailure(latestToken, now);
        // Nếu bị chặn lại do Spam
        if (sendPolicyFailure is not null)
        {
            // Trả lỗi Rate Limit
            return sendPolicyFailure;
        }

        // Truy xuất các OTP cũ vẫn đang khả dụng nhưng chưa được dùng
        var oldTokens = await _dbContext.EmailVerificationTokens
            .Where(item => item.UserId == user.UserId && item.Purpose == EmailVerificationPurpose && !item.IsUsed)
            .ToListAsync(cancellationToken);

        // Vô hiệu hóa (hủy) toàn bộ các OTP cũ đó
        foreach (var oldToken in oldTokens)
        {
            oldToken.IsUsed = true;
        }

        // Tạo mã OTP 6 số ngẫu nhiên mới
        var otp = _otpGenerator.GenerateSixDigitOtp();
        // Cấu hình bản ghi OTP mới
        var verificationToken = new EmailVerificationToken
        {
            // Tạo ID mới cho token
            TokenId = NewId(DomainConstants.EntityIdPrefix.EmailVerificationToken),
            // Map token với User
            UserId = user.UserId,
            // Băm nội dung OTP để bảo mật
            Token = _passwordHasher.HashSecret(otp),
            // Ghi nhận mốc thời gian tạo
            CreatedAt = now,
            // Cộng thêm thời hạn sử dụng
            ExpiredAt = now.AddSeconds(_authSettings.OtpExpirySeconds),
            // Trạng thái khả dụng ban đầu
            IsUsed = false,
            // Mục đích sử dụng của OTP
            Purpose = EmailVerificationPurpose,
            // Xác định số thứ tự của lần gửi (Attempt count)
            AttemptCount = GetNextOtpAttemptCount(latestToken, now)
        };

        // Ghi lại thay đổi (Token mới) vào EF Core
        _dbContext.EmailVerificationTokens.Add(verificationToken);
        // Thực thi Update Database
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Bắn tín hiệu đẩy email thông qua Hangfire hoặc Queue
        var emailResult = await TrySendVerificationOtpEmailAsync(
            user.Email,
            otp,
            verificationToken.ExpiredAt,
            cancellationToken);
        // Nếu việc gửi gặp sự cố (ví dụ SMTP từ chối)
        if (!emailResult.Success)
        {
            // Đánh dấu mã OTP vừa tạo thành Đã sử dụng (Hủy mã OTP đó đi)
            verificationToken.IsUsed = true;
            // Lưu lại hành động hủy
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Trả về Exception
            return emailResult;
        }

        // Báo kết quả gửi lại OTP thành công
        return ServiceResult<object>.Ok(
            new
            {
                email = user.Email,
                expiresAt = verificationToken.ExpiredAt,
                attemptsRemaining = _authSettings.OtpMaxSendAttempts - verificationToken.AttemptCount
            },
            "Account is pending email verification. A new verification OTP has been sent.");
    }

    private ServiceResult<object>? GetOtpSendPolicyFailure(
        EmailVerificationToken? latestToken,
        DateTime now)
    {
        // Nếu đây là lần gửi OTP đầu tiên
        if (latestToken is null)
        {
            // Cho phép gửi bình thường
            return null;
        }

        // Kiểm tra xem số lần đã gửi có chạm mốc tối đa cho phép hay chưa
        if (GetEffectiveOtpAttemptCount(latestToken) >= _authSettings.OtpMaxSendAttempts)
        {
            // Tính toán thời gian khóa tính từ lúc gửi Token gần nhất
            var unlockAt = latestToken.CreatedAt.AddHours(_authSettings.OtpSendLockHours);
            // Nếu vẫn đang trong thời gian bị phạt (Lock)
            if (unlockAt > now)
            {
                // Trả về lỗi chặn việc tạo OTP
                return CreateOtpRateLimitFailure(
                    "OTP send limit reached. Please try again later.",
                    "OTP_SEND_LIMIT_REACHED",
                    unlockAt,
                    now);
            }
        }

        // Tính toán mốc thời gian được phép gửi một OTP khác kể từ lần gửi gần nhất (CoolDown)
        var resendAvailableAt = latestToken.CreatedAt.AddSeconds(_authSettings.OtpResendCooldownSeconds);
        // Nếu chưa hết thời gian nghỉ (Cooldown)
        if (resendAvailableAt > now)
        {
            // Trả về lỗi báo chờ đợi thêm
            return CreateOtpRateLimitFailure(
                "Please wait before requesting another OTP.",
                "OTP_RESEND_COOLDOWN",
                resendAvailableAt,
                now);
        }

        // Không vi phạm Rule nào cả
        return null;
    }

    private ServiceResult<object> CreateOtpRateLimitFailure(
        string message,
        string errorCode,
        DateTime retryAt,
        DateTime now)
    {
        // Tính toán số giây còn lại phải đợi
        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((retryAt - now).TotalSeconds));

        // Trả kết quả lỗi Http Code 429 Too Many Requests
        return ServiceResult<object>.Fail(
            429,
            message,
            errorCode,
            new Dictionary<string, string[]>
            {
                // Truyền tham số thời gian chờ lên client
                ["retryAfterSeconds"] = [retryAfterSeconds.ToString()]
            });
    }

    private int GetNextOtpAttemptCount(EmailVerificationToken? latestToken, DateTime now)
    {
        // Nếu chưa từng có Token nào, trả về lần gửi số 1
        if (latestToken is null)
        {
            return 1;
        }

        // Nếu đã từng vượt quá giới hạn gửi, NHƯNG thời gian phạt Lock đã trôi qua
        if (GetEffectiveOtpAttemptCount(latestToken) >= _authSettings.OtpMaxSendAttempts &&
            latestToken.CreatedAt.AddHours(_authSettings.OtpSendLockHours) <= now)
        {
            // Reset chu kỳ đếm về 1
            return 1;
        }

        // Tăng biến đếm lên 1 cho lần yêu cầu tiếp theo
        return GetEffectiveOtpAttemptCount(latestToken) + 1;
    }

    private int GetEffectiveOtpAttemptCount(EmailVerificationToken token)
    {
        // Lấy giá trị biến đếm của Token, thấp nhất là 1
        return Math.Max(1, token.AttemptCount);
    }

    private async Task<Role?> GetPublicRegistrationRoleAsync(CancellationToken cancellationToken)
    {
        return await (
                from policy in _dbContext.RoleProvisioningPolicies
                join role in _dbContext.Roles on policy.RoleId equals role.RoleId
                where policy.IsActive
                      && policy.IsPublicRegistrationAllowed
                      && policy.ProfileKind == DomainConstants.AccountProfileKind.Customer
                      && !policy.RequiresCinema
                select role)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private RefreshToken CreateRefreshToken(string userId, string token)
    {
        // Lấy thời gian thực UTC
        var now = _clock.UtcNow;
        // Sinh ra đối tượng Token cho Entity Framework
        return new RefreshToken
        {
            // Khởi tạo mã định danh ID
            RefreshTokenId = NewId(DomainConstants.EntityIdPrefix.RefreshToken),
            // Map Token thuộc về User nào
            UserId = userId,
            // Sinh mã băm của Token
            TokenHash = HashRefreshToken(token),
            // Ghi nhận mốc sinh Token
            IssuedAt = now,
            // Xác định thời điểm Token hết hiệu lực (Tối thiểu 1 ngày)
            ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenDays),
            // Xác nhận trạng thái Token lúc vừa tạo là chưa bị thu hồi
            IsRevoked = false
        };
    }

    private static string HashRefreshToken(string refreshToken)
    {
        // Biến đổi chuỗi Refresh Token ra dạng Byte mảng, băm bằng thuật toán SHA256
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        // Chuyển đổi dãy Byte Hash thành dạng Base64 String để lưu trong DB
        return Convert.ToBase64String(bytes);
    }

    private async Task CleanupFailedRegistrationAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            // Truy vấn toàn bộ Tokens của người dùng này
            var tokens = await _dbContext.EmailVerificationTokens
                .Where(item => item.UserId == userId)
                .ToListAsync(cancellationToken);
            // Tìm Hồ sơ khách hàng của người dùng này
            var customerProfile = await _dbContext.CustomerProfiles
                .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            // Lấy ra tài khoản đang đăng ký thất bại
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

            // Gỡ bỏ Tokens
            _dbContext.EmailVerificationTokens.RemoveRange(tokens);
            // Gỡ bỏ Hồ sơ cá nhân (nếu có)
            if (customerProfile is not null)
            {
                _dbContext.CustomerProfiles.Remove(customerProfile);
            }

            // Gỡ bỏ Tài khoản
            if (user is not null)
            {
                _dbContext.Users.Remove(user);
            }

            // Thực thi hành động xóa Database để giữ DB gọn gàng
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Bỏ qua lỗi xóa để không làm sập ứng dụng (Keep the public error focused on SMTP failure. A cleanup failure should be diagnosed from server logs.)
        }
    }

    private async Task<ServiceResult<object>> TrySendVerificationOtpEmailAsync(
        string email,
        string otp,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        // Chuẩn bị nội dung Email gửi đi sử dụng chuỗi Multi-line String
        var body = string.Format(
            _emailTemplates.VerificationBody,
            otp,
            expiresAt);

        try
        {
            // Xếp hàng Job gửi Email chạy trên Background Worker (Hangfire)
            _backgroundJobClient.Enqueue<IEmailSender>(emailSender => 
                emailSender.SendEmailAsync(
                    email,
                    _emailTemplates.VerificationSubject,
                    body,
                    CancellationToken.None));
        }
        catch (Exception)
        {
            // Xử lý bắt lỗi khi Hangfire hoặc tiến trình kết nối SMTP gặp sự cố
            return ServiceResult<object>.Fail(
                500,
                "Unable to send verification email. Please check Gmail SMTP configuration.",
                "EMAIL_SEND_FAILED");
        }

        // Báo kết quả đẩy tác vụ gửi Email thành công
        return ServiceResult<object>.Ok(new { email, expiresAt }, "Verification email sent.");
    }

    private async Task<ServiceResult<object>> TrySendPasswordResetOtpEmailAsync(
        string email,
        string otp,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        // Khởi tạo đoạn thông điệp Email
        var body = string.Format(
            _emailTemplates.PasswordResetBody,
            otp,
            expiresAt);

        try
        {
            // Sử dụng Hangfire Queue để đưa Task gửi Email ra Background (Tránh block Main Thread)
            _backgroundJobClient.Enqueue<IEmailSender>(emailSender => 
                emailSender.SendEmailAsync(
                    email,
                    _emailTemplates.PasswordResetSubject,
                    body,
                    CancellationToken.None));
        }
        catch (Exception)
        {
            // Bắt Exeption khi gửi Email, trả về Internal Server Error 500
            return ServiceResult<object>.Fail(
                500,
                "Unable to send password reset email. Please check Gmail SMTP configuration.",
                "EMAIL_SEND_FAILED");
        }

        // Báo tín hiệu thành công
        return ServiceResult<object>.Ok(new { email, expiresAt }, "Password reset email sent.");
    }

    private static string NormalizeEmail(string email)
    {
        // Dọn dẹp khoảng trắng 2 đầu và chuyển email sang dạng chữ in thường để đồng bộ CSDL
        return email.Trim().ToLowerInvariant();
    }

    private static string NewId(string prefix)
    {
        // Sinh ra ID chuỗi bao gồm Tiền tố (Prefix) nối với một đoạn mã Guid ở định dạng N (No hyphens)
        return $"{prefix}_{Guid.NewGuid():N}";
    }
}
