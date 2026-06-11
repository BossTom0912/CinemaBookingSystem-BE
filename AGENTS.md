# CinemaSystem Codex Guidance

## Project
CinemaSystem is an ASP.NET Core Web API backend for an Online Movie Ticket Booking and Management System. The current solution follows a Clean Architecture-style structure with API, Application, Domain, Infrastructure, and Contracts projects.

## Documentation
Authoritative project documents are indexed in `docs/README.md`:
- `docs/requirements/srs-group-2.docx`
- `docs/requirements/business-rules.docx`
- `docs/architecture/backend-system-design-clean-architecture.docx`
- `docs/architecture/conceptual-erd-explanation.docx`
- `docs/database/cinema-booking-schema.sql`

Always inspect these documents before making feature-level changes.

## Architecture Rules
- Follow the existing Clean Architecture project split.
- `CinemaSystem` is the API layer: controllers, middleware, Swagger, auth configuration, and DI entry point only.
- Controllers must stay thin and delegate use cases to Application abstractions.
- `CinemaSystem.Application` contains use case interfaces, application constants, and service contracts.
- `CinemaSystem.Infrastructure` handles EF Core, SQL Server, JWT creation, SMTP email, password/OTP hashing, and external service implementations.
- `CinemaSystem.Contracts` contains request/response DTOs and shared API response models.
- `CinemaSystem.Domain` is reserved for domain entities, enums, business rules, constants, and exceptions when needed.
- Do not rename projects, namespaces, or scaffolded database-first models unnecessarily.
- Prefer the existing database schema and scaffolded EF Core models.

## Current Implemented Scope
The repository currently contains authentication and authorization, cinema,
room, seat, showtime, seat-locking, and SePay payment functionality. Inspect the
existing service and controller patterns before extending these modules.

Do not implement Movie, Booking, Ticket, Refund, Voucher, Food and Beverage, or
other incomplete modules unless explicitly requested.

## Database/Auth Tables
Documents and current EF models define these Sprint 1 auth tables:
- `ROLE`
- `USER`
- `CUSTOMER_PROFILE`
- `STAFF_PROFILE`
- `EMAIL_VERIFICATION_TOKEN`
- `REFRESH_TOKEN`

Public registration may create only Customer accounts. Admin, Manager, and Staff accounts must not be created through public register.

## Security Rules
- Do not hard-code secrets.
- Do not log passwords, OTPs, JWTs, refresh tokens, SMTP passwords, or connection strings.
- Hash passwords securely.
- Generate OTP and refresh tokens with cryptographically secure randomness.
- Never return OTP values in API responses.
- Use UTC for timestamps.
- Store refresh tokens in the database.
- Revoke refresh tokens on logout.
- Reject login for unverified users.
- Reject login for inactive or banned users.
- Keep access tokens short-lived.

## Coding Conventions
- Use async EF Core calls.
- Use DTOs for API contracts.
- Use consistent API responses:
  `{ success, message, data, errorCode, errors }`.
- Validate request DTOs with data annotations and controller model validation.
- Use clear HTTP status codes: 200, 201, 400, 401, 403, 404, 409.
- Keep scaffolded EF models minimally edited, preferably untouched.

## Authorization Policies
Minimum roles:
- `Customer`
- `Staff`
- `Manager`
- `Admin`

Policies:
- `CanBookTicket`: Customer
- `CanScanTicket`: Staff, Manager, Admin
- `CanManageShowtime`: Manager, Admin
- `CanManageSystem`: Admin

## Config Notes
Configure JWT via appsettings, user secrets, or environment variables:
- `JwtSettings:Issuer`
- `JwtSettings:Audience`
- `JwtSettings:Secret`
- `JwtSettings:AccessTokenMinutes`
- `JwtSettings:RefreshTokenDays`

Configure Gmail SMTP via appsettings, user secrets, or environment variables:
- `EmailSettings:SmtpHost`
- `EmailSettings:SmtpPort`
- `EmailSettings:SenderEmail`
- `EmailSettings:SenderName`
- `EmailSettings:Password`

Never commit real SMTP passwords or production JWT secrets.

## Testing Commands
Run from repository root:
- `dotnet restore CinemaSystem.sln`
- `dotnet build CinemaSystem.sln`
- `dotnet test CinemaSystem.sln`

If no test project exists and the project structure supports it, create focused tests for the changed feature.
