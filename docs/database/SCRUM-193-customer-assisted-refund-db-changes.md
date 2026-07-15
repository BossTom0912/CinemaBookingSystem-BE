# SCRUM-193 Customer-Assisted Refund Database Changes

## Purpose

This change aligns SQL Server with the existing EF Core mappings and refund
runtime flow. It fixes `GET /api/manager/refunds` failing with:

```text
Invalid object name 'REFUND_CLAIM'
```

## Files

- Full reset schema:
  `docs/database/cinema-booking-schema.sql`

For an existing database with data to keep, use
`docs/database/cinema-booking-schema-upgrade.sql`. The reset schema remains
for a disposable/local database only.

## Schema Changes

New tables used by the current EF model:

- `BANK_DIRECTORY`
- `REFUND_CLAIM`
- `REFUND_CLAIM_TOKEN`
- `CUSTOMER_REFUND_REQUEST`
- `MANUAL_REFUND_PROCESS`

`CK_REFUND_STATUS` now accepts:

- `PENDING`
- `PROCESSING`
- `SUCCESS`
- `FAILED`
- `REQUESTED`
- `MANUAL_REQUIRED`

Indexes and unique constraints:

- `UQ_BANK_DIRECTORY_BIN`
- `UQ_REFUND_CLAIM_REFUND`
- `UQ_REFUND_CLAIM_TOKEN_HASH`
- `UQ_MANUAL_REFUND_PROCESS_REFUND`
- `UQ_MANUAL_REFUND_PROCESS_CLAIM`
- `IX_REFUND_CLAIM_CUSTOMER_PROFILE_ID`
- `IX_REFUND_CLAIM_STATUS`
- `IX_REFUND_CLAIM_TOKEN_CLAIM`
- `IX_CUSTOMER_REFUND_REQUEST_CUSTOMER_STATUS`
- `IX_MANUAL_REFUND_PROCESS_STATUS_CREATED`
- `UX_MANUAL_REFUND_BANK_TRANSACTION_CODE`

Foreign keys connect the new tables to:

- `REFUND`
- `CUSTOMER_PROFILE`
- `BANK_DIRECTORY`
- `TICKET`
- `USER`

## Security

- Raw refund-claim tokens are never stored; only the SHA-256 token hash is
  persisted.
- Bank account and holder-name values are binary encrypted payloads.
- API read models expose only the account-number suffix.
- The SQL patch does not contain credentials, connection strings, or runtime
  secrets.

Status values and column lengths remain SQL literals because they define
database constraints and schema metadata. Runtime code continues to reference
Domain-backed constants.

## Seed Data

The patch idempotently adds five active bank-directory records:

- `VCB`
- `MB`
- `TCB`
- `BIDV`
- `CTG`

Provider capability flags default to disabled. The seed does not claim that
account inquiry or automatic payout is available.

## Apply

The refund schema is now part of `cinema-booking-schema.sql`, the canonical full
reset script. It recreates `CinemaBookingDB`, so it is appropriate only for a
disposable/local database. An existing database with retained data needs a
reviewed deployment migration outside this consolidated reset script.

Expected verification:

```text
DB_PATCH_APPLIED=1
REFUND_WORKFLOW_TABLES_FOUND=5/5
ACTIVE_BANKS=5
MANUAL_REFUND_PROCESS_EXISTS=1
```

## Deployment Notes

- Back up the target database before applying schema changes.
- Apply the patch before deploying code that queries `Refund.RefundClaim`.
- Do not drop these tables during rollback if they contain submitted customer
  bank data or completed manual-refund audit history.
- A rollback must be planned as a data migration, not as an unconditional
  `DROP TABLE`.
