# SCRUM-195 - Manager Revenue and Ticket Overview

## Task

Implement backend API for Manager to view the overview of revenue and ticket metrics
inside the manager's assigned cinema.

Required metrics:

- Total revenue after deducting refunded tickets.
- Total tickets sold.
- Room occupancy rate.

## Documentation Sources

- `docs/requirements/srs-group-2.docx`
  - Project scope requires Manager/Admin support for revenue report tracking.
  - Manager is defined as the cinema or cinema-cluster operator who follows branch
    revenue, handles operational issues, and manages cinema operations.
  - Dashboard is defined as statistics, revenue reports, and operation monitoring.
  - UC003 links showtime cancellation and refund with Revenue Report, so revenue
    metrics must account for refund data.
- `docs/requirements/business-rules.docx`
  - Booking is confirmed only after successful payment.
  - If a showtime is cancelled by Manager/Admin, paid bookings move to refund
    processing.
  - Successful refund moves booking to refunded.
  - Manager can view only dashboard and data that belong to the assigned cinema.
- `docs/api/api-contract-backend.docx`
  - Defines Manager endpoint `GET /api/manager/dashboard` for manager dashboard data.
  - Defines `GET /api/manager/reports/revenue` separately for revenue chart/report,
    so this task uses the dashboard route for compact overview metrics.
- `docs/architecture/backend-system-design-clean-architecture.docx`
  - API controllers must stay thin and delegate use cases to Application interfaces.
  - Report/dashboard queries should use projection for performance.
  - Report/Audit module includes revenue dashboard.
- `docs/architecture/conceptual-erd-explanation.docx`
  - Booking is the center linking Customer, Showtime, Seat, Payment, Ticket, Refund,
    and Notification.
  - Payment and Refund are separated for gateway reconciliation and business reports.
  - Staff/Manager must be linked to Cinema through StaffProfile for branch scoping.
- `docs/database/cinema-booking-schema.sql`
  - Revenue source tables: `PAYMENT`, `REFUND`, `BOOKING`, `SHOWTIME`, `ROOM`,
    `CINEMA`.
  - Ticket/occupancy source tables: `TICKET`, `BOOKING_SEAT`, `SHOWTIME_SEAT`,
    `SHOWTIME`, `ROOM`, `CINEMA`.
- `docs/reports/SCRUM-190-manager-cinema-scope.md`
  - Existing `ICinemaScopeAuthorizationService` resolves Manager scope from
    `STAFF_PROFILE.cinemaId`.
- `docs/reports/SCRUM-192-cancel-showtime-refund.md`
  - Existing refund flow creates `REFUND` records from successful payments and keeps
    refund lifecycle status.

## Applied Design

- Endpoint: `GET /api/manager/dashboard`.
- Authorization: `CanViewBranchDashboard`.
- Scope:
  - Manager must have active `STAFF_PROFILE` and only sees that cinema.
  - Admin follows the existing scope service behavior and can call without cinema
    scope.
- Query range:
  - Optional `from` and `to` query parameters.
  - Range filters by `SHOWTIME.startTime` so revenue, ticket count, and occupancy all
    describe the same cinema operation window.
  - Date values are normalized to UTC.

## Formula

- `GrossRevenue` = sum of `PAYMENT.amount` where `paymentStatus = SUCCESS`.
- `RefundedAmount` = sum of `REFUND.refundAmount` where `refundStatus = SUCCESS`.
- `TotalRevenue` = `GrossRevenue - RefundedAmount`.
- `TicketsSold` = count of issued tickets whose:
  - booking is `PAID` or `COMPLETED`;
  - ticket is not `CANCELLED` or `REFUNDED`;
  - showtime is not `CANCELLED`.
- `TotalShowtimeSeats` = count of `SHOWTIME_SEAT` for non-cancelled showtimes in
  range.
- `RoomOccupancyRate` = `TicketsSold / TotalShowtimeSeats * 100`, rounded to 2
  decimals.

## Why This Approach

- Payment is the source of truth for collected money because the business rules say a
  booking is confirmed only after payment success.
- Refund is subtracted from revenue because the task explicitly requires revenue after
  deducting refunded tickets and the ERD separates refund lifecycle from payment.
- Tickets are counted from `TICKET`, not only `BOOKING_SEAT`, because tickets are
  generated after payment success and cancelled/refunded tickets must not be counted as
  sold active tickets.
- Occupancy is based on `SHOWTIME_SEAT` because this table represents actual seats for
  each showtime, which matches the existing seat-map and booking model.
- The controller delegates to `IManagerDashboardService`, keeping the API layer thin
  and matching the current Clean Architecture split.

## Code Changes

- Contracts
  - `CinemaSystem.Contracts/Dashboard/ManagerDashboardOverviewQueryRequest.cs`
  - `CinemaSystem.Contracts/Dashboard/ManagerDashboardOverviewResponse.cs`
- Application
  - `CinemaSystem.Application/Interfaces/IManagerDashboardService.cs`
- Infrastructure
  - `CinemaSystem.Infrastructure/Reports/ManagerDashboardService.cs`
  - `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`
- API
  - `CinemaSystem/Controllers/ManagerDashboardController.cs`
- Tests
  - `CinemaSystem.Tests/ManagerDashboardApiIntegrationTests.cs`

## Test Results

Commands run from repository root:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.Tests\CinemaSystem.Tests.csproj --no-build --filter ManagerDashboardApiIntegrationTests
dotnet test CinemaSystem.sln --no-build
```

Results:

- Build: passed.
- New dashboard tests: 3 passed, 0 failed, 0 skipped.
- Full test suite: 252 passed, 0 failed, 0 skipped.

Build warnings:

- Existing nullable warnings remain in old test files:
  - `CinemaSystem.Tests/CinemaApiIntegrationTests.cs`
  - `CinemaSystem.Tests/MovieApiIntegrationTests.cs`
  - `CinemaSystem.Tests/RoomShowtimeServiceTests.cs`
- No new build error was introduced by this task.
