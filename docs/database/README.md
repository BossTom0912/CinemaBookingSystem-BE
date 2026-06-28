# Cinema Booking Database

## Authoritative schema

`cinema-booking-schema.sql` is the full SQL Server reset script. It drops and
recreates `CinemaBookingDB`, so it must not be run against a database that
contains data that has not been backed up.

The canonical schema follows the project documents and current EF Core model:

- authentication: `ROLE`, `USER`, `CUSTOMER_PROFILE`, `STAFF_PROFILE`,
  `EMAIL_VERIFICATION_TOKEN`, `REFRESH_TOKEN`;
- cinema operations: `CINEMA`, `ROOM`, `SEAT_TYPE`, `SEAT`, `SHOWTIME`,
  `SHOWTIME_SEAT`;
- movie catalog: `MOVIE`, `GENRE`, `MOVIE_GENRE`, `LANGUAGE`;
- booking/payment: `BOOKING`, `BOOKING_SEAT`, `PAYMENT`,
  `PAYMENT_PROVIDER`, `TICKET`, `CHECKIN_LOG`;
- cancellation/refund: `SHOWTIME_CANCELLATION`, `REFUND`,
  `REFUND_CLAIM`, `REFUND_CLAIM_TOKEN`, `CUSTOMER_REFUND_REQUEST`,
  `MANUAL_REFUND_PROCESS`, `REFUND_PAYOUT_ATTEMPT`, `BANK_DIRECTORY`;
- review/analytics/chat: `REVIEW`, `REVIEW_EDIT_HISTORY`,
  `REVIEW_MODERATION_HISTORY`, `MOVIE_VIEW_LOG`, `MOVIE_DAILY_VIEW`,
  `CHAT_HISTORY`;
- supporting data: voucher, F&B, reward points, notification, email outbox,
  and audit log.

## Existing-database patches

Run patches only against an existing `CinemaBookingDB`.

1. `profile-token-counter-sale-patch.sql`
   adds the earlier profile, auth-token, and counter-sale fields.
2. `customer-assisted-refund-patch.txt`
   adds the customer-assisted/manual refund workflow.
3. `Genre_Languge.sql`
   is the idempotent Admin schema-alignment patch. It:
   - creates `GENRE`, `MOVIE_GENRE`, and `LANGUAGE`;
   - migrates legacy `MOVIE.genre` and `MOVIE.language` data before dropping
     the old text columns;
   - adds `MOVIE.languageId` and `MOVIE.director`;
   - aligns movie view, chatbot, review, and moderation-history columns with
     the EF Core mappings;
   - adds missing Admin indexes and seed data.
4. `sprint-2-update-constraints.sql`
   idempotently aligns the `MOVIE`, `SHOWTIME`, and `BOOKING` status
   constraints required by the Admin maintenance/re-seat workflows.

`sprint-2-full-architecture.sql` and `sprint-2-review-and-views.sql` are
historical non-idempotent scripts. Do not run them after the canonical reset
script or the Admin alignment patch.

## Admin merge database decisions

- Genre is normalized as many-to-many (`MOVIE` -> `MOVIE_GENRE` -> `GENRE`).
- Language is normalized as many-to-one (`MOVIE.languageId` -> `LANGUAGE`).
- Existing genre and language text is migrated before legacy columns are
  removed.
- The canonical schema contains one `MOVIE.viewCount` column and one
  `MOVIE_VIEW_LOG` definition.
- `REVIEW.moderatedBy` remains nullable because automated moderation has no
  `USER` actor; manual Admin moderation stores the Admin user ID.
- Review status supports `PENDING`, `APPROVED`, `REJECTED`, and `FLAGGED`.
- Status constraints include `ARCHIVED`, `SUSPENDED`, and
  `PROCESSING_UNSTABLE` where the Admin use cases require them.

## Verification

After applying a patch:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.sln
```

Also verify that:

- `GENRE`, `LANGUAGE`, and `MOVIE_GENRE` exist;
- `MOVIE` contains `languageId` and `director`, but not legacy `genre` or
  `language`;
- `CHAT_HISTORY` contains `userMessage` and `aiReplyMessage`;
- `MOVIE_VIEW_LOG` contains `movieViewLogId` and `viewedAt`;
- the three status constraints were recreated successfully.
