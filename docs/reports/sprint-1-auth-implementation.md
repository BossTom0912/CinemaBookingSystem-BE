# CinemaSystem Sprint 1 Auth Implementation Report

## Start
- Task started in the local workspace on 2026-05-22.

## Documents Read
- `docs/requirements/srs-group-2.docx`
- `docs/architecture/backend-system-design-clean-architecture.docx`
- `docs/requirements/business-rules.docx`
- `docs/architecture/conceptual-erd-explanation.docx`
- `docs/database/cinema-booking-schema.sql`

## Existing Project Structure Discovered
- Solution file at the time: `CinemaSystem.slnx` (later replaced by
  `CinemaSystem.sln` for .NET 8 CI compatibility).
- API project: `CinemaSystem`
- Application project: `CinemaSystem.Application`
- Contracts project: `CinemaSystem.Contracts`
- Domain project: `CinemaSystem.Domain`
- Infrastructure project: `CinemaSystem.Infrastructure`
- Current data access: EF Core database-first `CinemaDbContext` in Infrastructure.
- Existing API controllers before this task: `HealthController`, `DbTestController`.
- Existing auth code before this task: none beyond scaffolded EF models for auth tables.

## Auth Requirements Found In Documents
- Sprint 1 foundation includes Clean Architecture, SQL Server/EF Core, Swagger, register/login/logout, email verification, refresh token, and role authorization.
- Public register can only create Customer accounts.
- User starts as `PENDING_VERIFICATION` with `emailVerified = false`.
- Email verification uses `EMAIL_VERIFICATION_TOKEN`.
- Login is rejected until email is verified and status is `ACTIVE`.
- JWT and refresh token are required.
- Logout revokes refresh token.
- Roles required: Customer, Staff, Manager, Admin.
- Suggested policies: Customer booking, Staff/Manager/Admin scan, Manager/Admin showtime management, Admin system management.

## Existing Database/Auth Tables Discovered
- `ROLE`
- `USER`
- `CUSTOMER_PROFILE`
- `STAFF_PROFILE`
- `EMAIL_VERIFICATION_TOKEN`
- `REFRESH_TOKEN`

## Implementation Summary
- Added root `AGENTS.md` for future Codex guidance.
- Implemented Sprint 1 Auth only.
- Added consistent API response wrapper.
- Added request/response DTOs for register, verify email, login, refresh token, logout, and resend OTP.
- Added Application interfaces and constants.
- Implemented EF Core-backed Auth service in Infrastructure.
- Implemented secure PBKDF2 hashing for passwords and OTP storage.
- Implemented cryptographically secure 6-digit OTP generation.
- Implemented Gmail SMTP email sender via configuration.
- Implemented JWT access token generation.
- Implemented refresh token generation, persistence, rotation, and revocation.
- Configured JWT Bearer authentication.
- Configured role policies.
- Added Auth controller endpoints.
- Added safe policy test endpoints.
- Added automated xUnit test project with 22 tests.
- Added manual Swagger/Postman test cases in `docs/testing/auth-manual-test-cases.md`.

## Endpoints Implemented
- `POST /api/auth/register`
- `POST /api/auth/verify-email`
- `POST /api/auth/login`
- `POST /api/auth/refresh-token`
- `POST /api/auth/logout`
- `POST /api/auth/resend-verification-otp`
- `GET /api/auth-test/customer`
- `GET /api/auth-test/admin`

## Files Created
- `AGENTS.md`
- `docs/testing/auth-manual-test-cases.md`
- `CinemaSystem.Contracts/Common/ApiResponse.cs`
- `CinemaSystem.Contracts/Auth/RegisterRequest.cs`
- `CinemaSystem.Contracts/Auth/VerifyEmailRequest.cs`
- `CinemaSystem.Contracts/Auth/LoginRequest.cs`
- `CinemaSystem.Contracts/Auth/RefreshTokenRequest.cs`
- `CinemaSystem.Contracts/Auth/LogoutRequest.cs`
- `CinemaSystem.Contracts/Auth/ResendVerificationOtpRequest.cs`
- `CinemaSystem.Contracts/Auth/AuthResponse.cs`
- `CinemaSystem.Contracts/Auth/TokenResponse.cs`
- `CinemaSystem.Contracts/Auth/UserProfileResponse.cs`
- `CinemaSystem.Application/Common/AuthConstants.cs`
- `CinemaSystem.Application/Common/ServiceResult.cs`
- `CinemaSystem.Application/Auth/GeneratedToken.cs`
- `CinemaSystem.Application/Interfaces/IAuthService.cs`
- `CinemaSystem.Application/Interfaces/IEmailSender.cs`
- `CinemaSystem.Application/Interfaces/IJwtTokenService.cs`
- `CinemaSystem.Application/Interfaces/IPasswordHasher.cs`
- `CinemaSystem.Application/Interfaces/IOtpGenerator.cs`
- `CinemaSystem.Application/Interfaces/IClock.cs`
- `CinemaSystem.Infrastructure/Configuration/JwtSettings.cs`
- `CinemaSystem.Infrastructure/Configuration/EmailSettings.cs`
- `CinemaSystem.Infrastructure/Time/SystemClock.cs`
- `CinemaSystem.Infrastructure/Security/Pbkdf2PasswordHasher.cs`
- `CinemaSystem.Infrastructure/Security/CryptoOtpGenerator.cs`
- `CinemaSystem.Infrastructure/Identity/JwtTokenService.cs`
- `CinemaSystem.Infrastructure/Email/SmtpEmailSender.cs`
- `CinemaSystem.Infrastructure/Auth/AuthService.cs`
- `CinemaSystem/Controllers/AuthController.cs`
- `CinemaSystem/Controllers/AuthPolicyTestController.cs`
- `CinemaSystem.Tests/CinemaSystem.Tests.csproj`
- `CinemaSystem.Tests/AuthServiceTests.cs`

## Files Modified
- `CinemaSystem.slnx`
- `CinemaSystem/CinemaSystem.csproj`
- `CinemaSystem/Program.cs`
- `CinemaSystem/appsettings.json`
- `CinemaSystem/appsettings.Development.json`
- `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`

## Packages Added
- `Microsoft.AspNetCore.Authentication.JwtBearer` to `CinemaSystem`.
- `Microsoft.EntityFrameworkCore.InMemory` to `CinemaSystem.Tests`.
- `Microsoft.AspNetCore.Mvc.Testing` to `CinemaSystem.Tests`.
- xUnit test packages were created by `dotnet new xunit`.

## Commands Run
- `dotnet add CinemaSystem\CinemaSystem.csproj package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.8`
- `dotnet build`
- `dotnet new xunit -n CinemaSystem.Tests`
- `dotnet add CinemaSystem.Tests\CinemaSystem.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 10.0.8`
- `dotnet add CinemaSystem.Tests\CinemaSystem.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 10.0.8`
- `dotnet test`
- `dotnet restore`
- `dotnet build`
- `dotnet test`

## Build Result
- Final `dotnet build`: Passed.
- Warnings: 0.
- Errors: 0.

## Test Result
- Final `dotnet test`: Passed.
- Total tests: 23.
- Passed: 23.
- Failed: 0.
- Skipped: 0.

## Automated Test Coverage
- Register success creates pending Customer user.
- Duplicate email returns conflict.
- Public register cannot inject Admin/Manager/Staff role.
- Register sends OTP through email abstraction.
- Password is not stored in plain text.
- Verify OTP success activates user.
- Wrong OTP fails.
- Expired OTP fails.
- Used OTP fails.
- Already verified user returns conflict.
- Login success returns JWT and refresh token.
- Wrong password fails.
- Unverified account cannot login.
- Inactive/banned account cannot login.
- JWT contains userId, email, and role claims.
- Valid refresh token returns new access token and new refresh token.
- Revoked refresh token fails.
- Expired refresh token fails.
- Logout revokes refresh token.
- Logout with unknown token does not crash.
- Customer token can access Customer policy endpoint.
- Customer token cannot access Admin policy endpoint.
- Register SMTP failure cleanup leaves no pending user/profile/OTP behind.

## Bugs Found And Fixed
- Infrastructure options binding initially failed because `Configure<T>(IConfigurationSection)` extension package was not referenced. Fixed by binding config manually.
- Swashbuckle/OpenAPI 10 uses `Microsoft.OpenApi` namespace and a newer security requirement signature. Fixed Swagger JWT security configuration.
- Authorization integration tests initially returned `401 Unauthorized` because test JWTs were generated with an expired fixed clock. Fixed by generating policy-test JWTs with current UTC time.
- SMTP configuration failures would have bubbled as raw exceptions. Fixed Auth service to return a clean `EMAIL_SEND_FAILED` response.
- Register initially saved `USER`, `CUSTOMER_PROFILE`, and `EMAIL_VERIFICATION_TOKEN` before SMTP sending, so failed email delivery still caused duplicate email on the next register attempt. Fixed by cleaning up the just-created user/profile/token when register email delivery fails.

## Manual Configuration Needed

### JWT
Use appsettings, user secrets, or environment variables:
- `JwtSettings:Issuer`
- `JwtSettings:Audience`
- `JwtSettings:Secret`
- `JwtSettings:AccessTokenMinutes`
- `JwtSettings:RefreshTokenDays`

The committed appsettings contains a development placeholder secret only. Replace it for real use.

### Gmail SMTP
Use appsettings, user secrets, or environment variables:
- `EmailSettings:SmtpHost=smtp.gmail.com`
- `EmailSettings:SmtpPort=587`
- `EmailSettings:SenderEmail=your-email@gmail.com`
- `EmailSettings:SenderName=Cinema Booking System`
- `EmailSettings:Password=<Gmail App Password>`

Do not commit a real Gmail password. Use a Gmail App Password, not the normal Gmail account password.

## Manual Swagger/Postman Test Cases
Detailed cases are saved in `docs/testing/auth-manual-test-cases.md`.

Required flows:
- Flow A: Register + verify + login.
- Flow B: Login before verify.
- Flow C: Duplicate register.
- Flow D: Refresh token.
- Flow E: Logout then refresh should fail.
- Flow F: Customer/Admin authorization policy behavior.

## Known Limitations
- Real Gmail sending was not tested because credentials are not present in the repository. Automated tests use a fake email sender.
- If SMTP sending fails during register, the API returns `EMAIL_SEND_FAILED` and cleans up the just-created pending user/profile/OTP. If old pending rows already exist from an earlier run, delete them manually or use a different email.
- Public register creates or reuses only the Customer role. Staff, Manager, and Admin account provisioning is intentionally not public and should be implemented through an admin/internal flow in a later sprint.
- No database migrations were added because the current project uses the existing database-first schema.

## Final Status
- Sprint 1 Auth implementation completed.
- Build passed.
- Tests passed.
- Detailed manual testing plan created.
