# SCRUM-198 - API Soat Ve (Scan Ticket)

## Task

Implement backend API for Staff/Manager to scan an e-ticket QR code at the cinema,
validate the ticket, update check-in state, and write scan history.

Endpoint implemented:

- `POST /api/staff/tickets/scan`

## Documentation Sources

- `docs/requirements/srs-group-2.docx`
  - Section `3.2.2 Quet ma QR ve (Scan Ticket QR)` defines Staff as the main actor
    and Manager as the support actor.
  - The use case requires the system to load ticket, booking, showtime, and cinema
    data; validate cinema/room/showtime/time/status; update a valid ticket to
    `Checked-in/Used`; and leave invalid tickets unchanged while showing a warning.
  - The business process summary states that Staff/Manager scans the QR and the
    system checks ticket ID, cinema, showtime, and ticket status.
- `docs/requirements/business-rules.docx`
  - BR-61: one ticket can be checked in only once.
  - BR-62: a ticket is valid only for the correct cinema, room, showtime, date/time,
    and unused status.
  - BR-63: if already checked in, Staff/Manager must see a warning.
  - BR-64: cancelled/refunded booking tickets cannot check in.
  - BR-65: Staff/Manager can reject check-in if the customer cannot provide valid
    confirmation or order/account information.
- `docs/api/api-contract-backend.docx`
  - Staff API reference lists `POST /api/staff/tickets/scan` for QR-code ticket
    validation.
- `docs/architecture/backend-system-design-clean-architecture.docx`
  - Section `10.5 Scan QR Ticket` requires a transaction that loads Ticket ->
    BookingSeat -> ShowtimeSeat -> Showtime -> Room -> Cinema, validates staff
    cinema permission, ticket status, showtime status, and check-in time window,
    updates `ticketStatus = CHECKED_IN`, and creates `CHECKIN_LOG`.
  - Section `11.1 Concurrency Strategy` says repeated scans must check ticket status
    before update and still write `CheckInLog`.
  - Section `11.2 Transaction Boundary` says scan QR needs a transaction because
    ticket update and check-in log insert must be consistent.
  - Clean Architecture sections require thin controllers and business logic behind
    Application interfaces.
- `docs/architecture/conceptual-erd-explanation.docx`
  - `BookingSeat 1 ---- 0..1 Ticket`: each paid booking seat can have its own ticket.
  - `Ticket 1 ---- N CheckInLog`: repeated scans can create multiple logs, but only
    one successful check-in is allowed.
  - `StaffProfile 1 ---- N CheckInLog`: logs must record who scanned.
- `docs/database/cinema-booking-schema.sql`
  - `TICKET.qrCode` is unique and `ticketStatus` is constrained to
    `GENERATED`, `UNUSED`, `CHECKED_IN`, `CANCELLED`, `REFUNDED`.
  - `CHECKIN_LOG.staffProfileId` is `NOT NULL`, `ticketId` is nullable, and `result`
    is constrained to `SUCCESS` or `FAILED`.

## Existing Code Dependencies

- `PaymentService.ConfirmPaymentAsync` already generates one `Ticket` per
  `BookingSeat` after successful payment and sets ticket status to `UNUSED`.
- `ShowtimeCancellationService` already moves tickets to `CANCELLED` when a showtime
  is cancelled/refund flow starts.
- `AuthConstants.Policies.CanScanTicket` and `Program.cs` already allow
  Staff/Manager/Admin by policy.
- `ICinemaScopeAuthorizationService` already resolves Staff/Manager cinema scope from
  active `STAFF_PROFILE.cinemaId`; Admin bypasses cinema scope.

## Design Decisions

- QR validation uses exact `TICKET.qrCode` lookup. The QR string is not parsed as a
  trusted source because the persisted unique QR code is the source of truth.
- Staff and Manager are limited to their assigned cinema through
  `ICinemaScopeAuthorizationService`.
- Admin can pass the existing `CanScanTicket` policy, but must still have an active
  `StaffProfile` to write `CHECKIN_LOG.staffProfileId`. Without it the API returns
  `403 STAFF_PROFILE_REQUIRED`. This follows the database rule that every check-in log
  must reference a staff profile.
- The default valid check-in window is configurable:
  `TicketSettings:CheckInLeadMinutes = 30`. The implementation allows check-in from
  30 minutes before `Showtime.startTime` until `Showtime.endTime`. This fills a gap in
  the docs: the docs require a valid time window, but do not specify the exact number
  of minutes.
- Failed business validations still create `CHECKIN_LOG` with `result = FAILED`,
  `rawQrCode`, and `failureReason` when an active staff profile is known.
- Successful scan updates `Ticket.ticketStatus` to `CHECKED_IN` and creates
  `CHECKIN_LOG` with `result = SUCCESS` in one service operation.

## Code Changes

- Contracts
  - `CinemaSystem.Contracts/Tickets/ScanTicketRequest.cs`
  - `CinemaSystem.Contracts/Tickets/ScanTicketResponse.cs`
- Application
  - `CinemaSystem.Application/Interfaces/ITicketScanService.cs`
  - Added scan-ticket error codes in `BookingConstants`.
- Infrastructure
  - `CinemaSystem.Infrastructure/Tickets/TicketScanService.cs`
  - `CinemaSystem.Infrastructure/Configuration/TicketSettings.cs`
  - Registered `ITicketScanService` and `TicketSettings` in dependency injection.
- API
  - `CinemaSystem/Controllers/StaffTicketsController.cs`
- Configuration
  - Added `TicketSettings:CheckInLeadMinutes` to
    `CinemaSystem/appsettings.Development.example.json`.
- Tests
  - `CinemaSystem.Tests/TicketScanApiIntegrationTests.cs`

## Test Coverage Added

- Staff assigned to the correct cinema scans a valid ticket:
  - returns `200 OK`;
  - ticket becomes `CHECKED_IN`;
  - a `SUCCESS` check-in log is written.
- Scanning the same ticket again:
  - returns `409 TICKET_ALREADY_CHECKED_IN`;
  - ticket remains `CHECKED_IN`;
  - a second `FAILED` log is written.
- Staff assigned to another cinema scans a ticket:
  - returns `403 CINEMA_SCOPE_FORBIDDEN`;
  - ticket remains `UNUSED`;
  - a `FAILED` log is written.
- Unknown QR code:
  - returns `404 TICKET_NOT_FOUND`;
  - writes a `FAILED` log with `ticketId = null`.
- Ticket scanned too early:
  - returns `409 CHECKIN_TIME_NOT_ALLOWED`;
  - ticket remains `UNUSED`;
  - writes a `FAILED` log.

## Test Results

Commands run from repository root:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.sln --no-build --filter TicketScanApiIntegrationTests
dotnet test CinemaSystem.sln --no-build
```

Results:

- Build: passed.
- Scan ticket tests: 5 passed, 0 failed, 0 skipped.
- Full test suite: 257 passed, 0 failed, 0 skipped.

Build warnings:

- Existing nullable warnings remained in unrelated test files:
  - `CinemaApiIntegrationTests.cs`
  - `MovieApiIntegrationTests.cs`
  - `RoomShowtimeServiceTests.cs`

## PM/BA Review Notes

- Confirm whether the 30-minute pre-showtime check-in lead time is acceptable. It is
  configurable and can be changed without code changes.
- Confirm whether Admin accounts should be allowed to scan without a `StaffProfile`.
  Current implementation requires `StaffProfile` because `CHECKIN_LOG.staffProfileId`
  is mandatory in the database schema.
