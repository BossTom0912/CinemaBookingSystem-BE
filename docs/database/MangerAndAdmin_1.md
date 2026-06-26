# MangerAndAdmin_1 Database Merge Notes

## Purpose

This README summarizes database changes brought into branch `MangerAndAdmin_1`
after merging:

- `CancelPerformanceAndGenerateDataRefund`
- `Admin`

It is based on the current files in `docs/database` and branch diffs against
their merge bases.

## Existing Database README

The existing database README is:

- `docs/database/README.md`

That file currently contains a full reset-style schema script explanation and
the consolidated schema content. This branch-specific README is an additional
summary, not a replacement.

## Files Reviewed

- `docs/database/README.md`
- `docs/database/cinema-booking-schema.sql`
- `docs/database/cinema-booking-schema.txt`
- `docs/database/customer-assisted-refund-patch.txt`
- `docs/database/sprint-2-full-architecture.sql`
- `docs/database/sprint-2-review-and-views.sql`

## Added By CancelPerformanceAndGenerateDataRefund

This branch adds the customer-assisted refund and manual refund database flow.

### New tables

- `BANK_DIRECTORY`
  Stores supported banks by bank code, BIN, short name, full name, active flag,
  and provider capability flags.

- `REFUND_CLAIM`
  Stores customer-submitted refund payout information. Sensitive bank data is
  designed to be encrypted before persistence. It links one-to-one with
  `REFUND` and links to `CUSTOMER_PROFILE` and optionally `BANK_DIRECTORY`.

- `REFUND_CLAIM_TOKEN`
  Stores short-lived hashed claim tokens for customer refund claim links. The
  schema stores only `tokenHash`, not the raw token.

- `CUSTOMER_REFUND_REQUEST`
  Stores customer refund/reissue requests tied to `REFUND`, customer profile,
  and optionally a ticket.

- `MANUAL_REFUND_PROCESS`
  Tracks admin/manual payout processing for refunds requiring manual bank
  transfer. It stores assignment, transfer amount, proof URL, admin note,
  confirmation time, and row version.

- `REFUND_PAYOUT_ATTEMPT`
  Tracks provider payout attempts with idempotency key, attempt number,
  provider request/transaction codes, status, and failure details.

- `EMAIL_OUTBOX`
  Stores encrypted email payloads for retryable email delivery.

### New or changed constraints and indexes

- `UQ_BANK_DIRECTORY_BIN`
- `UQ_REFUND_CLAIM_REFUND`
- `UQ_REFUND_CLAIM_TOKEN_HASH`
- `UQ_MANUAL_REFUND_PROCESS_REFUND`
- `UQ_MANUAL_REFUND_PROCESS_CLAIM`
- `UX_MANUAL_REFUND_BANK_TRANSACTION_CODE`
- `IX_REFUND_CLAIM_CUSTOMER_PROFILE_ID`
- `IX_REFUND_CLAIM_STATUS`
- `IX_REFUND_CLAIM_TOKEN_CLAIM`
- `IX_CUSTOMER_REFUND_REQUEST_CUSTOMER_STATUS`
- `IX_REFUND_PAYOUT_ATTEMPT_REFUND_STATUS`
- `IX_MANUAL_REFUND_PROCESS_STATUS_CREATED`
- `IX_EMAIL_OUTBOX_STATUS_NEXT_ATTEMPT`

### Important status values

- `REFUND.refundStatus`: includes `REQUESTED` in addition to the refund states
  already used by payment/refund flows.
- `REFUND_CLAIM.claimStatus`: `PENDING_INFO`, `VERIFIED`, `SUBMITTED`,
  `PROCESSING`, `COMPLETED`, `EXPIRED`, `MANUAL_REQUIRED`, `REVOKED`.
- `REFUND_CLAIM.accountValidationStatus`: `NOT_STARTED`, `VERIFIED`,
  `FAILED`, `UNAVAILABLE`.
- `MANUAL_REFUND_PROCESS.processStatus`: `OPEN`, `IN_PROGRESS`, `CONFIRMED`,
  `REJECTED`.
- `REFUND_PAYOUT_ATTEMPT.attemptStatus`: `CREATED`, `SUBMITTED`, `ACCEPTED`,
  `CONFIRMED`, `FAILED`, `UNKNOWN`.
- `EMAIL_OUTBOX.outboxStatus`: `PENDING`, `PROCESSING`, `SENT`, `FAILED`.

### Seed data

`customer-assisted-refund-patch.txt` seeds the following bank directory rows:

- `VCB`
- `MB`
- `TCB`
- `BIDV`
- `CTG`

## Added By Admin

This branch adds database support for movie CRUD/admin features, review
moderation, movie view tracking, chatbot history, and movie highlight
classification.

### New movie columns

The current merged schema adds these columns to `MOVIE`:

- `highlight`
- `viewCount`
- `averageRating`
- `totalReviews`
- `totalViews`
- `dailyViews`

`CK_MOVIE_HIGHLIGHT` limits `highlight` to:

- `HOT`
- `NEW`
- `TRENDING`

### New user moderation columns

The current merged schema adds review-moderation blocking fields to `USER`:

- `spamViolationCount`
- `isBlocked`
- `blockedUntil`

### Review-related changes

- `REVIEW` is linked to `BOOKING` through `bookingId`.
- A filtered unique index `UX_REVIEW_BOOKING` prevents more than one review for
  the same booking when `bookingId` is not null.
- Review moderation references user/admin moderation data.

### New tables

- `CHAT_HISTORY`
  Stores chatbot conversation messages by user/session.

- `MOVIE_VIEW_LOG`
  Stores per-view movie tracking data such as movie, user, timestamp, and IP.

- `MOVIE_DAILY_VIEW`
  Stores per-movie, per-day aggregated view counts.

- `REVIEW_EDIT_HISTORY`
  Stores old/new comment and rating data for review edits.

- `REVIEW_MODERATION_HISTORY`
  Stores review moderation status changes and moderation reasons.

### New indexes from Admin scripts

- `IX_REVIEW_EDIT_HISTORY_REVIEW_ID`
- `IX_REVIEW_MODERATION_HISTORY_REVIEW_ID`
- `IX_MOVIE_DAILY_VIEW_DATE`
- `IX_MOVIE_HIGHLIGHT_VIEWS`

## Current Consolidated DB Impact

After merging both branches, the database model supports:

- Customer refund claim collection after cancellation/refund creation.
- Manual admin refund assignment and confirmation.
- Payout attempt tracking and idempotency for provider refund attempts.
- Encrypted/retryable email outbox for refund-related emails.
- Bank directory reference data for refund payout account details.
- Movie view logging and daily view aggregation.
- Movie highlight classification based on view/review metrics.
- Review creation linked to bookings.
- Review edit and moderation history.
- User blocking fields for review spam/moderation enforcement.
- Chatbot message history.

## Schema Issues To Fix Before Running Full Reset Script

The merged `docs/database/cinema-booking-schema.sql` currently has issues that
should be cleaned before using it as a fresh database reset script:

- `MOVIE.viewCount` is declared twice in the `MOVIE` table.
- `MOVIE_VIEW_LOG` is created twice with different column names:
  - first version: `movieViewLogId`, `viewedAt`
  - second version: `viewLogId`, `viewTime`
- `REVIEW` defines foreign key `FK_REVIEW_MODERATED_BY` on `moderatedBy`, but
  the visible `REVIEW` table definition should be checked to ensure the column
  is declared before the constraint.
- `docs/database/sprint-2-full-architecture.sql` and
  `docs/database/sprint-2-review-and-views.sql` overlap with parts already
  merged into `cinema-booking-schema.sql`; running them after the full reset
  script may fail unless wrapped with existence checks.

## Recommended Cleanup

Before applying the merged schema to a clean SQL Server database:

1. Keep only one `MOVIE.viewCount` column.
2. Choose one canonical `MOVIE_VIEW_LOG` shape and remove the duplicate table
   creation.
3. Verify `REVIEW` includes all columns referenced by its constraints.
4. Make the sprint patch scripts idempotent with `IF COL_LENGTH`, `IF OBJECT_ID`,
   and `IF NOT EXISTS` checks.
5. Re-run `dotnet test CinemaSystem.sln` after the schema cleanup because EF
   mappings and integration tests depend on these table names and columns.
