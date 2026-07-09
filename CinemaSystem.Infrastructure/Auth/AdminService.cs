// Sử dụng thư viện bảo mật Cryptography để tạo mật khẩu và dữ liệu ngẫu nhiên
using System.Security.Cryptography;
// Sử dụng các định nghĩa dùng chung trong tầng Application (như ServiceResult, IClock)
using CinemaSystem.Application.Common;
// Sử dụng các Data Transfer Object (DTO) dành cho quá trình xác thực (Auth)
using CinemaSystem.Contracts.Auth;
// Khai báo các interface dịch vụ được định nghĩa trong tầng Application
using CinemaSystem.Application.Interfaces;
// Bao gồm các Domain Entity ánh xạ với các bảng trong cơ sở dữ liệu
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Constants;
// Sử dụng lớp kết nối cơ sở dữ liệu (DbContext) từ tầng Infrastructure
using CinemaSystem.Infrastructure.Persistence;
// Sử dụng Entity Framework Core để tương tác với SQL Server
using Microsoft.EntityFrameworkCore;

// Sử dụng Hangfire để đưa các tác vụ (như gửi email) chạy ngầm (background job)
using Hangfire;

// Khai báo namespace xử lý các tác vụ xác thực của Admin trong tầng Infrastructure
namespace CinemaSystem.Infrastructure.Auth;

/// <summary>
/// Use case Admin tạo tài khoản Staff và phát hành lời mời đặt mật khẩu.
/// </summary>
/// <remarks>
/// Nhận lệnh từ <c>CinemaSystem/Controllers/AdminController.cs</c>; xử lý tiếp
/// tại USER, STAFF_PROFILE, EMAIL_VERIFICATION_TOKEN qua <see cref="CinemaDbContext"/>
/// và gửi email bằng <see cref="IEmailService"/>. Kết quả quay về AdminController.
/// </remarks>
public sealed class AdminService : IAdminService
{
    // Khai báo hằng số mục đích của token là đặt lại mật khẩu (để tái sử dụng cho tính năng gửi mã mời)
    private const string PasswordResetPurpose =
        DomainConstants.VerificationTokenPurpose.PasswordReset;

    // Khai báo DbContext để thao tác với cơ sở dữ liệu
    private readonly CinemaDbContext _dbContext;
    // Khai báo công cụ mã hóa mật khẩu và token
    private readonly IPasswordHasher _passwordHasher;
    // Khai báo công cụ sinh mã OTP (One-Time Password) ngẫu nhiên
    private readonly IOtpGenerator _otpGenerator;
    // Khai báo dịch vụ gửi email
    private readonly IEmailService _emailService;
    // Khai báo IClock để lấy thời gian hệ thống chuẩn xác
    private readonly IClock _clock;
    // Khai báo cấu hình liên quan đến hệ thống xác thực (lấy từ appsettings.json)
    private readonly CinemaSystem.Application.Settings.AuthSettings _authSettings;
    // Khai báo client Hangfire dùng để đưa công việc gửi email vào hàng đợi chạy ngầm
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;

    // Hàm khởi tạo tiêm các phụ thuộc (Dependency Injection) cần thiết cho AdminService
    public AdminService(
        // DbContext của hệ thống
        CinemaDbContext dbContext,
        // Dịch vụ mã hóa mật khẩu
        IPasswordHasher passwordHasher,
        // Dịch vụ sinh mã OTP
        IOtpGenerator otpGenerator,
        // Dịch vụ gửi email
        IEmailService emailService,
        // Dịch vụ cung cấp thời gian hệ thống (IClock)
        IClock clock,
        // Các thiết lập cấu hình của phần Auth
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.AuthSettings> authOptions,
        // Hangfire Client để tạo công việc nền
        Hangfire.IBackgroundJobClient backgroundJobClient)
    {
        // Gán DbContext
        _dbContext = dbContext;
        // Gán PasswordHasher
        _passwordHasher = passwordHasher;
        // Gán OtpGenerator
        _otpGenerator = otpGenerator;
        // Gán EmailService
        _emailService = emailService;
        // Gán Clock
        _clock = clock;
        // Trích xuất cấu hình AuthSettings từ IOptions
        _authSettings = authOptions.Value;
        // Gán BackgroundJobClient
        _backgroundJobClient = backgroundJobClient;
    }

    // Hàm xử lý việc khởi tạo tài khoản nhân viên (Staff) mới
    public async Task<ServiceResult<object>> CreateStaffAsync(
        // Thông tin đầu vào của nhân viên
        CreateStaffRequest request,
        // Token hủy tác vụ bất đồng bộ
        CancellationToken cancellationToken)
    {
        // Chuẩn hóa email đầu vào bằng cách xóa khoảng trắng ở 2 đầu và chuyển thành chữ thường
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        // Kiểm tra trong database xem email đã tồn tại ở bất kỳ user nào chưa
        var duplicateEmail = await _dbContext.Users
            // So sánh email chuẩn hóa
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        // Nếu email đã được sử dụng
        if (duplicateEmail)
        {
            // Trả về lỗi xung đột dữ liệu (HTTP 409 Conflict) với thông báo Email đã tồn tại
            return ServiceResult<object>.Fail(
                // Mã HTTP 409
                409,
                // Thông báo lỗi cho người dùng
                "Email already exists.",
                // Mã lỗi hệ thống nội bộ
                "DUPLICATE_EMAIL");
        }

        // Truy vấn lấy rạp phim đầu tiên được sắp xếp theo CinemaId
        var cinema = await _dbContext.Cinemas
            // Sắp xếp tăng dần theo CinemaId
            .OrderBy(item => item.CinemaId)
            // Lấy dòng đầu tiên hoặc null nếu bảng trống
            .FirstOrDefaultAsync(cancellationToken);

        // Kiểm tra xem hệ thống có rạp phim nào không
        if (cinema is null)
        {
            // Trả về lỗi HTTP 400 Bad Request
            return ServiceResult<object>.Fail(
                // Mã HTTP 400
                400,
                // Yêu cầu phải có dữ liệu rạp phim trước khi tạo nhân viên
                "No cinema found. Seed at least one cinema before creating staff.",
                // Mã lỗi hệ thống
                "CINEMA_NOT_FOUND");
        }

        // Truy vấn tìm thông tin Role (Quyền) tương ứng với chức vụ Staff
        var staffRole = await _dbContext.Roles
            // Tìm Role dựa trên tên Role hoặc ID của Role chuẩn trong hệ thống
            .FirstOrDefaultAsync(
                // Điều kiện lọc RoleName hoặc RoleId là Staff
                role => role.RoleName == AuthConstants.Roles.Staff || role.RoleId == AuthConstants.RoleIds.Staff,
                // CancellationToken cho tác vụ bất đồng bộ
                cancellationToken);

        // Nếu quyền Staff không tồn tại trong DB
        if (staffRole is null)
        {
            // Trả về lỗi HTTP 400 Bad Request
            return ServiceResult<object>.Fail(
                // Mã HTTP 400
                400,
                // Thông báo không tìm thấy quyền Staff
                "Staff role was not found.",
                // Mã lỗi hệ thống
                "ROLE_NOT_FOUND");
        }

        // Lấy thời gian UTC hiện tại từ Clock service
        var now = _clock.UtcNow;
        // Tạo một mã định danh (ID) người dùng mới với tiền tố "USR"
        var userId = NewId(DomainConstants.EntityIdPrefix.User);
        // Sinh ra mã OTP gồm 6 chữ số dùng làm mật khẩu gửi vào email để xác thực
        var invitationOtp = _otpGenerator.GenerateSixDigitOtp();
        // Tạo một mật khẩu ngẫu nhiên tạm thời không thể giải mã và mã hóa Base64
        var placeholderPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Khởi tạo một đối tượng thực thể User cho nhân viên mới
        var staffUser = new User
        {
            // Gán mã người dùng vừa tạo
            UserId = userId,
            // Gán RoleId của quyền Staff
            RoleId = staffRole.RoleId,
            // Gán email chuẩn hóa
            Email = normalizedEmail,
            // Hash mật khẩu tạm thời rồi lưu
            PasswordHash = _passwordHasher.HashSecret(placeholderPassword),
            // Đặt tên đầy đủ, nếu không có truyền vào thì dùng mặc định là "New Staff"
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? "New Staff" : request.FullName.Trim(),
            // Trạng thái ban đầu của nhân viên là Active
            Status = AuthConstants.UserStatus.Active,
            // Ghi nhận email chưa được xác minh
            EmailVerified = false,
            // Thời gian tạo tài khoản
            CreatedAt = now
        };

        // Khởi tạo một đối tượng StaffProfile chứa thông tin hồ sơ nhân viên
        var staffProfile = new StaffProfile
        {
            // Tạo ID hồ sơ với tiền tố "STF"
            StaffProfileId = NewId(DomainConstants.EntityIdPrefix.StaffProfile),
            // Map với User vừa tạo
            UserId = userId,
            // Map với Cinema đầu tiên trong DB
            CinemaId = cinema.CinemaId,
            // Chức vụ là "Staff"
            Position = DomainConstants.StaffPosition.Staff,
            // Trạng thái công việc đang hoạt động ("ACTIVE")
            EmploymentStatus = DomainConstants.StaffEmploymentStatus.Active
        };

        // Tạo một token xác minh email (lưu dạng Password Reset để hệ thống dùng lại logic)
        var invitationToken = new EmailVerificationToken
        {
            // Tạo ID với tiền tố "EVT"
            TokenId = NewId(DomainConstants.EntityIdPrefix.EmailVerificationToken),
            // Liên kết với UserId
            UserId = userId,
            // Lưu trữ mã OTP đã được hash bảo mật
            Token = _passwordHasher.HashSecret(invitationOtp),
            // Ghi nhận thời gian tạo Token
            CreatedAt = now,
            // Tính toán thời gian hết hạn của Token dựa trên cài đặt hệ thống
            ExpiredAt = now.AddMinutes(_authSettings.InvitationTokenExpiryMinutes),
            // Trạng thái token là chưa được sử dụng
            IsUsed = false,
            // Gán mục đích token là PASSWORD_RESET
            Purpose = PasswordResetPurpose
        };

        // Đưa thực thể User vào tracking để chuẩn bị thêm vào Database
        _dbContext.Users.Add(staffUser);
        // Đưa thực thể StaffProfile vào tracking để chuẩn bị thêm vào Database
        _dbContext.StaffProfiles.Add(staffProfile);
        // Đưa thực thể EmailVerificationToken vào tracking để chuẩn bị thêm vào Database
        _dbContext.EmailVerificationTokens.Add(invitationToken);

        // Bắt đầu một khối lệnh Try Catch để xử lý lỗi lưu DB hoặc gửi mail
        try
        {
            // Lưu tất cả các đối tượng đã theo dõi vào cơ sở dữ liệu
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Sau khi lưu DB thành công, đẩy công việc gửi email xác nhận chứa mã OTP vào Hangfire chạy nền
            _backgroundJobClient.Enqueue<IEmailService>(email => 
                // Gọi phương thức SendInvitationAsync để gửi lời mời kèm mã OTP xác nhận
                email.SendInvitationAsync(normalizedEmail, invitationOtp, CancellationToken.None));
        }
        // Bắt lỗi nếu quá trình lưu DB có vấn đề (vd mất kết nối DB)
        catch (Exception)
        {
            // Loại bỏ User khỏi tracking (hoàn tác)
            _dbContext.Users.Remove(staffUser);
            // Loại bỏ StaffProfile khỏi tracking (hoàn tác)
            _dbContext.StaffProfiles.Remove(staffProfile);
            // Loại bỏ EmailVerificationToken khỏi tracking (hoàn tác)
            _dbContext.EmailVerificationTokens.Remove(invitationToken);
            // Xác nhận cập nhật xóa (nhưng thường là SaveChangesAsync ở trên đã lỗi nên không tạo rác dữ liệu)
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Trả về lỗi Internal Server Error (HTTP 500)
            return ServiceResult<object>.Fail(
                // Mã HTTP 500
                500,
                // Thông báo lỗi việc gửi email / xử lý gặp sự cố
                "Unable to send staff invitation email.",
                // Mã lỗi hệ thống
                "EMAIL_SEND_FAILED");
        }

        // Nếu tất cả thành công, trả về HTTP 201 Created cùng với email và thời gian hết hạn mã xác minh
        return ServiceResult<object>.Ok(
            // Dữ liệu phản hồi ẩn danh (anonymous object)
            new { email = normalizedEmail, expiresAt = invitationToken.ExpiredAt },
            // Thông báo thành công
            "Staff account created. Invitation email sent.",
            // Trả về mã HTTP 201
            201);
    }

    // Hàm tiện ích nội bộ (private static) để tạo ID ngẫu nhiên định dạng có tiền tố, ví dụ: "USR_a1b2c3d4..."
    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
