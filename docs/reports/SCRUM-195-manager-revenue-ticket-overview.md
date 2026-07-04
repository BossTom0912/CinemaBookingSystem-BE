# SCRUM-195 - Manager Revenue and Ticket Overview

## Task

Implement the Manager branch dashboard API for:

- revenue after successful refunds;
- sold ticket totals after fully refunded bookings;
- room occupancy;
- strict filtering to the cinema assigned to the authenticated Manager.

## Requirement Sources

### SRS

Source: `docs/requirements/srs-group-2.docx`

- Section 3.2.25 / UC-R01: `View Dashboard / Revenue Report`.
- The actor can filter by date, cinema, and movie.
- The system reads booking, payment, ticket, and refund data.
- The report calculates revenue, ticket count, refund, and occupancy.
- Manager views branch-level revenue; Admin can view the system-level dashboard.

### Business Rules

Source: `docs/requirements/business-rules.docx`

- BR-38: paid bookings from a cancelled showtime move to Refund Pending.
- BR-39: a successful refund moves the booking to Refunded.
- BR-40: an unsuccessful automatic refund requires manual handling.
- BR-79: Manager can view only the dashboard and data of the assigned cinema.
- BR-80: Admin has system-wide access.

### Backend Architecture

Source: `docs/architecture/backend-system-design-clean-architecture.docx`

- Report/dashboard is a dedicated application use case.
- Controllers must remain thin.
- Dashboard queries should use projections.
- Report data is based on `BOOKING`, `PAYMENT`, `REFUND`, and `TICKET`.

### ERD and Database

Sources:

- `docs/architecture/conceptual-erd-explanation.docx`
- `docs/database/cinema-booking-schema.sql`

Relevant relationships:

- `CINEMA -> ROOM -> SHOWTIME -> BOOKING`
- `BOOKING -> BOOKING_SEAT -> TICKET`
- `BOOKING -> PAYMENT`
- `PAYMENT -> REFUND`

One booking can have multiple payment attempts but only one successful payment. A
payment can have multiple refund records for retry or partial-refund support.

### API Contract

Source: `docs/api/api-contract-backend.docx`

- `GET /api/manager/dashboard`
- `GET /api/manager/reports/revenue`
- `GET /api/manager/bookings/statistics`

SCRUM-195 implements the combined overview endpoint:

```http
GET /api/manager/dashboard
```

## Implemented API

```http
GET /api/manager/dashboard
    ?from=2026-06-01T00:00:00Z
    &to=2026-07-01T00:00:00Z
    &movieId=MOV001
Authorization: Bearer <access-token>
```

Authorization policy:

```text
CanViewBranchDashboard
```

Allowed roles:

- Manager
- Admin

Scope behavior:

- Manager cinema scope is always loaded from the active
  `STAFF_PROFILE.cinemaId`.
- Manager cannot supply another `cinemaId` through the query string.
- Admin has no cinema restriction and receives system-wide totals.
- Manager without an active staff profile receives
  `STAFF_PROFILE_SCOPE_NOT_FOUND`.

## Date Semantics

The endpoint requires both `from` and `to`.

The filter is a half-open UTC interval applied to the showtime start time:

```text
from <= SHOWTIME.startTime < to
```

Filtering by showtime time keeps revenue, ticket, and occupancy metrics in the same
business period. Filtering ticket metrics by payment time would mix payments made in
advance with the wrong showtime reporting period.

Invalid range:

```text
from >= to -> 400 INVALID_DATE_RANGE
```

## KPI Definitions

### Revenue

```text
grossRevenue
    = SUM(PAYMENT.amount WHERE paymentStatus = SUCCESS)

refundedAmount
    = SUM(REFUND.refundAmount WHERE refundStatus = SUCCESS)

netRevenue
    = grossRevenue - refundedAmount
```

Only a successful refund reduces recognized revenue.

Refunds with `PENDING` and `MANUAL_REQUIRED` are returned separately as
`pendingRefundAmount` and `manualRefundAmount`; they do not reduce `netRevenue` until
successful.

`PAYMENT.amount` is the source of collected revenue instead of
`BOOKING.totalAmount`, because a successful payment is the financial evidence that
money was received.

### Tickets

```text
grossTicketsSold
    = COUNT(BOOKING_SEAT under a booking with a SUCCESS payment)

refundedTickets
    = COUNT(all BOOKING_SEAT of a booking only when
            total SUCCESS refund amount >= successful payment amount)

netTicketsSold
    = grossTicketsSold - refundedTickets
```

The current schema stores refunds at booking/payment level and does not link a partial
refund to individual seats. Therefore, a partial successful refund does not guess which
ticket was refunded. All tickets are removed from the net count only when the successful
refund total covers the full successful payment.

### Occupancy

```text
sellableSeatCapacity
    = COUNT(SHOWTIME_SEAT)
      excluding CANCELLED showtimes
      and excluding UNAVAILABLE showtime seats

occupiedSeats
    = eligible showtime seats linked to a booking with SUCCESS payment
      and not fully refunded

occupancyRate
    = occupiedSeats / sellableSeatCapacity * 100
```

The result is rounded to two decimal places. If capacity is zero, occupancy is `0`.

`SHOWTIME_SEAT` is used instead of `ROOM.capacity` because it represents the actual
seat inventory generated for each showtime.

## Response Fields

```json
{
  "cinemaId": "CIN001",
  "cinemaName": "Cinema A",
  "from": "2026-06-01T00:00:00Z",
  "to": "2026-07-01T00:00:00Z",
  "movieId": null,
  "grossRevenue": 400000,
  "refundedAmount": 100000,
  "pendingRefundAmount": 100000,
  "manualRefundAmount": 0,
  "netRevenue": 300000,
  "grossTicketsSold": 4,
  "refundedTickets": 1,
  "netTicketsSold": 3,
  "sellableSeatCapacity": 4,
  "occupiedSeats": 2,
  "occupancyRate": 50.00
}
```

## Code Changes

Contracts:

- `CinemaSystem.Contracts/Dashboard/ManagerDashboardQueryRequest.cs`
- `CinemaSystem.Contracts/Dashboard/ManagerDashboardResponse.cs`

Application:

- `CinemaSystem.Application/Interfaces/IManagerDashboardService.cs`
- Added the missing `COUNTER` value to `BookingConstants.BookingChannel`.

Infrastructure:

- `CinemaSystem.Infrastructure/Dashboard/ManagerDashboardService.cs`
- Registered `IManagerDashboardService` in Infrastructure dependency injection.
- Uses async EF Core, `AsNoTracking`, aggregate projections, and cancellation tokens.

API:

- `CinemaSystem/Controllers/ManagerDashboardController.cs`
- Controller handles authorization/scope and delegates calculations to the service.

Tests:

- `CinemaSystem.Tests/ManagerDashboardApiIntegrationTests.cs`

## Database Refund Verification

The authoritative schema already contains the `REFUND` table and all fields required by
SCRUM-195:

- `refundId`
- `bookingId`
- `paymentId`
- `paymentProviderId`
- `showtimeCancellationId`
- `refundAmount`
- `refundStatus`
- `refundReason`
- `providerRefundCode`
- `failureReason`
- `requestedAt`
- `refundedAt`

Run this query against the target SQL Server database before deployment:

```sql
SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS AS c
WHERE c.TABLE_SCHEMA = 'dbo'
  AND c.TABLE_NAME = 'REFUND'
ORDER BY c.ORDINAL_POSITION;
```

Quick required-column check:

```sql
SELECT required.columnName AS missingColumn
FROM (VALUES
    ('refundId'),
    ('bookingId'),
    ('paymentId'),
    ('paymentProviderId'),
    ('showtimeCancellationId'),
    ('refundAmount'),
    ('refundStatus'),
    ('refundReason'),
    ('providerRefundCode'),
    ('failureReason'),
    ('requestedAt'),
    ('refundedAt')
) AS required(columnName)
WHERE COL_LENGTH('dbo.REFUND', required.columnName) IS NULL;
```

The second query must return zero rows.

## Conditional SQL Patch for a Schema-Drifted Database

Do not run this patch when the database already matches
`docs/database/cinema-booking-schema.sql`. It is provided only for an older database
that has a `REFUND` table but is missing dashboard-required columns.

```sql
IF OBJECT_ID(N'dbo.REFUND', N'U') IS NULL
BEGIN
    THROW 50001,
        'dbo.REFUND does not exist. Apply the authoritative cinema-booking-schema.sql instead of a partial patch.',
        1;
END;
GO

IF COL_LENGTH('dbo.REFUND', 'refundAmount') IS NULL
BEGIN
    ALTER TABLE dbo.REFUND
        ADD refundAmount DECIMAL(18,2) NULL;
END;
GO

IF COL_LENGTH('dbo.REFUND', 'refundStatus') IS NULL
BEGIN
    ALTER TABLE dbo.REFUND
        ADD refundStatus NVARCHAR(30) NULL;
END;
GO

IF COL_LENGTH('dbo.REFUND', 'requestedAt') IS NULL
BEGIN
    ALTER TABLE dbo.REFUND
        ADD requestedAt DATETIME2 NULL;
END;
GO

IF COL_LENGTH('dbo.REFUND', 'refundedAt') IS NULL
BEGIN
    ALTER TABLE dbo.REFUND
        ADD refundedAt DATETIME2 NULL;
END;
GO
```

The temporary nullable definitions prevent a deployment from failing when legacy rows
exist. Existing data must be backfilled and validated before changing
`refundAmount`, `refundStatus`, and `requestedAt` to `NOT NULL`. For a database missing
foreign keys or most refund fields, apply the full authoritative schema rather than
incrementally inventing relationships.

## Test Coverage

Integration tests verify:

- Manager receives only the assigned cinema data.
- Another cinema's revenue is excluded.
- Admin bypasses cinema scope.
- Successful refunds reduce net revenue.
- Pending refunds are reported but do not reduce net revenue.
- A full successful refund removes all booking tickets from net ticket count.
- A partial refund does not incorrectly mark all booking tickets as refunded.
- Cancelled showtimes are excluded from occupancy.
- Invalid date ranges return `INVALID_DATE_RANGE`.
- Customer cannot access the Manager dashboard.

Commands:

```powershell
dotnet build CinemaSystem.sln --no-restore
dotnet test CinemaSystem.Tests\CinemaSystem.Tests.csproj --no-restore --filter ManagerDashboardApiIntegrationTests
dotnet test CinemaSystem.sln --no-build --no-restore
```

Results:

- Build: passed with 0 warnings and 0 errors.
- SCRUM-195 integration tests: 6 passed.
- Full solution: 244 passed, 0 failed, 0 skipped.

## Source Recovery and Hardcode Review - 2026-07-04

Repository history was checked before changing the current branch:

- `d26d24c` on `RevenueAndTicketOverview` is the completed SCRUM-195 commit.
- `8cc9267` is the earlier Manager dashboard implementation commit.
- `d26d24c` is already an ancestor of the current `Tom/remove-hardcodes` branch,
  so no cherry-pick or merge was required.
- `stash@{1}` contains an earlier dashboard cleanup, but its
  `ManagerDashboardService.cs` content already matches the current branch. The
  stash was not applied because it also contains unrelated refund changes.

The current runtime path was retained and the remaining avoidable literals were
removed:

- dashboard error codes are declared in `DomainConstants.ManagerDashboardErrorCode`
  and exposed through `BookingConstants`;
- HTTP status codes use `HttpStatusCode` instead of numeric `400` and `404`;
- the `movieId` validation limit is declared in
  `DashboardContractConstants.MovieIdMaxLength`;
- payment, refund, showtime, and seat statuses continue to use Domain-backed
  constants.

Unique response messages and zero-value arithmetic remain local because they are
not reusable domain rules or environment-dependent configuration.

This cleanup does not change the database schema and requires no SQL patch.

## Known Data Limitation

The schema cannot determine exactly which seat was refunded in a partial refund because
`REFUND` has no `bookingSeatId` or refund-line table. If product requirements later
require seat-level partial refunds, add a dedicated refund allocation entity rather than
deriving seat identity from refund amount.
