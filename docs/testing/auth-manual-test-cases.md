# Sprint 1 Auth Manual Test Cases

Use Swagger or Postman against the running API. Configure SQL Server, JWT, and Gmail SMTP before testing register with real email delivery.

## Flow A: Register + Verify + Login

### 1. Register
- Endpoint: `POST /api/auth/register`
- Body:
```json
{
  "email": "customer1@example.com",
  "password": "Password1",
  "fullName": "Customer One",
  "phoneNumber": "0900000001"
}
```
- Expected status: `201 Created`
- Expected response: `success = true`, no OTP returned.
- Expected DB state:
  - `USER.status = PENDING_VERIFICATION`
  - `USER.emailVerified = false`
  - `ROLE.roleName = Customer`
  - `CUSTOMER_PROFILE` row exists.
  - `EMAIL_VERIFICATION_TOKEN` row exists with unused token.

### 2. Verify Email
- Endpoint: `POST /api/auth/verify-email`
- Body:
```json
{
  "email": "customer1@example.com",
  "otp": "123456"
}
```
- Expected status: `200 OK`
- Expected response: `success = true`
- Expected DB state:
  - `USER.status = ACTIVE`
  - `USER.emailVerified = true`
  - latest `EMAIL_VERIFICATION_TOKEN.isUsed = true`
  - latest `EMAIL_VERIFICATION_TOKEN.verifiedAt` is not null.

### 3. Login
- Endpoint: `POST /api/auth/login`
- Body:
```json
{
  "email": "customer1@example.com",
  "password": "Password1"
}
```
- Expected status: `200 OK`
- Expected response data:
  - `accessToken`
  - `refreshToken`
  - `expiresAt`
  - `userId`
  - `email`
  - `fullName`
  - `role = Customer`
- Expected DB state:
  - `REFRESH_TOKEN` row exists and `isRevoked = false`.

## Flow B: Login Before Verify

### 1. Register New Account
- Endpoint: `POST /api/auth/register`
- Use a new email.
- Expected status: `201 Created`

### 2. Login Before OTP Verify
- Endpoint: `POST /api/auth/login`
- Expected status: `403 Forbidden`
- Expected response: `errorCode = EMAIL_NOT_VERIFIED`

## Flow C: Duplicate Register

### 1. Register Same Email Twice
- Endpoint: `POST /api/auth/register`
- Expected first status: `201 Created`
- Expected second status: `409 Conflict`
- Expected second response: `errorCode = DUPLICATE_EMAIL`

## Flow D: Refresh Token

### 1. Login
- Endpoint: `POST /api/auth/login`
- Save returned `refreshToken`.

### 2. Refresh
- Endpoint: `POST /api/auth/refresh-token`
- Body:
```json
{
  "refreshToken": "paste-refresh-token-here"
}
```
- Expected status: `200 OK`
- Expected response data:
  - new `accessToken`
  - new `refreshToken`
- Expected DB state:
  - old refresh token has `isRevoked = true`
  - new refresh token has `isRevoked = false`

## Flow E: Logout

### 1. Logout
- Endpoint: `POST /api/auth/logout`
- Body:
```json
{
  "refreshToken": "paste-refresh-token-here"
}
```
- Expected status: `200 OK`
- Expected DB state:
  - matching refresh token has `isRevoked = true`
  - `revokedAt` is not null.

### 2. Refresh After Logout
- Endpoint: `POST /api/auth/refresh-token`
- Expected status: `401 Unauthorized`
- Expected response: `errorCode = REFRESH_TOKEN_REVOKED`

## Flow F: Authorization

### 1. Customer Policy
- Login as a verified Customer.
- Add header: `Authorization: Bearer <accessToken>`
- Endpoint: `GET /api/auth-test/customer`
- Expected status: `200 OK`

### 2. Admin Policy
- Use the same Customer token.
- Endpoint: `GET /api/auth-test/admin`
- Expected status: `403 Forbidden`

## Resend Verification OTP

### Resend OTP
- Endpoint: `POST /api/auth/resend-verification-otp`
- Body:
```json
{
  "email": "customer1@example.com"
}
```
- Expected status: `200 OK` for an unverified user.
- Expected DB state:
  - previous unused OTP rows are marked used.
  - a new unused OTP row is created.
