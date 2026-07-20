# Counter F&B order: SQL retry transaction fix

**Date:** 2026-07-20
**Backend branch:** `main`
**Frontend branch checked:** `CinemaBookingSystem-FE` / `MobileVipPro14proMax`

## Reported symptom

The staff Counter F&B screen called `POST /api/fb-items/counter-orders` and received:

> The configured execution strategy `SqlServerRetryingExecutionStrategy` does not support user-initiated transactions.

## Root cause

`CinemaSystem.Infrastructure` configures SQL Server with `EnableRetryOnFailure`. That provider requires any explicit database transaction to be created *inside* the delegate executed by `DbContext.Database.CreateExecutionStrategy()`.

`FbItemService.CreateCounterOrderAsync` previously called `BeginTransactionAsync()` directly. On the first database command within that transaction, EF Core rejected the request to prevent a partially retried unit of work. The UI therefore received HTTP 500 before the counter sale could be completed.

The frontend was not the cause:

- `MobileVipPro14proMax` posts to `/api/fb-items/counter-orders`.
- The backend controller exposes the same route.
- The frontend API client attaches the Bearer token.

## Change made

`CreateCounterOrderAsync` now:

1. Creates the SQL Server execution strategy.
2. Runs the complete operation inside `ExecuteAsync`.
3. Creates the transaction inside that delegate.
4. Keeps inventory deduction, booking creation, `SaveChangesAsync`, and `CommitAsync` in the same retriable transaction.
5. Rolls back and rethrows unexpected exceptions so EF Core can retry transient SQL failures.
6. Logs the server exception and returns a safe generic `INTERNAL_ERROR` response instead of sending EF/SQL internals to the frontend.

The transaction is required: inventory must not be decremented unless the corresponding fulfilled `COUNTER` booking and its F&B line items are saved successfully.

## Deliberately not changed

No frontend route or payload was changed because the FE/BE route already matches.

`Booking.ShowtimeId` remains nullable in the EF model, which supports standalone counter F&B sales. However, the canonical reset script still declares `BOOKING.showtimeId` as `NOT NULL`. This pre-existing schema/model mismatch needs a separate business and migration decision before using that reset script for standalone F&B orders; it is not the cause of the retry-strategy error fixed here.

## Validation

Executed after this change:

```powershell
dotnet build CinemaSystem.sln --no-restore
```

The solution builds successfully with **0 errors**. It still reports 59 pre-existing nullable/obsolete warnings in unrelated modules.

The full suite was also started with:

```powershell
dotnet test CinemaSystem.sln --no-build --no-restore
```

It has two unrelated failures in `EmailSystemBusinessRulesTests`: both tests try to send a real email and the configured SMTP server rejected the send (`Failure sending mail`).

The existing integration-test host uses EF Core InMemory and cannot exercise the raw SQL inventory update or SQL Server retry strategy. A live SQL Server smoke test should submit one valid counter order and confirm exactly one new `BOOKING`, its `BOOKING_FB_ITEM` rows, and the expected inventory decrement.
