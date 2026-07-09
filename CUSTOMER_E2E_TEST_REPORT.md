# CUSTOMER E2E TEST REPORT

## 1. Environment

* Branch: `Tom/integration-main-manager-admin-test`
* BE URL: `http://localhost:5070`
* FE URL: `http://127.0.0.1:5173`
* DB connection used: SQL Server `DESKTOP-VM1KTEM`, database `CinemaBookingDB`, Windows Authentication (`sqlcmd -E`). Không ghi connection string/secrets vào report.
* Backend startup project: `CinemaSystem/CinemaSystem.csproj`
* Backend health/API check:
  * `http://localhost:5070/swagger/index.html` trả `200`
  * `/health` không có endpoint, trả `404`
* Frontend check:
  * `http://127.0.0.1:5173/login` trả `200`
  * FE dùng `VITE_API_BASE_URL=http://localhost:5070`
* Dependency install: Không chạy install mới; FE dependency đã có sẵn.
* Commit/push: Không commit, không push.
* Manager/Admin testing: Không chạy trong Task 2.

## 2. Account Preparation

* Email: `tommy090305@gmail.com`
* Existing user found before registration: Không.
* Cleanup queries used: Chỉ chạy SELECT kiểm tra. Không chạy DELETE vì email chưa tồn tại trước bước register.
* Result: Email đủ điều kiện để đăng ký mới.

SQL kiểm tra trước khi đăng ký:

```sql
SET NOCOUNT ON;
DECLARE @Email nvarchar(255)=N'tommy090305@gmail.com';
DECLARE @UserId nvarchar(50)=(SELECT userId FROM dbo.[USER] WHERE email=@Email);
DECLARE @CustomerProfileId nvarchar(50)=(SELECT customerProfileId FROM dbo.CUSTOMER_PROFILE WHERE userId=@UserId);

SELECT COUNT(*) AS userCount,
       MAX(userId) AS userId,
       MAX(status) AS userStatus,
       MAX(CONVERT(int,emailVerified)) AS emailVerified
FROM dbo.[USER]
WHERE email=@Email;

SELECT
  (SELECT COUNT(*) FROM dbo.CUSTOMER_PROFILE WHERE userId=@UserId) AS profileCount,
  (SELECT COUNT(*) FROM dbo.EMAIL_VERIFICATION_TOKEN WHERE userId=@UserId) AS verificationTokenCount,
  (SELECT COUNT(*) FROM dbo.REFRESH_TOKEN WHERE userId=@UserId) AS refreshTokenCount,
  (SELECT COUNT(*) FROM dbo.BOOKING WHERE customerProfileId=@CustomerProfileId) AS bookingCount,
  (SELECT COUNT(*) FROM dbo.REVIEW WHERE customerProfileId=@CustomerProfileId) AS reviewCount,
  (SELECT COUNT(*) FROM dbo.REFUND_CLAIM WHERE customerProfileId=@CustomerProfileId) AS refundClaimCount,
  (SELECT COUNT(*) FROM dbo.REWARD_POINT_TRANSACTION WHERE customerProfileId=@CustomerProfileId) AS rewardTransactionCount,
  (SELECT COUNT(*) FROM dbo.VOUCHER_USAGE WHERE customerProfileId=@CustomerProfileId) AS voucherUsageCount;
```

Kết quả:

* `userCount = 0`
* `profileCount = 0`
* `verificationTokenCount = 0`
* `refreshTokenCount = 0`
* `bookingCount = 0`
* `reviewCount = 0`
* `refundClaimCount = 0`
* `rewardTransactionCount = 0`
* `voucherUsageCount = 0`

## 3. Register Test

* Status: Pass.
* Flow:
  * Mở trang login/register từ FE.
  * Chuyển sang form đăng ký.
  * Đăng ký customer bằng email `tommy090305@gmail.com`.
  * CAPTCHA được xử lý trong phạm vi user đã cấp quyền cho Task 2.
  * OTP được lấy từ Gmail đã được user cấp quyền. Không ghi OTP vào report.
  * FE hiển thị thông báo đăng ký thành công và sau đó xác thực email thành công.
* Screenshots:
  * `test-artifacts/customer-e2e/01-login-page.png`
  * `test-artifacts/customer-e2e/02-register-page.png`
  * `test-artifacts/customer-e2e/03-otp-page.png`
  * `test-artifacts/customer-e2e/04-register-success.png`
* API errors: Không thấy lỗi API blocking trong bước register.
* UI errors: Không thấy lỗi UI blocking trong bước register.

## 4. Login Test

* Status: Pass.
* Flow:
  * Login bằng customer vừa đăng ký.
  * CAPTCHA được xử lý trong phạm vi user đã cấp quyền cho Task 2.
  * Login thành công, FE hiển thị homepage với greeting `Chào, Tommy E2E Test`.
* Screenshots:
  * `test-artifacts/customer-e2e/01-login-page.png`
  * `test-artifacts/customer-e2e/05-login-success-home.png`
* API errors: Không thấy lỗi API blocking trong bước login.
* UI errors:
  * Console warning không blocking:

```text
[GSI_LOGGER]: google.accounts.id.initialize() is called multiple times. This could cause unexpected behavior and only the last initialized instance will be used.
```

## 5. Booking Test

* Status: Pass sau targeted DB update do môi trường test không có webhook thanh toán thật.
* Movie: `Doraemon Movie 2026`
* Showtime:
  * FE modal chọn suất chiếu: `01/07/2026 19:00`
  * DB: `SHOWTIME.showtimeId = SHW004`, `startTime = 2026-07-01 19:00:00`, `status = OPEN`
* Seats: `B1`, VIP, `90.000đ`
* Combos/Food:
  * FE chọn: `Beta Combo 69oz`, quantity `1`, `75.000đ`
  * Sau khi tạo booking, item hiển thị: `1x Combo bap ngot va Pepsi lon`, `75.000 đ`
* Booking ID: `BOK_d71e512806c64880974a57ed93fd7427`
* Payment ID: `PAY_f7bbea4569c34a6189288a9eec38458d`
* Transaction code: `TBUWU2HSVZH`
* Total: `165.000đ`
* Payment/Booking status before DB update:
  * `BOOKING.bookingStatus = PENDING_PAYMENT`
  * `PAYMENT.paymentStatus = PENDING`
  * `SHOWTIME_SEAT.seatStatus = LOCKED`
  * Chưa có `TICKET`
* Payment sandbox/webhook blocker:
  * FE hiển thị lỗi sau khi bấm kiểm tra thanh toán:

```text
Hệ thống chưa nhận được webhook thanh toán. Vui lòng chờ thêm vài giây rồi kiểm tra lại.
```

* Targeted DB update query used:

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @Email nvarchar(255)=N'tommy090305@gmail.com';
DECLARE @BookingId nvarchar(50)=N'BOK_d71e512806c64880974a57ed93fd7427';
DECLARE @Now datetime2=SYSUTCDATETIME();

BEGIN TRANSACTION;

IF (
    SELECT COUNT(*)
    FROM dbo.BOOKING b
    JOIN dbo.CUSTOMER_PROFILE cp ON cp.customerProfileId=b.customerProfileId
    JOIN dbo.[USER] u ON u.userId=cp.userId
    WHERE u.email=@Email
      AND b.bookingId=@BookingId
      AND b.bookingStatus=N'PENDING_PAYMENT'
)<>1 THROW 51000,'Target booking guard failed',1;

IF (
    SELECT COUNT(*)
    FROM dbo.PAYMENT p
    WHERE p.bookingId=@BookingId
      AND p.paymentStatus=N'PENDING'
)<>1 THROW 51001,'Target payment guard failed',1;

IF EXISTS(
    SELECT 1
    FROM dbo.BOOKING b
    JOIN dbo.SHOWTIME s ON s.showtimeId=b.showtimeId
    WHERE b.bookingId=@BookingId
      AND s.status=N'CANCELLED'
) THROW 51002,'Target showtime is cancelled',1;

UPDATE p
SET paymentStatus=N'SUCCESS',
    paidAt=@Now,
    updatedAt=@Now
FROM dbo.PAYMENT p
WHERE p.bookingId=@BookingId
  AND p.paymentStatus=N'PENDING';
DECLARE @PaymentsUpdated int=@@ROWCOUNT;

UPDATE b
SET bookingStatus=N'PAID'
FROM dbo.BOOKING b
JOIN dbo.CUSTOMER_PROFILE cp ON cp.customerProfileId=b.customerProfileId
JOIN dbo.[USER] u ON u.userId=cp.userId
WHERE u.email=@Email
  AND b.bookingId=@BookingId
  AND b.bookingStatus=N'PENDING_PAYMENT';
DECLARE @BookingsUpdated int=@@ROWCOUNT;

UPDATE ss
SET seatStatus=N'BOOKED',
    lockedUntil=NULL,
    lockedByUserId=NULL
FROM dbo.SHOWTIME_SEAT ss
JOIN dbo.BOOKING_SEAT bs ON bs.showtimeSeatId=ss.showtimeSeatId
JOIN dbo.BOOKING b ON b.bookingId=bs.bookingId
JOIN dbo.CUSTOMER_PROFILE cp ON cp.customerProfileId=b.customerProfileId
JOIN dbo.[USER] u ON u.userId=cp.userId
WHERE u.email=@Email
  AND b.bookingId=@BookingId;
DECLARE @SeatsUpdated int=@@ROWCOUNT;

INSERT INTO dbo.TICKET(ticketId,bookingSeatId,qrCode,ticketStatus,generatedAt)
SELECT N'TCK_'+LOWER(REPLACE(CONVERT(nvarchar(36),NEWID()),N'-',N'')),
       bs.bookingSeatId,
       N'G2C|'+b.bookingId+N'|'+bs.bookingSeatId+N'|'+LOWER(REPLACE(CONVERT(nvarchar(36),NEWID()),N'-',N'')),
       N'UNUSED',
       @Now
FROM dbo.BOOKING_SEAT bs
JOIN dbo.BOOKING b ON b.bookingId=bs.bookingId
JOIN dbo.CUSTOMER_PROFILE cp ON cp.customerProfileId=b.customerProfileId
JOIN dbo.[USER] u ON u.userId=cp.userId
LEFT JOIN dbo.TICKET t ON t.bookingSeatId=bs.bookingSeatId
WHERE u.email=@Email
  AND b.bookingId=@BookingId
  AND t.ticketId IS NULL;
DECLARE @TicketsInserted int=@@ROWCOUNT;

IF @PaymentsUpdated<>1
   OR @BookingsUpdated<>1
   OR @SeatsUpdated<1
   OR @TicketsInserted<1
   THROW 51003,'Target update affected unexpected rows',1;

COMMIT TRANSACTION;

SELECT @BookingsUpdated AS bookingsUpdated,
       @PaymentsUpdated AS paymentsUpdated,
       @SeatsUpdated AS seatsUpdated,
       @TicketsInserted AS ticketsInserted;
```

Kết quả targeted update:

* `bookingsUpdated = 1`
* `paymentsUpdated = 1`
* `seatsUpdated = 1`
* `ticketsInserted = 1`

Final DB verification query:

```sql
SET NOCOUNT ON;
DECLARE @Email nvarchar(255)=N'tommy090305@gmail.com';
DECLARE @BookingId nvarchar(50)=N'BOK_d71e512806c64880974a57ed93fd7427';

SELECT u.email,
       u.status AS userStatus,
       u.emailVerified,
       b.bookingId,
       b.bookingStatus,
       b.totalAmount,
       p.paymentId,
       p.paymentStatus,
       s.showtimeId,
       s.startTime,
       s.status AS showtimeStatus,
       seat.seatCode,
       ss.seatStatus,
       COUNT(t.ticketId) AS ticketCount,
       MAX(t.ticketStatus) AS ticketStatus
FROM dbo.[USER] u
JOIN dbo.CUSTOMER_PROFILE cp ON cp.userId=u.userId
JOIN dbo.BOOKING b ON b.customerProfileId=cp.customerProfileId
JOIN dbo.PAYMENT p ON p.bookingId=b.bookingId
JOIN dbo.SHOWTIME s ON s.showtimeId=b.showtimeId
JOIN dbo.BOOKING_SEAT bs ON bs.bookingId=b.bookingId
JOIN dbo.SHOWTIME_SEAT ss ON ss.showtimeSeatId=bs.showtimeSeatId
JOIN dbo.SEAT seat ON seat.seatId=ss.seatId
LEFT JOIN dbo.TICKET t ON t.bookingSeatId=bs.bookingSeatId
WHERE u.email=@Email
  AND b.bookingId=@BookingId
GROUP BY u.email,
         u.status,
         u.emailVerified,
         b.bookingId,
         b.bookingStatus,
         b.totalAmount,
         p.paymentId,
         p.paymentStatus,
         s.showtimeId,
         s.startTime,
         s.status,
         seat.seatCode,
         ss.seatStatus;
```

Final DB verification result:

* `USER.status = ACTIVE`
* `USER.emailVerified = 1`
* `BOOKING.bookingStatus = PAID`
* `BOOKING.totalAmount = 165000.00`
* `PAYMENT.paymentStatus = SUCCESS`
* `SHOWTIME.showtimeId = SHW004`
* `SHOWTIME.startTime = 2026-07-01 19:00:00`
* `SHOWTIME.status = OPEN`
* `SEAT.seatCode = B1`
* `SHOWTIME_SEAT.seatStatus = BOOKED`
* `ticketCount = 1`
* `ticketStatus = UNUSED`

FE sau targeted update:

* Trang success hiển thị `Thanh toán thành công`.
* Booking hiển thị trạng thái `PAID`.
* Vé QR cho seat `B1` hiển thị được.
* Trang `Vé của tôi` hiển thị:
  * Tổng số vé: `1`
  * Đã thanh toán: `1`
  * Tổng chi tiêu: `165.000 đ`

Screenshots:

* `test-artifacts/customer-e2e/06-movie-showtime-selection.png`
* `test-artifacts/customer-e2e/07-seat-selection-b1.png`
* `test-artifacts/customer-e2e/08-combo-selection.png`
* `test-artifacts/customer-e2e/09-payment-pending.png`
* `test-artifacts/customer-e2e/10-payment-webhook-missing.png`
* `test-artifacts/customer-e2e/11-payment-success-ticket-viewport.png`
* `test-artifacts/customer-e2e/12-my-bookings-paid-viewport.png`
* `test-artifacts/customer-e2e/13-current-booking-success-viewport.png`

## 6. Bugs Found

### Bug ID: CUST-E2E-001

* Component: FE
* Description: Lệch hiển thị thời gian suất chiếu giữa modal chọn suất chiếu và checkout/success/my-bookings.
* Steps to reproduce:
  1. Login customer.
  2. Chọn `Doraemon Movie 2026`.
  3. Chọn suất `SHW004`.
  4. So sánh thời gian ở modal chọn suất với checkout/success/my-bookings.
* Expected result: FE hiển thị nhất quán theo DB hoặc theo timezone được định nghĩa rõ.
* Actual result:
  * Modal chọn suất hiển thị `01/07/2026 19:00`.
  * Checkout/success/my-bookings hiển thị dạng `02:00 2/7/26`.
  * DB xác nhận `SHOWTIME.startTime = 2026-07-01 19:00:00`.
* Severity: Major
* Status: Not fixed trong Task 2; nằm ngoài phạm vi fix bắt buộc của luồng customer E2E.

### Bug ID: CUST-E2E-002

* Component: FE
* Description: Checkout contact section hiển thị số điện thoại là `Đang cập nhật` dù DB user có `phoneNumber`.
* Steps to reproduce:
  1. Login customer vừa đăng ký.
  2. Tạo booking đến trang checkout.
  3. Kiểm tra phần thông tin liên hệ.
* Expected result: Hiển thị đúng phone number của user hoặc lý do rõ ràng nếu profile chưa đồng bộ.
* Actual result: FE hiển thị `Đang cập nhật`.
* Severity: Minor
* Status: Not fixed trong Task 2.

### Bug ID: CUST-E2E-003

* Component: FE
* Description: Console warning Google Identity Services bị initialize nhiều lần.
* Steps to reproduce:
  1. Mở/login FE.
  2. Quan sát browser console.
* Expected result: Google Identity Services chỉ initialize một lần.
* Actual result:

```text
[GSI_LOGGER]: google.accounts.id.initialize() is called multiple times. This could cause unexpected behavior and only the last initialized instance will be used.
```

* Severity: Minor
* Status: Not fixed trong Task 2.

### Environment note: Payment webhook unavailable

* Component: Test environment / Payment callback
* Description: Sau khi tạo payment QR, FE không nhận được webhook thanh toán trong môi trường test.
* Expected result: Nếu sandbox callback được cấu hình, payment tự chuyển sang success.
* Actual result: FE báo chưa nhận được webhook.
* Severity: Không phân loại là product bug trong Task 2; đây là blocker môi trường nên đã dùng targeted DB update theo yêu cầu.

## 7. Fixes Applied

* Source code fixes: Không sửa source code trong Task 2.
* FE config fixes: Không cần sửa; `VITE_API_BASE_URL` đã trỏ đúng `http://localhost:5070`.
* BE config/build fixes: Không cần sửa; BE chạy được và Swagger trả `200`.
* DB test operation:
  * Chỉ update booking/payment/seat/ticket liên quan trực tiếp tới `tommy090305@gmail.com` và `BOK_d71e512806c64880974a57ed93fd7427`.
  * Có guard theo exact email, booking ID, booking status, payment status.
  * Không update booking/movie/showtime/seat unrelated.

