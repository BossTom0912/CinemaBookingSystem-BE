# CinemaBookingSystem-BE

ASP.NET Core Web API backend for the Online Movie Ticket Booking and Management System.

## Architecture

This backend follows a Clean Architecture-style structure in ASP.NET Core.

## Projects

- CinemaSystem: API layer
- CinemaSystem.Application: use cases, service contracts, application constants
- CinemaSystem.Domain: entities, enums, business rules, exceptions
- CinemaSystem.Infrastructure: EF Core, SQL Server, JWT, SMTP email, security services
- CinemaSystem.Contracts: request/response models and shared API response

## Prerequisites

- .NET SDK compatible with the project target framework
- SQL Server
- Gmail app password for SMTP OTP email, stored locally only

## Configuration

Do not commit real secrets.

Local developers can copy:

```powershell
Copy-Item CinemaSystem/appsettings.Development.example.json CinemaSystem/appsettings.Development.json
```

Then update `CinemaSystem/appsettings.Development.json` locally:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings:Secret`
- `EmailSettings:SenderEmail`
- `EmailSettings:Password`

Production and shared environments should use user secrets, environment variables, or deployment secrets.

## How to run

```powershell
dotnet restore
dotnet build CinemaSystem.slnx
dotnet run --project CinemaSystem
```

Swagger:

```text
https://localhost:<port>/swagger
```

## Health Check

GET `/api/health`

## Database Test

GET `/api/db-test/movies-count`

If this endpoint returns a movie count, the API is connected to SQL Server successfully.

## Database Setup

This project uses SQL Server and EF Core Database First.

1. Run `DB_CinemaBookingDB.txt` in SQL Server to create `CinemaBookingDB`.
2. Update the local connection string in `CinemaSystem/appsettings.Development.json`.
3. Run the API project.
4. Open Swagger and test `/api/db-test/movies-count`.

## Git Workflow

- GitHub repository name: `CinemaBookingSystem-BE`
- Keep the repository private for the team.
- Use `main` for stable code.
- Create feature branches such as `feature/auth-register`.
- Merge through pull requests.
- Protect `main` with pull request review before merging.
