# CinemaSystem Code Review Checklist

## Purpose

This checklist is used for reviewing CinemaSystem backend changes.

The goal is to catch real production risks, not to give generic comments.

Focus on:

- Correctness.
- Security.
- Authorization.
- Cinema scope.
- Clean Architecture.
- EF Core correctness.
- Database consistency.
- Payment safety.
- Seat-locking safety.
- API contract consistency.
- Missing tests.

## Review Output Format

Every review must use this structure:

```md
### Summary

State whether the code is safe to merge.

### Critical Issues

List issues that can break production, security, authorization, payment, seat locking, or data consistency.

For each issue:

- File:
- Problem:
- Impact:
- Suggested Fix:

### Major Issues

List important architecture, maintainability, business rule, or testing problems.

For each issue:

- File:
- Problem:
- Impact:
- Suggested Fix:

### Minor Issues

List naming, formatting, small cleanup, or readability problems.

### Missing Tests

List tests that should be added.

### Final Verdict

Use one:

- APPROVE
- APPROVE WITH MINOR COMMENTS
- REQUEST CHANGES
```

Do not skip the final verdict.

Do not modify code during review unless explicitly asked.

## Severity Rules

### Critical Issues

Mark as Critical when the issue can cause:

- Unauthorized access.
- Manager/Staff accessing another cinema.
- Customer accessing another customer's data.
- Secret exposure.
- Plain text password/OTP/token exposure.
- Payment marked successful without trusted verification.
- Duplicate payment processing.
- Seat double-locking or double-booking.
- Database corruption.
- Broken login/register/logout flow.
- Production build failure.
- Data loss.

### Major Issues

Mark as Major when the issue causes:

- Clean Architecture violation.
- Business rule mismatch.
- Missing validation.
- Inconsistent API response.
- Weak EF Core query.
- N+1 query risk.
- Missing transaction boundary.
- Missing important test.
- Hardcoded role/status logic.
- Poor maintainability that will likely cause bugs.

### Minor Issues

Mark as Minor when the issue is mostly:

- Naming.
- Formatting.
- Small duplication.
- Small readability improvement.
- Non-blocking cleanup.
- Comment cleanup.

## Clean Architecture Checklist

Check:

- Controller is thin.
- Controller delegates to Application abstraction/service.
- Controller does not contain heavy business logic.
- Controller does not perform complex EF Core workflow.
- Application layer coordinates use cases.
- Infrastructure implements technical details.
- Domain does not depend on EF Core, HTTP, JWT, SMTP, or SePay.
- Contracts contain DTOs, not business logic.
- No circular project reference.
- No unnecessary project/namespace rename.
- No unrelated architecture rewrite.

Red flags:

- `DbContext` injected directly into a controller for business flow.
- Payment logic inside controller.
- Seat-locking logic inside controller.
- JWT signing logic outside auth infrastructure/service.
- SMTP logic inside controller.
- EF entity returned directly from API in a new endpoint.
- Application layer directly using ASP.NET Core HTTP types without existing pattern.

## Authorization Checklist

Check:

- Endpoint has correct `[Authorize]` usage.
- Endpoint uses policy when policy exists.
- Anonymous endpoints are intentionally anonymous.
- Public registration creates only Customer accounts.
- Staff/Admin/Manager creation is not exposed publicly.
- Login rejects unverified users.
- Login rejects inactive/banned users.
- Logout revokes refresh token.
- Refresh token handling follows existing project pattern.
- Role claim value matches policy role value.
- No incorrect role casing issue.
- No accidental use of roleId when policy expects roleName, or the opposite.
- No authorization bypass through request body fields.

Role consistency must be verified from code and database assumptions.

Do not assume role values.

Check whether JWT uses:

- `Customer`, `Staff`, `Manager`, `Admin`
- `CUSTOMER`, `STAFF`, `MANAGER`, `ADMIN`
- `ROLE_CUSTOMER`, `ROLE_STAFF`, `ROLE_MANAGER`, `ROLE_ADMIN`

## Cinema Scope Checklist

Check:

- Customer only accesses their own data.
- Staff only operates within assigned cinema.
- Manager only manages assigned cinema.
- Admin global access is intentional.
- Staff/Manager scope is resolved from server-side `STAFF_PROFILE.cinemaId`.
- Request body `cinemaId` is not trusted as permission proof.
- Query filters include cinema scope where needed.
- Update/delete operations verify ownership/scope before mutation.

Red flags:

- Manager can pass any `cinemaId` in request.
- Staff can scan ticket from another cinema.
- Manager can update showtime in another cinema.
- Dashboard aggregates all cinemas for Manager.
- Scope check happens after data has already been returned.
- Only frontend hides buttons but backend has no scope check.

## Security Checklist

Check:

- No hardcoded secrets.
- No real credentials in committed config.
- No JWT secret in code.
- No SMTP password in code.
- No SePay secret in code.
- No connection string with real production credentials.
- Passwords are hashed.
- OTPs are not returned in API responses.
- OTPs are not logged.
- JWTs are not logged.
- Refresh tokens are not logged.
- Refresh tokens are stored securely according to existing pattern.
- Error responses do not leak stack traces.
- Sensitive provider data is not exposed.

Red flags:

- `Console.WriteLine(password)`
- Returning OTP in register/forgot password response.
- Returning refresh token hash.
- Logging raw Authorization header.
- Hardcoded `Admin@123` in application code.
- Hardcoded webhook secret.
- Weak JWT secret accepted silently in production-like config.

## EF Core and Database Checklist

Check:

- Async EF Core calls are used.
- `AsNoTracking()` is used for read-only queries where appropriate.
- Queries are filtered in SQL, not after loading full tables.
- No N+1 query risk.
- DTO projection is used where appropriate.
- `CancellationToken` is passed where project style supports it.
- `SaveChangesAsync` is not called repeatedly in a loop without reason.
- Database constraints are respected.
- Status values match DB check constraints.
- Money uses decimal.
- UTC time is used.
- Unique constraints are respected.
- Foreign keys are valid.
- Scaffolded models are not heavily edited.

Red flags:

- `.ToListAsync()` before applying important filters.
- Filtering by cinema/customer in memory.
- Using `double` or `float` for money.
- Comparing status to a typo string.
- Ignoring possible null from `FirstOrDefaultAsync`.
- Updating multiple related tables without transaction.
- Creating duplicate records where unique constraint exists.

## API Contract Checklist

Check:

- Request DTO has validation.
- Response uses consistent shape:
  `{ success, message, data, errorCode, errors }`
- Correct HTTP status codes are used.
- Swagger matches behavior.
- DTOs do not expose sensitive fields.
- DTOs do not expose password hash, OTP, refresh token hash, internal secrets.
- Validation errors help FE debug.
- Error codes/messages are consistent with existing style.

Red flags:

- Returning raw EF User including `passwordHash`.
- Returning internal exception message.
- Returning `200 OK` for validation failure if existing API does not follow that pattern.
- Returning plain string instead of standard API response.
- Swagger says one DTO but endpoint returns another shape.

## Authentication Review Checklist

Check:

- Register validates duplicate email.
- Register hashes password.
- Register creates only Customer profile for public registration.
- Register creates verification token securely.
- Register does not return OTP.
- Verify email checks token existence.
- Verify email checks token expiry.
- Verify email checks token not already used.
- Verify email marks user active/email verified.
- Login verifies password.
- Login rejects unverified/inactive/banned users.
- Login issues access token and refresh token.
- Refresh token is stored/revoked according to existing pattern.
- Logout revokes token.
- Token generation uses secure randomness.
- Timestamps use UTC.

## Seat Locking Review Checklist

Check:

- Seat availability is checked server-side.
- Expired locks are handled.
- Lock ownership is checked.
- A seat locked by another active user cannot be locked.
- A booked seat cannot be locked.
- Concurrent lock attempts are safe.
- Seat status transitions are explicit.
- Lock expiration time uses UTC.
- Unlock/release behavior is correct.
- Database update cannot silently override another user's lock.

Red flags:

- Only frontend prevents selecting locked seats.
- No check for `lockedUntil`.
- No check for `lockedByUserId`.
- No concurrency guard.
- Seat lock never expires.
- Expired lock still blocks forever.
- User can unlock another user's active lock.

## SePay Payment Review Checklist

Check:

- Webhook secret/signature is validated.
- Payment success is based on trusted provider callback/webhook, not client claim only.
- Amount is validated.
- Transaction reference is validated.
- Duplicate webhook is idempotent.
- Duplicate webhook does not duplicate records.
- Payment status transition is safe.
- Failed payment is handled safely.
- Sensitive payment data is not logged.
- Multi-table payment updates use transaction when needed.

Red flags:

- Client can call endpoint and mark payment successful.
- No webhook secret validation.
- No duplicate transaction handling.
- No amount check.
- Payment success updates only one table while related state remains inconsistent.
- Raw webhook secret logged.

## Showtime Review Checklist

Check:

- Manager/Admin policy is applied for management operations.
- Manager cinema scope is enforced.
- Showtime overlap is checked.
- Start/end time is valid.
- Room belongs to the cinema being managed.
- Movie/room/cinema status rules are respected where implemented.
- Status transitions are valid.
- UTC/local time handling is consistent with existing project.

Red flags:

- Manager can create showtime in another cinema.
- Two showtimes overlap in one room.
- Showtime end time is before start time.
- Showtime is opened for inactive room/cinema.
- Status typo violates DB constraint.

## Room and Seat Review Checklist

Check:

- Room belongs to correct cinema.
- Seat belongs to correct room.
- Seat code uniqueness is respected.
- Row/seat number uniqueness is respected.
- Manager/Admin scope is enforced where applicable.
- Deleting room/seat does not break existing showtime/booking assumptions.
- Soft disable is preferred when hard delete may break references.

Red flags:

- Creating duplicate seat code in same room.
- Manager updates room in another cinema.
- Hard deleting seats used by showtime seats.
- Capacity does not match seat count without clear reason.

## Missing Tests Checklist

For changed logic, suggest tests for:

- Happy path.
- Invalid input.
- Unauthorized access.
- Forbidden access.
- Customer ownership.
- Staff cinema scope.
- Manager cinema scope.
- Admin global access.
- Duplicate data conflict.
- Null/missing data.
- Token expiry.
- Refresh token revoke.
- Seat lock conflict.
- Expired seat lock.
- Payment webhook duplicate.
- Payment webhook invalid secret.
- EF query expected result.

If no test project exists, still list recommended tests.

Do not create tests during review-only task.

## Final Verdict Guidance

Use `APPROVE` only when:

- No critical issue.
- No major issue.
- Only no-op or clearly safe changes.
- Missing tests are not important for the changed scope.

Use `APPROVE WITH MINOR COMMENTS` when:

- No critical issue.
- No major issue.
- Only minor cleanup or low-risk missing tests.

Use `REQUEST CHANGES` when:

- Any critical issue exists.
- Any major issue exists.
- Build likely fails.
- Security or authorization is unsafe.
- Data consistency is unsafe.
- The change is outside requested scope.
