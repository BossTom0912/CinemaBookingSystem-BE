# Fix + Forgot Password Implementation

Date: 2026-05-25

## Scope

This update keeps the current CinemaSystem Auth implementation and does not replace it with the BA-provided CinemaBooking AuthService.

Completed items:
- Downgraded the current project from .NET 10 target packages to .NET 8 target packages.
- Kept the existing register, email OTP verification, login, refresh token, logout, and resend verification OTP behavior.
- Added a GlobalExceptionMiddleware that returns the current API response shape:
  `{ success, message, data, errorCode, errors }`.
- Added forgot password and reset password using OTP sent by email.
- Added automated tests for the new forgot/reset password behavior.

Deferred item:
- ISeatLockService/Redis fallback was not added in this update because it belongs with the later booking/seat-lock feature. This avoids adding unused booking infrastructure during Sprint 1 auth work.

## Database Impact

No database schema changes were made.

Forgot password reuses the existing `EMAIL_VERIFICATION_TOKEN` table:
- The OTP is generated with the existing crypto OTP generator.
- The OTP is stored as a hash with the existing password/OTP hasher.
- Previous unused tokens for the user are marked as used before creating a new password reset OTP.
- Resetting the password marks the OTP as used and sets `verifiedAt`.

The existing `REFRESH_TOKEN` table is also reused:
- When a password reset succeeds, active refresh tokens for that user are revoked.
- This keeps older sessions from continuing after a password change.

## .NET 8 Downgrade

Updated target frameworks:
- `CinemaSystem`
- `CinemaSystem.Application`
- `CinemaSystem.Contracts`
- `CinemaSystem.Domain`
- `CinemaSystem.Infrastructure`
- `CinemaSystem.Tests`

Updated package versions:
- `Microsoft.AspNetCore.Authentication.JwtBearer`: `8.0.11`
- `Microsoft.EntityFrameworkCore.Design`: `8.0.11`
- `Microsoft.EntityFrameworkCore.SqlServer`: `8.0.11`
- `Microsoft.EntityFrameworkCore.Tools`: `8.0.11`
- `Microsoft.AspNetCore.Mvc.Testing`: `8.0.11`
- `Microsoft.EntityFrameworkCore.InMemory`: `8.0.11`
- `Swashbuckle.AspNetCore`: `6.6.2`

Removed the .NET 10 OpenAPI package usage from the API project and changed Swagger security configuration to the .NET 8-compatible `Microsoft.OpenApi.Models` style.

## Global Exception Middleware

Added:
- `CinemaSystem/Middlewares/GlobalExceptionMiddleware.cs`

Registered in:
- `CinemaSystem/Program.cs`

Behavior:
- Catches unhandled exceptions.
- Logs the exception without logging secrets.
- Returns JSON using `ApiResponse<object>.Fail(...)`.
- Maps common exception types to `401`, `400`, `404`, and `500`.

## Forgot Password

Added request DTO:
- `CinemaSystem.Contracts/Auth/ForgotPasswordRequest.cs`

Added endpoint:
- `POST /api/auth/forgot-password`

Behavior:
- Normalizes email.
- If the email does not exist, returns success without sending email to avoid leaking account existence.
- Rejects unverified users.
- Rejects inactive or banned users.
- Marks previous unused OTP tokens as used.
- Creates a new 6-digit OTP.
- Stores only the hashed OTP.
- Sends the OTP by email using the existing email sender.

## Reset Password

Added request DTO:
- `CinemaSystem.Contracts/Auth/ResetPasswordRequest.cs`

Added endpoint:
- `POST /api/auth/reset-password`

Behavior:
- Validates new password strength with the same password rules used by register.
- Finds the latest unused OTP for the user.
- Rejects missing, expired, or invalid OTP.
- Hashes and stores the new password.
- Marks the OTP as used.
- Revokes active refresh tokens for the user.

## Existing Auth Flow Preserved

The following existing flows are still preserved:
- `POST /api/auth/register`
- `POST /api/auth/verify-email`
- `POST /api/auth/login`
- `POST /api/auth/refresh-token`
- `POST /api/auth/logout`
- `POST /api/auth/resend-verification-otp`

The existing `AuthService` remains the active implementation.

## Tests Added

Added basic forgot/reset password tests:
- Forgot password sends OTP email for verified active user.
- Forgot password for unknown email returns success and does not send email.
- Reset password with valid OTP updates password and revokes old refresh tokens.
- Login with old password fails after reset.
- Login with new password succeeds after reset.
- Reset password with wrong OTP fails.
- Reset password with expired OTP fails.
- Reset password with weak new password fails.

## Verification

Commands run:

```powershell
dotnet restore
dotnet build
dotnet test
```

Results:
- `dotnet restore`: Passed.
- `dotnet build`: Passed with 0 warnings and 0 errors.
- `dotnet test`: Passed.
- Total tests: 29.
- Passed: 29.
- Failed: 0.
- Skipped: 0.

Note:
- `dotnet restore` and `dotnet test` needed permission to read the local user NuGet config at `C:\Users\Tom\AppData\Roaming\NuGet\NuGet.Config`.
