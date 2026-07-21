using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

/// <summary>
/// Integration test HTTP cho api/auth — Register, Verify, Login, Logout Customer.
/// Nguồn: CinemaSystem/Controllers/AuthController.cs, AuthService.cs
/// </summary>
public sealed class AuthApiIntegrationTests
{
  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

  [Fact]
  public async Task Register_Login_Logout_CustomerE2E_Success()
  {
    // Luồng E2E Customer: Register → Verify OTP → Login → Logout.
    await using var factory = new CinemaWebApplicationFactory();
    await factory.SeedPublicRegistrationPolicyAsync();
    using var client = factory.CreateClient();

    // Bước 1: Đăng ký tài khoản Customer mới.
    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
      Email = "customer.api@test.com",
      Password = "Password1",
      FullName = "API Customer",
      PhoneNumber = "0901234567"
    });
    Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

    // Bước 2: Xác thực email bằng OTP cố định (123456) từ FakeOtpGenerator.
    var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest
    {
      Email = "customer.api@test.com",
      Otp = factory.FixedOtp
    });
    Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

    // Bước 3: Login nhận JWT + refresh token.
    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
    {
      Email = "customer.api@test.com",
      Password = "Password1"
    });
    Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    var loginBody = await DeserializeAsync<ApiResponse<AuthResponse>>(loginResponse);
    Assert.True(loginBody!.Success);
    Assert.False(string.IsNullOrWhiteSpace(loginBody.Data!.AccessToken));
    Assert.False(string.IsNullOrWhiteSpace(loginBody.Data.RefreshToken));

    // Bước 4: Logout — revoke refresh token.
    var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest
    {
      RefreshToken = loginBody.Data.RefreshToken
    });
    Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

    // Bước 5: Refresh token đã revoke — không dùng lại được.
    var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
    {
      RefreshToken = loginBody.Data.RefreshToken
    });
    Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
  }

  [Fact]
  public async Task Register_WeakPassword_ReturnsBadRequest()
  {
    // Luồng: password đủ 8 ký tự nhưng không đủ mạnh (service rule) → HTTP 400 WEAK_PASSWORD.
    await using var factory = new CinemaWebApplicationFactory();
    await factory.SeedPublicRegistrationPolicyAsync();
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
      Email = "weak@test.com",
      Password = "password",
      FullName = "Weak User"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<object>>(response);
    Assert.Equal("WEAK_PASSWORD", body!.ErrorCode);
  }

  [Fact]
  public async Task Register_PasswordTooShort_ReturnsWeakPassword()
  {
    // Độ dài mật khẩu được kiểm tra bằng AuthSettings tại runtime.
    await using var factory = new CinemaWebApplicationFactory();
    await factory.SeedPublicRegistrationPolicyAsync();
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
      Email = "short@test.com",
      Password = "123",
      FullName = "Short Pass"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<object>>(response);
    Assert.Equal("WEAK_PASSWORD", body!.ErrorCode);
  }

  [Fact]
  public async Task Login_UnverifiedAccount_ReturnsForbidden()
  {
    // Luồng: register nhưng chưa verify → login → 403 ACCOUNT_NOT_VERIFIED.
    await using var factory = new CinemaWebApplicationFactory();
    await factory.SeedPublicRegistrationPolicyAsync();
    using var client = factory.CreateClient();

    await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
      Email = "unverified@test.com",
      Password = "Password1",
      FullName = "Unverified"
    });

    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
    {
      Email = "unverified@test.com",
      Password = "Password1"
    });

    Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
    var body = await DeserializeAsync<ApiResponse<AuthResponse>>(loginResponse);
    Assert.Equal("EMAIL_NOT_VERIFIED", body!.ErrorCode);
  }

  [Fact]
  public async Task Register_SendsEmailThroughPipeline()
  {
    // Luồng: register qua HTTP → email OTP được gửi qua FakeEmailCapture.
    await using var factory = new CinemaWebApplicationFactory();
    await factory.SeedPublicRegistrationPolicyAsync();
    using var client = factory.CreateClient();

    await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
      Email = "email.pipe@test.com",
      Password = "Password1",
      FullName = "Email Pipe"
    });

    Assert.Contains(factory.EmailCapture.Emails, e => e.ToEmail == "email.pipe@test.com");
  }

  private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
  {
    var json = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(json, JsonOptions);
  }
}
