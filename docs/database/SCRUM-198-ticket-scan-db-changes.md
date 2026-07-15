# SCRUM-198 - Ticket Scan DB Changes

## Purpose

Support ticket check-in for Staff, Manager, and Admin without attributing an
Admin scan to an unrelated Staff profile.

## Schema changes

Table: `CHECKIN_LOG`

1. Add `scannedByUserId NVARCHAR(50) NOT NULL`.
2. Add foreign key `FK_CHECKIN_LOG_SCANNED_BY_USER` to `USER(userId)`.
3. Change `staffProfileId` from required to nullable.
4. Add index
   `IX_CHECKIN_LOG_SCANNED_BY_USER_TIME(scannedByUserId, scanTime)`.

`scannedByUserId` is the canonical authenticated actor for every scan.
`staffProfileId` remains populated for Staff/Manager when an active profile is
available, but may be null for Admin.

## Database setup

The schema is consolidated into `docs/database/cinema-booking-schema.sql`.
It creates `CHECKIN_LOG` with `scannedByUserId` from the outset and is a full
database reset script. For an existing database, use
`docs/database/cinema-booking-schema-upgrade.sql`; it rolls back rather than
inventing an actor for an unmappable historic scan.

## Application time rule

Ticket check-in now opens at the start of the local showtime date and closes
30 minutes after the showtime starts. The application no longer requires
`TicketScanSettings` at startup.

## Role behavior

- Staff: may scan only tickets for the cinema in the active Staff profile.
- Manager: may scan only tickets for the managed cinema.
- Admin: bypasses cinema scope but is still recorded through
  `scannedByUserId`.
- Customer: denied by the existing `CanScanTicket` policy.

## Rollback note

Do not remove `scannedByUserId` after the new API has started writing Admin
scan logs. A rollback requires first ensuring every row has a valid
`staffProfileId`.
