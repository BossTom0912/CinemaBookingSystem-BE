# CinemaBookingSystem-BE

ASP.NET Core Web API backend for the Online Movie Ticket Booking and Management System.

## Project Structure

- `CinemaSystem`: API layer, controllers, middleware, Swagger, auth configuration, DI entry point
- `CinemaSystem.Application`: use case contracts, service interfaces, application constants
- `CinemaSystem.Contracts`: request/response DTOs and shared API response models
- `CinemaSystem.Domain`: domain entities, enums, business rules, constants, exceptions
- `CinemaSystem.Infrastructure`: EF Core, SQL Server, JWT, SMTP email, password/OTP hashing, external services
- `CinemaSystem.Tests`: focused backend tests
- `KhoBauG2`: project documents and database scripts

## After Cloning

Clone the repository:

```powershell
git clone https://github.com/BossTom0912/CinemaBookingSystem-BE.git
cd CinemaBookingSystem-BE
```

Create a local development settings file:

```powershell
Copy-Item CinemaSystem/appsettings.Development.example.json CinemaSystem/appsettings.Development.json
```

Open this file:

```text
CinemaSystem/appsettings.Development.json
```

Update the local SQL Server connection string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=CinemaBookingDB;User Id=sa;Password=12345;TrustServerCertificate=True;"
}
```

Update Gmail SMTP settings if you need to test OTP email:

```json
"EmailSettings": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "SenderEmail": "your-email@gmail.com",
  "SenderName": "Cinema Booking System",
  "Password": "your-gmail-app-password"
}
```

Do not commit real passwords, Gmail app passwords, JWT secrets, or production connection strings.

## Database Setup

1. Open SQL Server.
2. Run this script to create the database:

```text
KhoBauG2/DB_CinemaBookingDB.txt
```

3. Confirm your local connection string points to `CinemaBookingDB`.

## Build And Test

Run from the repository root:

```powershell
dotnet restore
dotnet build CinemaSystem.slnx
dotnet test CinemaSystem.slnx
```

## Run API

```powershell
dotnet run --project CinemaSystem
```

Swagger will be available at:

```text
https://localhost:<port>/swagger
```

Health check:

```text
GET /api/health
```

Database connection test:

```text
GET /api/db-test/movies-count
```

## Team Git Workflow

Use `main` for stable code only. Do not push directly to `main` unless the team agrees.

Before starting work:

```powershell
git checkout main
git pull origin main
```

Create a feature branch:

```powershell
git checkout -b feature/your-feature-name
```

After making changes:

```powershell
dotnet build CinemaSystem.slnx
dotnet test CinemaSystem.slnx
git status
git add .
git commit -m "Describe your change"
git push -u origin feature/your-feature-name
```

Then create a Pull Request on GitHub to merge into `main`.

## Files That Must Not Be Committed

These should stay local only:

- `CinemaSystem/appsettings.Development.json`
- `.env`
- `.env.*`
- `bin/`
- `obj/`
- `.vs/`
- real SMTP passwords
- real JWT secrets
- real production connection strings

Use `appsettings.Development.example.json` as the template for local setup.
