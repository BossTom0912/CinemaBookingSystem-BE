# Forgot Password Bug Fix Test

Date: 2026-06-05

## Bug

`POST /api/auth/forgot-password` returned `200 OK` for an email that did not exist in the database.
The backend did not generate an OTP, did not save a token, and did not send email in that branch, but the frontend could still route users to the OTP input screen.

## Fix

- Unknown email now returns `404 Not Found` with `USER_NOT_FOUND`.
- Unverified accounts still return `403 Forbidden` with `EMAIL_NOT_VERIFIED`.
- Password reset OTP generation still uses `EMAIL_VERIFICATION_TOKEN.purpose = PASSWORD_RESET`.
- Registration/email verification OTPs remain separate with `purpose = EMAIL_VERIFICATION`.
- Registering again with an email that is still `PENDING_VERIFICATION` now resends a verification OTP instead of returning `DUPLICATE_EMAIL`.
- Registering again with an already active email still returns `409 Conflict` with `DUPLICATE_EMAIL`.

## Test Cases

1. Unknown email:
   - Request forgot password with an email not in DB.
   - Expected: `success=false`, HTTP `404`, `errorCode=USER_NOT_FOUND`.
   - Expected: no email sent, no OTP token stored.

2. Unverified email:
   - Register account but do not verify email.
   - Request forgot password.
   - Expected: `success=false`, HTTP `403`, `errorCode=EMAIL_NOT_VERIFIED`.
   - Expected: no `PASSWORD_RESET` token created.

3. Verified active email:
   - Register and verify account.
   - Request forgot password.
   - Expected: `success=true`, HTTP `200`.
   - Expected: password reset email sent and one unused `PASSWORD_RESET` token stored.

4. Register retry for pending email:
   - Register an account and leave it unverified.
   - Register again with the same email.
   - Expected: `success=true`, HTTP `200`.
   - Expected: no extra user/profile created.
   - Expected: old `EMAIL_VERIFICATION` OTP is marked used, new `EMAIL_VERIFICATION` OTP is sent and stored.

5. Register retry for active email:
   - Register and verify an account.
   - Register again with the same email.
   - Expected: `success=false`, HTTP `409`, `errorCode=DUPLICATE_EMAIL`.

## Verification

- `dotnet build -p:BaseOutputPath=.codex_tmp\build\`: passed with 0 warnings and 0 errors.
- `dotnet test -p:BaseOutputPath=.codex_tmp\test\`: passed, 31/31 tests.

Note: regular `dotnet build` could not overwrite the default API output folder because a running `CinemaSystem` process was locking DLL files.
