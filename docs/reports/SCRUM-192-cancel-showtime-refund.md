# SCRUM-192 - Manager Cancel Showtime and Generate Refund Data

## Task

Implement backend APIs for Manager/Admin to cancel a showtime, block new ticket sales,
generate refund records for paid bookings, and list refund records with Manager cinema
scope filtering.

## Purpose

This feature is used when a cinema showtime must be cancelled because of an operational
issue such as projector failure, power outage, force majeure, or a management decision.
The system must stop new sales for the cancelled showtime, keep a traceable cancellation
record, move paid bookings into refund processing, and expose refund data for Manager
follow-up.

## Documentation Sources

- `docs/requirements/srs-group-2.docx`
  - UC003: Cancel Showtime and Auto-Refund.
  - Main flow: Manager/Admin cancels showtime, system blocks new booking, finds paid
    bookings, updates booking/ticket state, creates refund data, notifies customer, and
    writes audit log.
  - Preconditions: Manager can operate only within assigned cinema scope; Admin can
    operate system-wide.
- `docs/requirements/business-rules.docx`
  - BR-38: If a showtime is cancelled by Manager/Admin, paid bookings must move to
    Refund Pending.
  - BR-39: Successful auto-refund moves booking to Refunded.
  - BR-40: Failed auto-refund moves booking to Manual Refund Required.
  - BR-41: Customer must be notified when booking is cancelled or refunded.
  - BR-75: Do not delete showtime with paid bookings except through cancellation flow.
  - BR-79: Manager only views data for assigned cinema.
  - BR-82: Important operations such as cancellation and refund must be audited.
- `docs/architecture/backend-system-design-clean-architecture.docx`
  - Clean Architecture rules: Controller stays thin; use cases live behind Application
    interfaces; Infrastructure handles EF Core transaction work.
  - Section 10.6: Cancel Showtime + Refund flow.
  - Section 11.2: Cancel Showtime requires a transaction.
  - Section 12.2: Cancel Showtime and Process Refund require audit logging.
- `docs/architecture/conceptual-erd-explanation.docx`
  - Payment & Refund relationships: Booking 1-N Payment, Payment 1-0..N Refund,
    Booking 1-0..N Refund, Showtime 1-0..1 ShowtimeCancellation.
- `docs/architecture/database-deep-dive-vi.md`
  - Section 4.22: `SHOWTIME_CANCELLATION` stores cancellation event, actor, reason,
    and time.
  - Section 4.23: `REFUND` stores refund lifecycle independently from payment.
  - Core flow: `SHOWTIME.CANCELLED -> SHOWTIME_CANCELLATION -> REFUND ->
    BOOKING.REFUND_PENDING/REFUNDED -> TICKET.CANCELLED/REFUNDED -> NOTIFICATION`.
- `docs/database/cinema-booking-schema.sql`
  - Tables used: `SHOWTIME`, `SHOWTIME_SEAT`, `BOOKING`, `BOOKING_SEAT`, `TICKET`,
    `PAYMENT`, `SHOWTIME_CANCELLATION`, `REFUND`, `NOTIFICATION`, `AUDIT_LOG`.
  - Status constraints used: `SHOWTIME.CANCELLED`, `BOOKING.REFUND_PENDING`,
    `REFUND.PENDING`, `TICKET.CANCELLED`.
- `docs/api/api-contract-backend.docx`
  - Manager endpoint reference: `POST /api/manager/showtimes/{showtimeId}/cancel`
    and `GET /api/manager/refunds`.
- `docs/reports/SCRUM-190-manager-cinema-scope.md`
  - Existing LimitOfManager scope implementation reused through
    `ICinemaScopeAuthorizationService`.

## Implemented APIs

- `POST /api/showtimes/{showtimeId}/cancel`
  - Policy: `CanCancelShowtimeAndRefund`.
  - Scope: Manager must own the showtime cinema; Admin bypasses scope.
  - Request: `CancelShowtimeRequest`.
  - Response: `CancelShowtimeResponse`.
- `GET /api/manager/refunds`
  - Policy: `CanCancelShowtimeAndRefund`.
  - Scope: Manager sees only refunds from assigned cinema; Admin sees all.
  - Query: `status`, `showtimeId`, `from`, `to`.
  - Response: `IReadOnlyList<RefundResponse>`.

## Code Changes

- Contracts
  - `CinemaSystem.Contracts/Showtimes/CancelShowtimeRequest.cs`
  - `CinemaSystem.Contracts/Showtimes/CancelShowtimeResponse.cs`
  - `CinemaSystem.Contracts/Refunds/RefundQueryRequest.cs`
  - `CinemaSystem.Contracts/Refunds/RefundResponse.cs`
- Application interfaces
  - `CinemaSystem.Application/Interfaces/IShowtimeCancellationService.cs`
  - `CinemaSystem.Application/Interfaces/IRefundService.cs`
- Infrastructure services
  - `CinemaSystem.Infrastructure/Showtimes/ShowtimeCancellationService.cs`
  - `CinemaSystem.Infrastructure/Refunds/RefundService.cs`
- API controllers
  - `CinemaSystem/Controllers/ShowtimeCancellationsController.cs`
  - `CinemaSystem/Controllers/ManagerRefundsController.cs`
- Supporting changes
  - Added status constants to `BookingConstants`.
  - Registered new services in `DependencyInjection`.
  - Updated `SeatService` to reject seat lock and seat map access when showtime is not
    `OPEN`.
  - Updated public `GetShowtimesAsync` to list only `OPEN` showtimes.
  - Updated `PaymentService` so a late successful payment after cancellation creates
    refund data instead of reactivating booking/ticket.

## Business Behavior

- Cancelling a showtime creates one `SHOWTIME_CANCELLATION` record.
- Showtime status becomes `CANCELLED`.
- Showtime seats are marked `UNAVAILABLE` and locks are cleared.
- Paid bookings move to `REFUND_PENDING`.
- Tickets under paid bookings move to `CANCELLED`.
- One `REFUND` record is created for each paid booking with a successful payment.
- Pending/created bookings move to `CANCELLED` and do not generate refund records.
- Pending payments under cancelled unpaid bookings move to `CANCELLED`.
- Notifications are created for customer bookings.
- Audit log action `CANCEL_SHOWTIME` is created.
- Calling cancel again returns `SHOWTIME_ALREADY_CANCELLED` and does not duplicate refund
  records.

## Tests Added

Added `CinemaSystem.Tests/ShowtimeCancellationApiIntegrationTests.cs`:

- Manager assigned to the cinema cancels a showtime and creates refund data.
- Manager cannot cancel a showtime from another cinema.
- Manager refund list returns only refunds from the assigned cinema.
- Second cancel call returns conflict and does not duplicate refund records.

## Test Results

Commands run from repository root:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.Tests\CinemaSystem.Tests.csproj --no-build --filter ShowtimeCancellationApiIntegrationTests
dotnet test CinemaSystem.sln --no-build
```

Results:

- Build: passed.
- New task tests: 4 passed, 0 failed, 0 skipped.
- Full test suite: 249 passed, 0 failed, 0 skipped.

Build warnings:

- Existing nullable warnings remain in old test files:
  - `CinemaSystem.Tests/CinemaApiIntegrationTests.cs`
  - `CinemaSystem.Tests/MovieApiIntegrationTests.cs`
  - `CinemaSystem.Tests/RoomShowtimeServiceTests.cs`
- No new build error was introduced by this task.
