# SCRUM-193 - Manual Refund Workflow

## Decision

Automatic payout is disabled because the current SePay integration does not provide a
documented payout or recipient account-verification API.

The implemented workflow is:

```text
Manager cancels future showtime
  -> REFUND.PENDING
  -> Customer receives five-minute claim link
  -> Customer submits bank information
  -> REFUND.MANUAL_REQUIRED
  -> Admin assigns the case and transfers money outside the system
  -> Admin confirms transaction code, exact amount and HTTPS proof
  -> REFUND.SUCCESS
  -> BOOKING/TICKET.REFUNDED
```

Manager cannot view full bank-account data and cannot complete a refund.

## Architecture

### API

- `ShowtimeCancellationsController`: cancellation only.
- `ManagerRefundsController`: cinema-scoped read model with masked account number.
- `CustomerRefundClaimsController`: bank directory, resolve, draft, submit and link reissue.
- `AdminRefundsController`: manual queue, assignment and confirmation.

### Application

- `IRefundClaimService`
- `IManualRefundService`
- `IRefundClaimIssuer`
- `ISensitiveDataProtector`

### Infrastructure

- `RefundClaimService`
- `ManualRefundService`
- `RefundClaimIssuer`
- `SensitiveDataProtector`

Controllers contain no EF Core or state-transition logic.

## Cancellation transaction

`POST /api/manager/showtimes/{showtimeId}/cancel`

The transaction:

1. Rechecks `showtime.startTime > UTC now`.
2. Sets showtime to `CANCELLED`.
3. Sets showtime seats to `UNAVAILABLE`.
4. Cancels unpaid bookings and pending payments.
5. Moves paid bookings to `REFUND_PENDING`.
6. Cancels paid tickets.
7. Creates `REFUND.PENDING`.
8. Creates `REFUND_CLAIM.PENDING_INFO`.
9. Creates a five-minute token containing only a SHA-256 hash in the database.
10. Creates notification and cancellation audit records.

SMTP runs after commit. No payment gateway is called.

Late successful payments on a cancelled booking create the same pending refund and
claim flow.

## Customer workflow

### Resolve link

```http
POST /api/customer/refund-claims/resolve
```

The authenticated customer must own the booking. Expired tokens return
`REFUND_CLAIM_EXPIRED`; used or revoked tokens cannot be replayed.

### Save draft

```http
PUT /api/customer/refund-claims/{claimId}/bank-account
```

Validation:

- active `bankCode` from `BANK_DIRECTORY`;
- account number contains 6-20 digits;
- holder name contains 2-255 characters.

The account number and holder name are encrypted with ASP.NET Data Protection. The API
returns only a masked account number for review.

Because the provider cannot verify account existence, the holder name is customer
declared. Admin must verify the destination in the bank portal before transferring.

### Submit

```http
POST /api/customer/refund-claims/{claimId}/submit
```

Submission is idempotent and locks bank-information editing:

```text
REFUND = MANUAL_REQUIRED
REFUND_CLAIM = MANUAL_REQUIRED
MANUAL_REFUND_PROCESS = OPEN
```

### Reissue expired link

```http
POST /api/customer/refund-requests
```

The backend verifies booking ownership and optional ticket ownership, revokes previous
active tokens, stores the request reason and sends a new five-minute link.

## Admin workflow

### Queue

```http
GET /api/admin/refunds/manual
```

Only Admin can see decrypted bank information.

### Assignment

```http
POST /api/admin/refunds/{refundId}/assign
```

Assignment uses a serializable transaction so two administrators cannot claim the same
case concurrently.

### Confirmation

```http
POST /api/admin/refunds/{refundId}/manual-confirm
```

Required:

- case assigned to the current Admin;
- exact transferred amount;
- globally unique bank transaction code;
- absolute HTTPS proof URL;
- refund still `MANUAL_REQUIRED`;
- total successful refunds do not exceed the original payment.

One database transaction updates:

```text
MANUAL_REFUND_PROCESS = CONFIRMED
REFUND = SUCCESS
REFUND_CLAIM = COMPLETED
BOOKING = REFUNDED
TICKET = REFUNDED
REWARD POINTS = REVERTED
NOTIFICATION + AUDIT
```

Success email is sent only after commit. Repeated confirmation is idempotent.

## Manager boundary

Manager may:

- cancel a future showtime in the assigned cinema;
- see refund and claim workflow status;
- see bank code and masked account number;
- filter refund records.

Manager may not:

- access Admin refund APIs;
- view a full bank account;
- assign a manual refund;
- enter transaction code/proof;
- mark a refund successful;
- access another cinema.

## Database

Mapped tables:

- `BANK_DIRECTORY`
- `REFUND_CLAIM`
- `REFUND_CLAIM_TOKEN`
- `CUSTOMER_REFUND_REQUEST`
- `MANUAL_REFUND_PROCESS`

The canonical reset schema and the idempotent existing-database patch are:

- `docs/database/cinema-booking-schema.sql`
- `docs/database/customer-assisted-refund-patch.txt`

## Verification

Automated integration coverage includes:

- future/started-showtime cancellation;
- cinema scope;
- pending refund and claim creation;
- five-minute claim email;
- bank-data encryption and masked Manager response;
- Customer submission;
- Manager rejection from Admin APIs;
- Admin full-data queue;
- assignment;
- exact manual confirmation;
- idempotent repeated confirmation;
- atomic Refund/Booking/Ticket completion;
- late successful payment protection.

Latest full result:

```text
257 passed, 0 failed
```

Existing-database SQL patch execution was verified against the configured local
database:

```text
DB_PATCH_APPLIED=1
ADDED_TABLES_FOUND=6/6
ACTIVE_BANKS=5
MANUAL_REFUND_PROCESS_EXISTS=1
```
