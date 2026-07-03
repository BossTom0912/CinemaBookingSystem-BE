// Import thư viện để làm việc với JWT Token
using System.IdentityModel.Tokens.Jwt;
// Import thư viện quản lý Claims cho bảo mật
using System.Security.Claims;
// Import thư viện cung cấp các thuật toán mã hóa (như RandomNumberGenerator)
using System.Security.Cryptography;
// Import thư viện xử lý chuỗi và mã hóa ký tự (UTF8)
using System.Text;
// Import các hằng số liên quan đến xác thực từ Application layer
using CinemaSystem.Application.Auth;
// Import các giao diện và class chung từ Application layer
using CinemaSystem.Application.Common;
// Import các interface của Application layer để dùng cho DI
using CinemaSystem.Application.Interfaces;
// Import cấu hình của Infrastructure
using CinemaSystem.Infrastructure.Configuration;
// Import thư viện lấy cấu hình từ appsettings
using Microsoft.Extensions.Options;
// Import thư viện cấu hình token của Microsoft
using Microsoft.IdentityModel.Tokens;

// Định nghĩa namespace cho các thành phần liên quan đến Identity (Xác thực) trong Infrastructure
namespace CinemaSystem.Infrastructure.Identity;

// Lớp JwtTokenService chịu trách nhiệm sinh token, kế thừa từ IJwtTokenService
public sealed class JwtTokenService : IJwtTokenService
{
    // Biến private chỉ đọc để lưu trữ cài đặt JWT
    private readonly JwtSettings _settings;
    // Biến private chỉ đọc để lấy thời gian hiện tại
    private readonly IClock _clock;

    // Constructor nhận cấu hình JWT và IClock qua Dependency Injection (DI)
    public JwtTokenService(IOptions<JwtSettings> options, IClock clock)
    {
        // Lấy giá trị cấu hình JWT từ tham số options
        _settings = options.Value;
        // Gán Clock từ DI vào biến private
        _clock = clock;
    }

    // Phương thức tạo Access Token cho người dùng
    public GeneratedToken GenerateAccessToken(string userId, string email, string role)
    {
        // Kiểm tra xem khóa bí mật của JWT có bị trống hoặc ngắn hơn 32 ký tự không
        if (!SecretSettingsValidator.IsConfigured(_settings.Secret, 32))
        {
            // Ném lỗi nếu khóa bí mật không hợp lệ (không đủ độ an toàn)
            throw new InvalidOperationException("JwtSettings:Secret must be configured and at least 32 characters long.");
        }

        // Tính toán thời điểm hết hạn của Token bằng thời gian hiện tại cộng với số phút trong cấu hình (tối thiểu 1 phút)
        var expiresAt = _clock.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
        // Tạo khóa ký (SymmetricSecurityKey) dựa trên mảng byte của khóa bí mật (Secret)
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        // Tạo đối tượng chứa thông tin chứng chỉ ký thuật toán HMAC SHA256
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        // Chuẩn hóa tên quyền (role) bằng cách gọi hàm trong AuthConstants
        var normalizedRole = AuthConstants.Roles.Normalize(role);
        // Tạo một mảng các Claim (những thông tin định danh) để đưa vào Token
        var claims = new[]
        {
            // Claim định danh đối tượng (Subject) chứa ID người dùng
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            // Claim chứa địa chỉ email (chuẩn JWT)
            new Claim(JwtRegisteredClaimNames.Email, email),
            // Claim định danh của NameIdentifier chứa ID người dùng
            new Claim(ClaimTypes.NameIdentifier, userId),
            // Claim chứa địa chỉ email (kiểu ClaimTypes)
            new Claim(ClaimTypes.Email, email),
            // Claim chứa quyền (role) đã chuẩn hóa
            new Claim(ClaimTypes.Role, normalizedRole),
            // Claim tùy chỉnh chứa userId
            new Claim(AuthConstants.Claims.UserId, userId),
            // Claim tùy chỉnh chứa quyền (role)
            new Claim("role", normalizedRole)
        };

        // Khởi tạo đối tượng JwtSecurityToken chứa đầy đủ thông tin của Token
        var token = new JwtSecurityToken(
            // Gán tổ chức phát hành Token (Issuer)
            issuer: _settings.Issuer,
            // Gán đối tượng sử dụng Token (Audience)
            audience: _settings.Audience,
            // Đưa mảng các Claim vào Token
            claims: claims,
            // Thời điểm bắt đầu có hiệu lực của Token (chính là thời điểm hiện tại)
            notBefore: _clock.UtcNow,
            // Thời điểm hết hạn của Token
            expires: expiresAt,
            // Gắn thông tin chữ ký vào Token
            signingCredentials: credentials);

        // Trả về một đối tượng GeneratedToken chứa thông tin Access Token đã tạo
        return new GeneratedToken
        {
            // Ghi Token thành chuỗi chữ ký (string)
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            // Gán thông tin thời điểm hết hạn của Token
            ExpiresAt = expiresAt
        };
    }

    // Phương thức tạo Refresh Token (Token làm mới)
    public string GenerateRefreshToken()
    {
        // Tạo 64 byte ngẫu nhiên an toàn (cryptographically secure) và chuyển thành chuỗi Base64 để làm Refresh Token
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

}
