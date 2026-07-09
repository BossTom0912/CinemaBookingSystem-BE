# MangerAndAdmin_1 - Admin Merge and Database Report

- Date: 2026-06-28
- Target branch: `MangerAndAdmin_1`
- Source branch: `Admin`
- Source commit: `45471c6b91963aa2a463592ae6a74d3597a8c5be`
- Previous target commit: `e616642c30f6a5bfa52a9fe97fffbd95e2eebcb6`

## 1. Result

The latest `Admin` branch was fetched from `origin` and merged into
`MangerAndAdmin_1`.

The merge brought four Admin commits that were not yet present in the target:

- `4a0d2b4` - cascading maintenance, re-seat logic, and token-based time-change
  approval;
- `01a8719` - payment/cancellation fixes and EF relationship fixes;
- `104b51f` - Admin workflow documentation;
- `385438c` and `45471c6` - database/readme follow-up changes.

The final integrated solution:

- builds successfully;
- passes all `225` current automated tests;
- has no unresolved Git conflict markers;
- has the Admin database changes applied to the configured local
  `CinemaBookingDB`;
- preserves the Manager cinema-scope and customer-assisted refund use cases.

The pre-existing untracked file
`docs/database/CancelPerformanceAndGenerateDataRefund_Admin_DB_Changes.txt`
was inspected and preserved, but was not overwritten or staged.

## 2. Documents used

The implementation and conflict decisions were checked against:

- `docs/requirements/srs-group-2.docx`;
- `docs/requirements/business-rules.docx`;
- `docs/architecture/backend-system-design-clean-architecture.docx`;
- `docs/architecture/conceptual-erd-explanation.docx`;
- `docs/api/api-contract-backend.docx`;
- `docs/database/cinema-booking-schema.sql`;
- `docs/reports/sprint-1-auth-implementation.md`;
- `docs/reports/SCRUM-190-manager-cinema-scope.md`;
- `docs/reports/SCRUM-192-cancel-showtime-refund.md`;
- `docs/reports/SCRUM-193-customer-assisted-refund.md`.

The main rules applied were:

- public registration creates only Customer accounts;
- Manager access is restricted to `STAFF_PROFILE.cinemaId`;
- Admin can access all cinemas;
- Manager cannot view full bank-account data or confirm a manual refund;
- only Admin can assign and complete a manual refund;
- cancellation/refund, role, showtime, and other sensitive changes must be
  auditable;
- database and external-service logic remain in Infrastructure, not
  controllers.

## 3. Merge conflicts

Six content conflicts occurred.

| File | Conflict | Resolution |
|---|---|---|
| `CinemaSystem.Infrastructure/Movies/MovieService.cs` | Movie detail query did not load normalized genres on the target side. | Kept Admin `MovieGenres -> Genre` loading and then fixed create/update so returned DTOs have populated genre navigation data. |
| `CinemaSystem.Infrastructure/Services/AdminRefundService.cs` | Target selected the first payment without validating whether it still existed; Admin added a defensive lookup. | Kept Admin payment validation and foreign-key-safe refund creation. |
| `CinemaSystem.Infrastructure/Services/PaymentService.cs` | Admin status constants overlapped with the existing late-payment customer-assisted refund flow. | Kept Admin constants/idempotency and preserved the existing `REFUND_CLAIM` creation required by UC003/BR-84..BR-92. |
| `CinemaSystem.Infrastructure/Services/ReviewService.cs` | Target moderated synchronously; Admin queued moderation through Hangfire. | Kept Admin asynchronous `PENDING` workflow. Automated moderation now stores `moderatedBy = null` because that column is a `USER` foreign key; a manual Admin action stores the real Admin user ID. |
| `CinemaSystem.Infrastructure/Services/SeatService.cs` | Target had an unused `affectedShowtimeIds` variable; Admin removed it. | Kept Admin cleanup and maintenance cascade. |
| `docs/database/README.md` | Both branches modified the embedded schema copy. | Replaced the duplicated schema body with a database guide that points to the canonical reset script and the idempotent patches. |

Files such as `CinemaDbContext`, `RoomService`, `ShowtimeService`,
`ShowtimesController`, and `cinema-booking-schema.sql` were auto-merged by
Git, but were reviewed and corrected where the combined result was
inconsistent.

## 4. Admin database analysis

### 4.1 New model introduced by Admin

Admin normalizes movie classification:

```text
LANGUAGE 1 ---- N MOVIE
MOVIE N ---- N GENRE, through MOVIE_GENRE
```

New or aligned movie fields:

- `MOVIE.languageId`;
- `MOVIE.director`;
- `MOVIE.highlight`;
- `MOVIE.viewCount`;
- `MOVIE.averageRating`;
- `MOVIE.totalReviews`;
- `MOVIE.totalViews`;
- `MOVIE.dailyViews`.

Admin maintenance states:

- `MOVIE.ARCHIVED`;
- `SHOWTIME.SUSPENDED`;
- `SHOWTIME.PROCESSING_UNSTABLE`;
- `BOOKING.PROCESSING_UNSTABLE`.

Admin review/chat/view fields and tables:

- `USER.spamViolationCount`, `isBlocked`, `blockedUntil`;
- `REVIEW.status`, `editCount`, `rejectedReason`, `moderatedBy`;
- `REVIEW_EDIT_HISTORY`;
- `REVIEW_MODERATION_HISTORY`;
- `MOVIE_VIEW_LOG`;
- `MOVIE_DAILY_VIEW`;
- `CHAT_HISTORY`.

### 4.2 Problems found in the incoming SQL

The incoming `Genre_Languge.sql` could not safely be run as received because
it:

- was not idempotent;
- dropped `MOVIE.genre` before migrating existing data;
- renamed `MOVIE.language` without normalizing old language text;
- recreated tables and seeds unconditionally;
- did not align older `CHAT_HISTORY`, `MOVIE_VIEW_LOG`, or review-history
  column names with the EF Core model.

The merged reset schema also still contained known historical errors:

- duplicate `MOVIE.viewCount`;
- two incompatible `MOVIE_VIEW_LOG` definitions;
- `REVIEW` referenced `moderatedBy` before defining the column;
- `CHAT_HISTORY` columns did not match `ChatHistory`;
- review-history key/column names did not match EF Core.

### 4.3 Database corrections

`docs/database/Genre_Languge.sql` was converted into an idempotent,
transactional Admin schema-alignment patch. It now:

1. Creates `GENRE`, `LANGUAGE`, and `MOVIE_GENRE` only when absent.
2. Seeds language and Admin genre values without duplicates.
3. Splits and migrates legacy movie genre text into `MOVIE_GENRE`.
4. Maps legacy language text to supported language IDs.
5. Adds the language foreign key only after data normalization.
6. Drops legacy `genre` and `language` only after migration.
7. Aligns movie aggregates, review fields, view logs, chatbot history, and
   review-history columns with the EF model.
8. Adds missing Admin indexes.
9. Rolls back the complete patch if any step fails.

`docs/database/sprint-2-update-constraints.sql` was also made transactional
and safe to rerun.

The canonical `docs/database/cinema-booking-schema.sql` was corrected to:

- contain one movie view counter and one view-log table;
- create normalized language/genre tables;
- use the EF-compatible chat and review-history shapes;
- seed normalized languages, genres, and movie-genre mappings;
- include Admin status values and indexes.

### 4.4 Automatic application to the local database

The patches were run against the configured local `CinemaBookingDB`.

Verification after execution:

```text
GENRE rows: 116
LANGUAGE rows: 7
MOVIE_GENRE rows migrated/seeded: 10
Movies with languageId: 4
Legacy MOVIE.genre column: removed
Legacy MOVIE.language column: removed
MOVIE.languageId: present
MOVIE.director: present
CHAT_HISTORY.userMessage/aiReplyMessage: present
MOVIE_VIEW_LOG.movieViewLogId/viewedAt: present
REVIEW.status: present
CK_MOVIE_STATUS: present
CK_SHOWTIME_STATUS: present
CK_BOOKING_STATUS: present
```

The first patch attempt failed at SQL Server batch compilation and rolled
back completely. Column-dependent statements were changed to dynamic SQL,
then the full patch succeeded. No partial schema was left by the failed
attempt.

## 5. Admin and Manager login

Admin and Manager do not have separate login endpoints. Both use:

```http
POST /api/auth/login
```

Implementation:

- API: `CinemaSystem/Controllers/AuthController.cs`;
- use case contract: `CinemaSystem.Application/Interfaces/IAuthService.cs`;
- implementation: `CinemaSystem.Infrastructure/Auth/AuthService.cs`;
- password hashing: `CinemaSystem.Infrastructure/Security`;
- JWT creation: `CinemaSystem.Infrastructure/Identity/JwtTokenService.cs`;
- policies/JWT validation: `CinemaSystem/Program.cs`.

Login flow:

1. Normalize email to trimmed lowercase.
2. Load `USER` and `ROLE`.
3. Verify the submitted password against `USER.passwordHash`.
4. Reject invalid credentials with `401 INVALID_CREDENTIALS`.
5. Reject unverified email with `403 EMAIL_NOT_VERIFIED` unless the explicit
   development auto-confirm option is enabled.
6. Reject any status other than `ACTIVE` with
   `403 ACCOUNT_NOT_ACTIVE`.
7. Create a short-lived JWT access token containing:
   - `sub`/`userId`;
   - email;
   - normalized role (`ADMIN` or `MANAGER`);
   - standard name-identifier and role claims.
8. Generate a cryptographically random refresh token.
9. Store only the refresh-token hash in `REFRESH_TOKEN`.
10. Return access token, raw refresh token, expiry, user identity, and role.

Refresh/logout:

- `POST /api/auth/refresh-token` validates the stored hash, expiry,
  revocation, user status, and rotates/revokes the old token;
- `POST /api/auth/logout` revokes the stored refresh token.

Provisioning boundary:

- `POST /api/auth/register` always creates Customer;
- `POST /api/admin/staff` currently creates Staff only;
- there is currently no completed API for creating Manager/Admin or changing
  a user's role;
- Manager/Admin accounts therefore must be provisioned through controlled
  seed/internal administration until UC-AD08 is completed.

### 5.1 What changes after login

Admin:

- JWT role is `ADMIN`;
- passes Admin-only policies;
- `CinemaScopeAuthorizationService` returns unrestricted scope;
- can access Admin manual-refund and staff-provisioning APIs.

Manager:

- JWT role is `MANAGER`;
- must have an active `STAFF_PROFILE`;
- cinema scope comes from `STAFF_PROFILE.cinemaId`;
- cross-cinema room, seat, showtime, cancellation, and refund access returns
  `403 CINEMA_SCOPE_FORBIDDEN`;
- missing active profile returns
  `403 STAFF_PROFILE_SCOPE_NOT_FOUND`.

## 6. Implemented Admin functions and locations

### UC-AD01 - Manage Movies

Endpoints:

```http
GET    /api/movies
GET    /api/movies/{movieId}
POST   /api/movies
PUT    /api/movies/{movieId}
DELETE /api/movies/{movieId}
POST   /api/movies/{movieId}/view
```

Locations:

- `CinemaSystem/Controllers/MoviesController.cs`;
- `CinemaSystem.Infrastructure/Movies/MovieService.cs`;
- movie DTOs under `CinemaSystem.Contracts/Movies`;
- `Movie`, `Genre`, `MovieGenre`, and `Language` in
  `CinemaSystem.Domain/Entities`;
- mapping in `CinemaSystem.Infrastructure/Persistence/CinemaDbContext.cs`.

Behavior includes normalized genre/language validation, director, view
tracking, highlight calculation, poster handling, soft delete, and cascading
showtime/refund handling.

### UC-AD03/AD04 - Manage Rooms and Seat Map

Endpoints are under:

```text
/api/rooms
/api/seats
```

Locations:

- `RoomsController`, `SeatsController`;
- `RoomService`, `SeatService`;
- `CinemaScopeAuthorizationService`.

Admin can work across all cinemas. Manager is restricted to the assigned
cinema. Room/seat maintenance can suspend affected future showtimes and queue
customer email actions.

### UC-AD05 - Manage Showtimes

Endpoints:

```http
POST   /api/showtimes
PUT    /api/showtimes/{showtimeId}
POST   /api/showtimes/{showtimeId}/change-room
DELETE /api/showtimes/{showtimeId}
GET    /api/bookings/{bookingId}/confirm-time-change
```

Locations:

- `ShowtimesController`;
- `BookingsController`;
- `ShowtimeService`;
- `BookingService`.

Behavior includes overlap checks, automatic `SHOWTIME_SEAT` generation,
maintenance states, room-change/re-seat flow, token-based time-change
confirmation, customer notification, and cancellation/refund delegation.

### UC003 - Cancel Showtime and Refund

Shared Manager/Admin endpoint:

```http
POST /api/manager/showtimes/{showtimeId}/cancel
```

Admin manual processing:

```http
GET  /api/admin/refunds
POST /api/admin/refunds/{bookingId}/confirm
GET  /api/admin/refunds/manual
POST /api/admin/refunds/{refundId}/assign
POST /api/admin/refunds/{refundId}/manual-confirm
```

Locations:

- `ShowtimeCancellationsController`;
- `AdminRefundsController`;
- `ShowtimeCancellationService`;
- `AdminRefundService`;
- `ManualRefundService`;
- `RefundProcessor`;
- `RefundClaimService`.

The manual-confirm transaction updates refund, claim, booking, tickets,
reward-point reversal, notification, and audit data before sending success
email.

### Review moderation

Endpoints:

```http
POST /api/reviews
PUT  /api/reviews/{reviewId}
PUT  /api/reviews/admin/{reviewId}/approve
```

Locations:

- `ReviewsController`;
- `ReviewService`;
- Admin review/history tables in `CinemaDbContext`.

Commented reviews enter `PENDING`, are moderated by a background job, and can
be approved by Admin. Review access is verified against a paid/completed
booking whose showtime has ended.

### Staff provisioning

Endpoint:

```http
POST /api/admin/staff
```

Locations:

- `AdminController`;
- `AdminService`.

It creates a Staff `USER`, `STAFF_PROFILE`, hashed invitation OTP, and queues
the invitation email. This is Admin-only.

### Chatbot and view history

Locations:

- `ChatbotController`;
- `GeminiChatbotService`;
- `CHAT_HISTORY`, `MOVIE_VIEW_LOG`, and `MOVIE_DAILY_VIEW`.

## 7. Implemented Manager functions and locations

### Cinema-scoped room/seat/showtime management

Manager shares the movie, room, seat, and showtime endpoints listed above,
but every cinema-owned resource must pass
`CinemaScopeAuthorizationService`.

Scope relationships:

```text
Manager USER
  -> active STAFF_PROFILE
  -> cinemaId
  -> ROOM
  -> SEAT / SHOWTIME
```

The request cannot bypass scope by submitting a different cinema or room.

### Cancel future showtime

```http
POST /api/manager/showtimes/{showtimeId}/cancel
```

Only future showtimes in the assigned cinema can be cancelled. The use case
updates showtime, seats, bookings, tickets, refunds, notifications, and audit
records transactionally, then performs email/external work after commit.

### Read refund status

```http
GET /api/manager/refunds
```

Manager can filter by status, showtime, and date but only within the assigned
cinema. The response contains masked account data. It does not expose the
decrypted bank account.

### Explicit Manager restrictions

Manager cannot:

- call `/api/admin/refunds/*`;
- decrypt full bank details;
- assign or confirm manual refunds;
- manage users/roles;
- bypass `STAFF_PROFILE.cinemaId`;
- view a system-wide dashboard.

## 8. Use cases documented but not fully implemented

The SRS/API contract lists functions that are not present as complete
controller/service use cases in the current branch:

- Admin cinema CRUD;
- voucher CRUD;
- F&B CRUD;
- full user list, ban/unban, and role assignment;
- Manager branch dashboard/revenue endpoint;
- Admin system dashboard;
- complete ticket-scan controller flow.

Authorization policy constants exist for some of these, but a policy is not
an implementation. These gaps should not be reported to the frontend as
finished APIs.

## 9. Additional integration fixes

The merge exposed integration defects that were corrected:

- Admin movie create/update returned unloaded genre navigation data.
- Payment confirmation used a query shape that could hide a payment when an
  optional test navigation was absent.
- Admin review moderation attempted to store the non-user string
  `SYSTEM_AI` in a user foreign key.
- Tests were updated for normalized movie DTOs, new settings, Hangfire jobs,
  and changed Admin maintenance behavior.
- Integration tests now execute background jobs deterministically.
- Time-dependent tests now create future showtimes instead of relying on
  hard-coded dates.
- `Program.cs` no longer falls back to a hard-coded JWT secret; startup fails
  clearly when `JwtSettings:Secret` is missing.

## 10. Verification

Commands:

```powershell
dotnet build CinemaSystem.sln --no-restore
dotnet test CinemaSystem.sln --no-build --no-restore
```

Result:

```text
Build: succeeded
Tests: 225 passed, 0 failed, 0 skipped
Warnings: 3 existing nullable warnings in tests
```

Database validation was performed after applying both Admin patches.

## 11. Remaining operational notes

- `CinemaSystem/appsettings.Development.json` is ignored by Git, but the local
  copy is malformed JSON and contains plaintext local credentials/secrets.
  It was not modified or staged. Correct the JSON, rotate exposed
  credentials, and move secrets to user secrets or environment variables
  before running the API locally.
- `sprint-2-full-architecture.sql` and
  `sprint-2-review-and-views.sql` are historical, non-idempotent scripts.
  They were not executed.
- The current Admin room-deactivation flow marks open showtimes
  `SUSPENDED` for manual handling; it does not immediately create refunds.
  A separate explicit cancellation is still required for UC003.
- `docs/database/cinema-booking-schema.txt` is deleted by the Admin branch;
  `cinema-booking-schema.sql` is the maintained canonical reset script.
