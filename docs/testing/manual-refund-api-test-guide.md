# Manual Refund API Test Guide

## 1. Prepare database

For a disposable local database, run the canonical reset schema. It drops and
recreates `CinemaBookingDB`; do not run it against an existing database whose
data must be retained:

```powershell
sqlcmd -S YOUR_SERVER -U YOUR_USER -P "YOUR_PASSWORD" -b -f 65001 `
  -i "docs\database\cinema-booking-schema.sql"
```

For an existing database with data to keep, do not run the canonical script.
Use a data migration reviewed for that database's current state.

If Windows authentication is used:

```powershell
sqlcmd -S YOUR_SERVER -E -b -f 65001 `
  -i "docs\database\cinema-booking-schema.sql"
```

Confirm:

```sql
SELECT name
FROM sys.tables
WHERE name IN
(
    'BANK_DIRECTORY',
    'REFUND_CLAIM',
    'REFUND_CLAIM_TOKEN',
    'CUSTOMER_REFUND_REQUEST',
    'MANUAL_REFUND_PROCESS',
    'REFUND_CUSTOMER_CONFIRMATION'
);

SELECT bankCode, bankBin, shortName
FROM BANK_DIRECTORY
WHERE isActive = 1;
```

## 2. Start API

Configure:

```json
"RefundSettings": {
  "FrontendBaseUrl": "http://localhost:5173",
  "ClaimTokenMinutes": 5
}
```

Start:

```powershell
dotnet run --project CinemaSystem
```

Open Swagger at the URL printed by the application.

For development without SMTP, set:

```json
"EmailSettings": {
  "UseMock": true
}
```

The claim URL will be printed in the API terminal.

## 3. Prepare data

Required records:

- active Manager with active `STAFF_PROFILE.cinemaId`;
- Customer account and `CUSTOMER_PROFILE`;
- future showtime in the Manager's cinema;
- paid booking with a successful payment and unused ticket.

The showtime must satisfy:

```text
startTime > current UTC time
```

## 4. Manager cancels showtime

Login as Manager and call:

```http
POST /api/manager/showtimes/{showtimeId}/cancel
Authorization: Bearer <manager-token>
Content-Type: application/json

{
  "reason": "Projector failure"
}
```

Expected:

```text
SHOWTIME = CANCELLED
BOOKING = REFUND_PENDING
TICKET = CANCELLED
REFUND = PENDING
REFUND_CLAIM = PENDING_INFO
```

Copy the `t=` token from the Customer email or mock-email terminal output.

## 5. Customer resolves claim

Login as the Customer who owns the booking:

```http
POST /api/customer/refund-claims/resolve
Authorization: Bearer <customer-token>
Content-Type: application/json

{
  "token": "<token-from-email>"
}
```

Save the returned `refundClaimId`.

Negative checks:

- another Customer receives `403 REFUND_CLAIM_FORBIDDEN`;
- token after five minutes receives `410 REFUND_CLAIM_EXPIRED`;
- replaying a used token receives `409 REFUND_CLAIM_TOKEN_USED`.

## 6. Customer reviews banks and saves account

```http
GET /api/customer/banks
Authorization: Bearer <customer-token>
```

```http
PUT /api/customer/refund-claims/{claimId}/bank-account
Authorization: Bearer <customer-token>
Content-Type: application/json

{
  "bankCode": "VCB",
  "accountNumber": "0123456789",
  "accountHolderName": "NGUYEN VAN A"
}
```

Expected response:

```json
{
  "maskedAccountNumber": "******6789",
  "accountHolderName": "NGUYEN VAN A"
}
```

The database must not contain the plaintext account number:

```sql
SELECT bankAccountEncrypted, bankAccountLast4, accountHolderNameEncrypted
FROM REFUND_CLAIM
WHERE refundClaimId = '...';
```

## 7. Customer submits

```http
POST /api/customer/refund-claims/{claimId}/submit
Authorization: Bearer <customer-token>
```

Expected:

```text
REFUND = MANUAL_REQUIRED
REFUND_CLAIM = MANUAL_REQUIRED
MANUAL_REFUND_PROCESS = OPEN
```

Trying to change the bank account afterward must return:

```text
409 REFUND_CLAIM_NOT_EDITABLE
```

## 8. Manager checks status

```http
GET /api/manager/refunds?status=MANUAL_REQUIRED
Authorization: Bearer <manager-token>
```

Manager sees:

- refund amount and operational data;
- `workflowStatus = MANUAL_REQUIRED`;
- bank code;
- masked account number only.

Calling `/api/admin/refunds/manual` with the Manager token must return `403`.

## 9. Admin processes transfer

Login as Admin:

```http
GET /api/admin/refunds/manual
Authorization: Bearer <admin-token>
```

Admin verifies the full destination account, then performs the transfer manually in the
bank portal.

Assign the case:

```http
POST /api/admin/refunds/{refundId}/assign
Authorization: Bearer <admin-token>
```

Confirm only after the bank portal shows a successful transfer:

```http
POST /api/admin/refunds/{refundId}/manual-confirm
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "bankTransactionCode": "VCB-REFUND-20260625-0001",
  "transferredAmount": 100000,
  "proofUrl": "https://secure-storage.example/refunds/0001",
  "note": "Verified in bank portal"
}
```

Expected:

```text
REFUND = SUCCESS
BOOKING = REFUNDED
TICKET = REFUNDED
REFUND_CLAIM = COMPLETED
MANUAL_REFUND_PROCESS = CONFIRMED
```

The Customer receives success notification/email after commit.

## 10. Required negative tests

- wrong amount -> `400 REFUND_AMOUNT_MISMATCH`;
- non-HTTPS proof -> `400 INVALID_REFUND_PROOF_URL`;
- duplicate transaction code -> `409 REFUND_TRANSACTION_CODE_DUPLICATE`;
- confirm before assignment -> `409 MANUAL_REFUND_NOT_ASSIGNED_TO_USER`;
- second Admin assigns an owned case -> `409 MANUAL_REFUND_ALREADY_ASSIGNED`;
- repeated successful confirmation -> HTTP 200 with `alreadyProcessed = true`;
- Customer/Staff/Manager calling Admin endpoints -> 403.

## 11. Automated verification

```powershell
dotnet build CinemaSystem.sln --no-restore -p:BaseOutputPath=build-check\
dotnet test CinemaSystem.sln --no-restore -p:BaseOutputPath=build-check\
```

Expected current result:

```text
257 passed, 0 failed
```
