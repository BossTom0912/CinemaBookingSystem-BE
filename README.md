# CinemaSystem Backend

ASP.NET Core 8 Web API for an online movie ticket booking and cinema
management system.

## Features

- Customer registration and email OTP verification
- JWT login, refresh-token rotation, logout, and role authorization
- Forgot-password and password-reset flows
- Staff account provisioning by administrators
- Cinema, room, seat, and showtime management
- Temporary seat locking with in-memory or Redis storage
- SePay payment creation and webhook confirmation

## Architecture

| Project | Responsibility |
| --- | --- |
| `CinemaSystem` | API controllers, middleware, authentication, Swagger, and DI entry point |
| `CinemaSystem.Application` | Use-case interfaces, application constants, and service contracts |
| `CinemaSystem.Contracts` | Request and response DTOs |
| `CinemaSystem.Domain` | Domain entities and business concepts |
| `CinemaSystem.Infrastructure` | EF Core, SQL Server, JWT, SMTP, Redis, and payment implementations |
| `CinemaSystem.Tests` | Automated tests |

Project documents are indexed in [`docs/README.md`](docs/README.md).

## Requirements

- .NET 8 SDK
- SQL Server
- Redis is optional; the application can use the in-memory seat-lock store
- Gmail SMTP credentials are optional when mock email mode is enabled

## Local Setup

1. Clone the repository.
2. Create the local settings file:

   ```powershell
   Copy-Item CinemaSystem/appsettings.Development.example.json CinemaSystem/appsettings.Development.json
   ```

3. Add local credentials to `CinemaSystem/appsettings.Development.json`.
4. Create the database using
   [`docs/database/cinema-booking-schema.sql`](docs/database/cinema-booking-schema.sql).
5. Restore, build, and test:

   ```powershell
   dotnet restore CinemaSystem.sln
   dotnet build CinemaSystem.sln
   dotnet test CinemaSystem.sln
   ```

6. Run the API:

   ```powershell
   dotnet run --project CinemaSystem
   ```

Swagger is available at `/swagger` in the development environment.

## Configuration

Keep credentials in `appsettings.Development.json`, .NET User Secrets, or
environment variables. Never commit database passwords, JWT secrets, SMTP app
passwords, webhook secrets, tokens, or production account details.

Important configuration sections:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings`
- `EmailSettings`
- `SepaySettings`
- `Redis:ConnectionString`

Optional seed accounts are created only when their password variables are set:

- `ADMIN_PASSWORD` and optional `ADMIN_EMAIL`
- `DEV_STAFF_PASSWORD`
- `DEV_CUSTOMER_PASSWORD`

## Git Workflow

Create feature branches from `main` and merge through a pull request or merge
request:

```powershell
git switch main
git pull
git switch -c feature/short-description
```

Before opening a PR/MR, run:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.sln
```
