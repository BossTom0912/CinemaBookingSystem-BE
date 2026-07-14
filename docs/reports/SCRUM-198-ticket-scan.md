# SCRUM-198 - Manager/Staff Ticket Scan

## Purpose

Implement `POST /api/tickets/scan` for Staff, Manager, and Admin while enforcing
cinema scope, ticket state, screening room, showtime state, and the configured
check-in window.

## Authoritative rules

- SRS UC002: Scan Ticket QR.
- BR-61: one ticket may be checked in only once.
- BR-62: ticket must match cinema, room, showtime, date/time, and be unused.
- BR-63: repeat scans must return a warning.
- BR-64: cancelled/refunded tickets cannot be checked in.
- BR-78: Staff may scan only within the assigned cinema.
- BR-79: Manager data access is limited to the managed cinema.
- BR-80: Admin has system-wide access.

## API

```http
POST /api/tickets/scan
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "qrCode": "G2C|...",
  "roomId": "ROOM_001"
}
```

`roomId` is required because a QR value alone cannot prove that the scan
occurred at the correct screening room.

## Role and cinema-scope behavior

- Customer: rejected by `CanScanTicket`.
- Staff: active Staff profile required; ticket cinema must match profile cinema.
- Manager: active Staff profile required; ticket cinema must match managed cinema.
- Admin: bypasses cinema scope.

The controller delegates cinema-scope resolution to
`ICinemaScopeAuthorizationService`. It does not duplicate role logic.

## Transaction and concurrency behavior

The service:

1. Resolves the authenticated actor and optional Staff profile.
2. Loads Ticket, BookingSeat, ShowtimeSeat, Seat, Booking, Showtime, Room,
   Cinema, and Movie.
3. Validates cinema, room, ticket status, booking status, showtime status, and
   configured UTC time window.
4. Atomically updates only a row whose status is still `UNUSED`.
5. Writes a `CHECKIN_LOG` row for every accepted or rejected scan.
6. Commits the ticket update and success log in one serializable transaction.

On SQL Server, the status change uses a conditional update. Two concurrent
scans cannot both change the same ticket from `UNUSED` to `CHECKED_IN`.

## Hardcode policy

- Ticket/check-in statuses, error codes, and ID prefixes are in
  `DomainConstants`.
- Application compatibility aliases are in `BookingConstants`.
- QR and ID validation bounds are in `TicketContractConstants`.
- Check-in time windows are mandatory `TicketScanSettings` values.
- Role names and policy names remain in `AuthConstants`.
- EF table, column, constraint, and index literals remain schema metadata.

There is no fallback check-in window in code. Deployment must configure values
approved by PM/BA.

## Database changes

See:

- `docs/database/SCRUM-198-ticket-scan-db-changes.md`
- `docs/database/cinema-booking-schema.sql` (canonical reset schema)

The change adds `CHECKIN_LOG.scannedByUserId`, makes `staffProfileId` nullable,
and preserves the exact authenticated actor for Admin scans.

## Verification

- `dotnet build CinemaSystem.sln --no-restore`: passed with 0 warnings and
  0 errors.
- `dotnet test CinemaSystem.sln --no-build --no-restore`: passed 244/244 tests.
- Focused `TicketScanApiIntegrationTests`: passed 13/13 tests.

Focused integration coverage includes:

- Staff success in assigned cinema.
- Manager success in managed cinema.
- Admin bypass without a Staff profile.
- Customer forbidden.
- JWT role rejected when it no longer matches the current database role.
- Manager blocked from another cinema.
- Wrong room.
- Repeat scan.
- Cancelled and refunded ticket.
- Cancelled showtime.
- Unknown QR with failed log.
- Scan before the configured opening window.
