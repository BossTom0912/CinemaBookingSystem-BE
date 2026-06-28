# Chức năng đã triển khai và luồng xử lý giữa các class

## 1. Mục đích và phạm vi

Tài liệu này mô tả trạng thái thực tế của nhánh `main` tại commit nền
`a9fa818` (`gitlab/main`) vào ngày 2026-06-28. Mục tiêu là giúp cả team:

- biết API nào đã có và actor nào được gọi;
- lần theo luồng từ HTTP request đến controller, Application interface,
  Infrastructure service, database hoặc dịch vụ ngoài;
- hiểu luồng đăng nhập và phân quyền của Customer, Staff, Manager và Admin;
- đối chiếu code hiện có với use case trong SRS;
- không nhầm policy/entity đã khai báo với một use case đã triển khai hoàn chỉnh.

Nhánh `main` đang theo dõi `gitlab/main`. Tại thời điểm rà soát, `main` bằng
`gitlab/main` và đi trước `origin/main` 3 commit. Không có nội dung từ
`MangerAndAdmin_1` được merge ngược vào `main` trong lần cập nhật tài liệu này.

## 2. Tài liệu và nguồn code đã đối chiếu

Tài liệu chuẩn:

- `docs/requirements/srs-group-2.docx`
- `docs/requirements/business-rules.docx`
- `docs/architecture/backend-system-design-clean-architecture.docx`
- `docs/architecture/conceptual-erd-explanation.docx`
- `docs/api/api-contract-backend.docx`
- `docs/api/api-contract-movie-showtime.docx`
- `docs/database/cinema-booking-schema.sql`

Nguồn code được dùng để xác định trạng thái **đã triển khai**:

- `CinemaSystem/Controllers`
- `CinemaSystem/Program.cs`
- `CinemaSystem.Application/Interfaces`
- `CinemaSystem.Application/Common`
- `CinemaSystem.Infrastructure`
- `CinemaSystem.Domain/Entities`
- `CinemaSystem.Contracts`
- `CinemaSystem.Tests`

Quy ước đánh giá:

- **Đã có API**: có route trong controller và có service xử lý.
- **Đã có hạ tầng**: có entity, policy hoặc DbSet nhưng chưa có API use case hoàn chỉnh.
- **Chưa triển khai**: SRS có yêu cầu nhưng không tìm thấy controller/service tương ứng.

## 3. Luồng kiến trúc chung

```text
Client
  -> ASP.NET middleware trong CinemaSystem/Program.cs
  -> Controller trong CinemaSystem/Controllers
  -> Application interface trong CinemaSystem.Application/Interfaces
  -> Runtime implementation đăng ký tại
     CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs
  -> Infrastructure service
  -> CinemaDbContext / Redis / SMTP / Gemini / SePay
  -> ServiceResult hoặc DTO
  -> Controller bọc ApiResponse
  -> HTTP response
```

Vai trò từng project:

| Project | Trách nhiệm thực tế |
|---|---|
| `CinemaSystem` | Route, auth middleware, policy, controller, mapping response và hosted service |
| `CinemaSystem.Application` | Interface của use case, constant role/policy/status, `ServiceResult` |
| `CinemaSystem.Contracts` | Request/response DTO và `ApiResponse` |
| `CinemaSystem.Infrastructure` | EF Core, SQL Server, nghiệp vụ service hiện tại, JWT, hash, OTP, SMTP, Redis, SePay, Gemini |
| `CinemaSystem.Domain` | Entity được scaffold theo database |
| `CinemaSystem.Tests` | Unit test và integration test |

EF Core đang được dùng trực tiếp trong Infrastructure service. Dự án không có
lớp Repository riêng; `CinemaDbContext` đóng vai trò repository và unit of work.

## 4. Bảng ánh xạ DI: interface chạy sang class nào

Các ánh xạ runtime nằm trong
`CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`.

| Application interface | Runtime implementation | File triển khai |
|---|---|---|
| `IAuthService` | `AuthService` | `CinemaSystem.Infrastructure/Auth/AuthService.cs` |
| `IAdminService` | `AdminService` | `CinemaSystem.Infrastructure/Auth/AdminService.cs` |
| `ICustomerService` | `CustomerService` | `CinemaSystem.Infrastructure/Services/CustomerService.cs` |
| `ICinemaService` | `CinemaService` | `CinemaSystem.Infrastructure/Cinemas/CinemaService.cs` |
| `IMovieService` | `MovieService` | `CinemaSystem.Infrastructure/Movies/MovieService.cs` |
| `IRoomService` | `RoomService` | `CinemaSystem.Infrastructure/Rooms/RoomService.cs` |
| `ISeatService` | `SeatService` | `CinemaSystem.Infrastructure/Services/SeatService.cs` |
| `IShowtimeService` | `ShowtimeService` | `CinemaSystem.Infrastructure/Showtimes/ShowtimeService.cs` |
| `IBookingService` | `BookingService` | `CinemaSystem.Infrastructure/Services/BookingService.cs` |
| `ICheckoutService` | `CheckoutService` | `CinemaSystem.Infrastructure/Bookings/CheckoutService.cs` |
| `IPaymentService` | `PaymentService` | `CinemaSystem.Infrastructure/Services/PaymentService.cs` |
| `IPaymentWebhookService` | `PaymentWebhookService` | `CinemaSystem.Infrastructure/Services/PaymentWebhookService.cs` |
| `IChatbotService` | `GeminiChatbotService` | `CinemaSystem.Infrastructure/Services/GeminiChatbotService.cs` |
| `ISeatLockStore` | `RedisSeatLockStore` hoặc `InMemorySeatLockStore` | `CinemaSystem.Infrastructure/Services` |
| `IEmailSender` | `SmtpEmailSender` hoặc `MockEmailService` | `CinemaSystem.Infrastructure/Email` |
| `IEmailService` | `SmtpEmailServiceAdapter` hoặc `MockEmailService` | `CinemaSystem.Infrastructure/Email` |
| `IJwtTokenService` | `JwtTokenService` | `CinemaSystem.Infrastructure/Identity/JwtTokenService.cs` |
| `IPasswordHasher` | `Pbkdf2PasswordHasher` | `CinemaSystem.Infrastructure/Security/Pbkdf2PasswordHasher.cs` |
| `IOtpGenerator` | `CryptoOtpGenerator` | `CinemaSystem.Infrastructure/Security/CryptoOtpGenerator.cs` |
| `IClock` | `SystemClock` | `CinemaSystem.Infrastructure/Time/SystemClock.cs` |
| `IWebhookSignatureVerifier` | `HmacVerifyHelper` | `CinemaSystem.Infrastructure/Services/HmacVerifyHelper.cs` |
| `IDatabaseMaintenanceService` | `DatabaseMaintenanceService` | `CinemaSystem.Infrastructure/Data/DatabaseMaintenanceService.cs` |
| `ICinemaDiagnosticsService` | `CinemaDiagnosticsService` | `CinemaSystem.Infrastructure/Persistence/CinemaDiagnosticsService.cs` |

`ISeatLockStore` dùng Redis khi có `Redis:ConnectionString`; nếu không có thì
dùng bộ nhớ của chính API process. Email dùng mock khi
`EmailSettings:UseMock=true`, ngược lại dùng SMTP.

## 5. Login và phân quyền

### 5.1 Tất cả role dùng chung một endpoint login

Customer, Staff, Manager và Admin đều đăng nhập qua:

```text
POST /api/auth/login
  -> AuthController.Login
  -> IAuthService.LoginAsync
  -> AuthService.LoginAsync
  -> USER JOIN ROLE
  -> Pbkdf2PasswordHasher.VerifySecret
  -> JwtTokenService.GenerateAccessToken
  -> REFRESH_TOKEN
  -> AuthResponse
```

Không có `AdminLoginController` hoặc `ManagerLoginController`. Role không do
client gửi lên khi login. `AuthService` lấy role thật từ quan hệ `USER.roleId ->
ROLE.roleId`.

Điều kiện login:

1. email tồn tại;
2. password khớp hash PBKDF2;
3. `emailVerified = true`, trừ khi cấu hình dev bật auto-confirm;
4. `USER.status = ACTIVE`.

JWT do `JwtTokenService` tạo có các claim chính:

- `sub`
- `userId`
- email
- `ClaimTypes.NameIdentifier`
- `ClaimTypes.Role`
- `role`

`AuthConstants.Roles.Normalize` chuẩn hóa cả `ADMIN` và `ROLE_ADMIN` về
`ADMIN`, tương tự cho các role còn lại.

Sau khi controller trả access token, request tiếp theo chạy qua:

```text
Program.UseAuthentication
  -> JwtBearer kiểm chữ ký, issuer, audience, expiry
  -> Program.UseAuthorization
  -> [Authorize], [Authorize(Roles=...)] hoặc [Authorize(Policy=...)]
  -> controller action
```

### 5.2 Admin được tạo như thế nào

Public register luôn tạo Customer, không cho client chèn role.

Admin đầu tiên được seed bởi `DbInitializer.SeedAdminAsync` khi có:

- `ADMIN_PASSWORD`
- `ADMIN_EMAIL` là tùy chọn, mặc định code là `admin@cinema.com`

Seed Admin:

- lấy role `ROLE_ADMIN`;
- hash password;
- tạo `USER` với `status=ACTIVE`;
- đặt `emailVerified=true`.

Không ghi password thật vào source hoặc tài liệu.

### 5.3 Manager được tạo như thế nào

`main` có role và policy cho Manager nhưng **chưa có API tạo Manager** và cũng
không có biến môi trường seed Manager. Endpoint Admin hiện tại chỉ tạo Staff.

Để Manager login được, database phải có `USER`:

- liên kết role `ROLE_MANAGER`;
- password đã được hash đúng bằng `IPasswordHasher`;
- `emailVerified=true`;
- `status=ACTIVE`.

Nếu Manager là nhân sự thuộc một rạp, dữ liệu nên có `STAFF_PROFILE` gắn
`cinemaId`. Tuy nhiên các API room/seat/showtime hiện chưa dùng profile này để
giới hạn phạm vi rạp.

### 5.4 Admin tạo Staff

```text
POST /api/admin/staff
  -> AdminController.CreateStaff
  -> IAdminService.CreateStaffAsync
  -> AdminService.CreateStaffAsync
  -> USER + STAFF_PROFILE + EMAIL_VERIFICATION_TOKEN
  -> IEmailService.SendInvitationAsync
```

Chỉ Admin được gọi endpoint do policy `CanManageUserAndRole`.

Luồng hiện tại:

1. kiểm tra email trùng;
2. chọn cinema đầu tiên theo `cinemaId`;
3. lấy role Staff;
4. tạo placeholder password ngẫu nhiên đã hash;
5. tạo OTP invitation với purpose `PASSWORD_RESET`;
6. lưu `USER`, `STAFF_PROFILE`, `EMAIL_VERIFICATION_TOKEN`;
7. gửi invitation qua email;
8. Staff dùng `POST /api/auth/reset-password` với OTP để đặt password;
9. reset password đánh dấu email đã xác thực;
10. Staff dùng endpoint login chung.

Giới hạn hiện tại: request tạo Staff không nhận `cinemaId`, nên service tự chọn
cinema đầu tiên.

### 5.5 Ma trận quyền đã cấu hình

| Policy | Role được phép |
|---|---|
| `CanBookTicket` | Customer |
| `CanSelectSeat` | Customer, Staff, Manager, Admin |
| `CanBuyFoodAndBeverageInCheckout` | Customer |
| `CanApplyVoucher` | Customer |
| `CanPayOnline` | Customer |
| `CanViewBookingHistory` | Customer |
| `CanReviewAndFeedback` | Customer |
| `CanScanTicket` | Staff, Manager, Admin |
| `CanManageMovie` | Manager, Admin |
| `CanManageCinemaRoomSeat` | Manager, Admin |
| `CanManageShowtime` | Manager, Admin |
| `CanManageFoodAndBeverage` | Staff, Manager, Admin |
| `CanManageVoucher` | Manager, Admin |
| `CanCancelShowtimeAndRefund` | Manager, Admin |
| `CanViewBranchDashboard` | Manager, Admin |
| `CanViewSystemDashboard` | Admin |
| `CanManageUserAndRole` | Admin |
| `CanManageSystem` | Admin |

Policy có trong `Program.cs` không đồng nghĩa với API đã tồn tại. Phần 8 liệt kê
các policy mới chỉ là hạ tầng.

## 6. Danh sách API và luồng class thực tế

### 6.1 Authentication

Controller: `CinemaSystem/Controllers/AuthController.cs`

| API | Controller method | Service method | Dữ liệu/dịch vụ tiếp theo |
|---|---|---|---|
| `POST /api/auth/register` | `Register` | `AuthService.RegisterCustomerAsync` | `ROLE`, `USER`, `CUSTOMER_PROFILE`, OTP hash, SMTP |
| `POST /api/auth/verify-email` | `VerifyEmail` | `AuthService.VerifyEmailAsync` | `USER`, `EMAIL_VERIFICATION_TOKEN` |
| `POST /api/auth/login` | `Login` | `AuthService.LoginAsync` | `USER`, `ROLE`, PBKDF2, JWT, `REFRESH_TOKEN` |
| `POST /api/auth/refresh-token` | `RefreshToken` | `AuthService.RefreshTokenAsync` | kiểm hash token, revoke token cũ, tạo JWT/token mới |
| `POST /api/auth/logout` | `Logout` | `AuthService.LogoutAsync` | revoke `REFRESH_TOKEN` |
| `POST /api/auth/resend-verification-otp` | `ResendVerificationOtp` | `AuthService.ResendVerificationOtpAsync` | cooldown/rate limit OTP, SMTP |
| `POST /api/auth/forgot-password` | `ForgotPassword` | `AuthService.ForgotPasswordAsync` | OTP purpose `PASSWORD_RESET`, SMTP |
| `POST /api/auth/reset-password` | `ResetPassword` | `AuthService.ResetPasswordAsync` | xác minh OTP, đổi hash password, revoke refresh tokens |

Public registration chỉ tạo Customer. OTP thô chỉ được gửi qua email; database
lưu hash. Refresh token thô trả cho client; database lưu hash.

### 6.2 Admin account operation

Controller: `CinemaSystem/Controllers/AdminController.cs`

| API | Quyền | Luồng |
|---|---|---|
| `POST /api/admin/staff` | Admin | `AdminController -> IAdminService -> AdminService -> USER/STAFF_PROFILE/EMAIL_VERIFICATION_TOKEN -> email` |

Trên `main`, đây là API nghiệp vụ duy nhất trong `AdminController`.

### 6.3 Customer profile

Controller: `CinemaSystem/Controllers/CustomersController.cs`

Tất cả endpoint yêu cầu JWT nhưng controller chỉ dùng `[Authorize]`, không ép
role Customer. Service dựa vào user/profile hiện có.

| API | Service method | Bảng chính |
|---|---|---|
| `GET /api/customer/profile` | `CustomerService.GetProfileAsync` | `USER`, `CUSTOMER_PROFILE` |
| `PUT /api/customer/profile` | `CustomerService.UpdateProfileAsync` | `USER`, `CUSTOMER_PROFILE` |
| `POST /api/customer/change-password` | `CustomerService.ChangePasswordAsync` | `USER`, PBKDF2 |
| `POST /api/customer/request-email-change` | `CustomerService.RequestEmailUpdateAsync` | `USER`, `EMAIL_VERIFICATION_TOKEN`, SMTP |
| `POST /api/customer/verify-email-change` | `CustomerService.VerifyEmailUpdateAsync` | `USER`, `EMAIL_VERIFICATION_TOKEN` |
| `GET /api/customer/bookings` | `CustomerService.GetBookingHistoryAsync` | booking, movie, cinema, room, seat |

Ngoài endpoint lịch sử trên còn có `GET /api/bookings/my-bookings`; hai đường
dẫn trả DTO khác nhau.

### 6.4 Public movie, cinema và showtime

| API | Luồng | Xử lý chính |
|---|---|---|
| `GET /api/cinemas` | `CinemasController -> ICinemaService -> CinemaService -> CINEMA` | trả danh sách cinema theo tên |
| `GET /api/movies` | `MoviesController -> IMovieService -> MovieService -> MOVIE` | ẩn movie `INACTIVE` và age rating `C`, cho lọc status |
| `GET /api/movies/{movieId}` | cùng luồng trên | trả chi tiết movie public hoặc 404 |
| `GET /api/showtimes` | `ShowtimesController -> IShowtimeService -> ShowtimeService -> SHOWTIME` | trả danh sách theo start time |
| `GET /api/showtimes/{showtimeId}` | cùng luồng trên | trả một showtime hoặc 404 |

Khác biệt với API contract cần chú ý:

- `GET /api/showtimes` hiện không có filter movie/cinema/date/status và không
  tự ẩn `CANCELLED`, `CLOSED`, `COMPLETED`.
- `GET /api/cinemas` hiện không lọc cinema inactive.
- chưa có search/genre/now-showing route riêng; frontend có thể dùng query
  `status` của `GET /api/movies`.

### 6.5 Quản lý room

Controller: `CinemaSystem/Controllers/RoomsController.cs`

| API | Quyền | Service method | Xử lý |
|---|---|---|---|
| `GET /api/rooms/rooms` | Staff, Manager, Admin | `GetRoomsAsync` | lấy room không `INACTIVE`, kèm cinema và số ghế |
| `GET /api/rooms/rooms/{roomId}` | Staff, Manager, Admin | `GetRoomByIdAsync` | lấy chi tiết room |
| `POST /api/rooms/cinemas/{cinemaId}/rooms` | Manager, Admin | `CreateRoomAsync` | kiểm cinema/status, tạo `ROOM` |
| `PUT /api/rooms/rooms/{roomId}` | Manager, Admin | `UpdateRoomAsync` | kiểm trùng tên/capacity/status, cập nhật `ROOM` |
| `DELETE /api/rooms/rooms/{roomId}` | Manager, Admin | `DeleteRoomAsync` | soft delete bằng `roomStatus=INACTIVE` |
| `POST /api/rooms/{roomId}/generate-seats` | Manager, Admin | `GenerateSeatsAsync` | sinh ma trận `SEAT`, cập nhật capacity |

Giới hạn generate seat:

- rows và columns phải lớn hơn 0;
- tối đa 500 ghế;
- room phải chưa có seat;
- `seatTypeId` trong request được gán cho toàn bộ ghế.

### 6.6 Quản lý seat và khóa ghế

Controller: `CinemaSystem/Controllers/SeatsController.cs`

| API | Quyền | Service method | Xử lý |
|---|---|---|---|
| `POST /api/seats` | Manager, Admin | `CreateSeatAsync` | kiểm room, seat type, trùng code rồi tạo `SEAT` |
| `PUT /api/seats/{seatId}` | Manager, Admin | `UpdateSeatAsync` | đổi row/number/type, kiểm trùng code |
| `DELETE /api/seats/{seatId}` | Manager, Admin | `DeleteSeatAsync` | chặn nếu dùng ở showtime tương lai, sau đó `isActive=false` |
| `GET /api/seats/room/{roomId}` | Staff, Manager, Admin | `GetSeatsByRoomAsync` | lấy layout ghế theo room |
| `POST /api/seats/lock` | Customer | `LockSeatAsync` | khóa phân tán/bộ nhớ rồi cập nhật `SHOWTIME_SEAT` |
| `POST /api/seats/unlock` | Customer | `UnlockSeatAsync` | chỉ chủ lock được mở lock còn hiệu lực |
| `GET /api/seats/showtimes/{showtimeId}/map` | Customer, Staff, Manager, Admin | `GetSeatMapAsync` | giải phóng lock hết hạn, chia available/locked/sold |

Luồng lock:

```text
SeatsController.LockSeat
  -> ISeatService.LockSeatAsync
  -> SeatService kiểm SHOWTIME_SEAT và BOOKING_SEAT
  -> ISeatLockStore.TryLockAsync
     -> RedisSeatLockStore nếu có Redis
     -> InMemorySeatLockStore nếu không có Redis
  -> cập nhật SHOWTIME_SEAT:
     seatStatus=LOCKED, lockedUntil=UTC+10 phút, lockedByUserId
```

Nếu ghi database thất bại sau khi lock store thành công, service release lock
store rồi ném lỗi để tránh lock mồ côi.

### 6.7 Quản lý showtime

Controller: `CinemaSystem/Controllers/ShowtimesController.cs`

| API | Quyền | Service method | Xử lý |
|---|---|---|---|
| `POST /api/showtimes` | Manager, Admin | `CreateShowtimeAsync` | validate movie/room/time/overlap, tạo showtime và showtime seats |
| `PUT /api/showtimes/{showtimeId}` | Manager, Admin | `UpdateShowtimeAsync` | chặn nếu đã có booking; đổi room thì sinh lại showtime seats |
| `DELETE /api/showtimes/{showtimeId}` | Manager, Admin | `DeleteShowtimeAsync` | hard delete khi không có booking/refund history |

Rule đang chạy:

- movie phải `NOW_SHOWING`;
- room và cinema phải `ACTIVE`;
- start time phải ở tương lai;
- end time = start time + duration phim + 15 phút buffer;
- không overlap showtime khác trong cùng room, trừ showtime `CANCELLED`;
- room phải có active seat;
- create sinh một `SHOWTIME_SEAT` trạng thái `AVAILABLE` cho mỗi active seat;
- update trực tiếp bị chặn khi đã có booking;
- delete trực tiếp bị chặn khi có booking hoặc refund history.

`DELETE /api/showtimes/{id}` không phải UC003 hủy suất chiếu. Nó chỉ xóa
showtime chưa phát sinh nghiệp vụ.

### 6.8 Booking và checkout

Controller: `CinemaSystem/Controllers/BookingsController.cs`

Tất cả endpoint yêu cầu policy Customer.

| API | Service method | Vai trò |
|---|---|---|
| `POST /api/bookings` | `BookingService.CreateBookingAsync` | luồng tạo booking cũ/đơn giản |
| `GET /api/bookings/{bookingId}` | `BookingService.GetBookingDetailsAsync` | chỉ chủ booking xem chi tiết |
| `GET /api/bookings/my-bookings` | `BookingService.GetMyBookingsAsync` | danh sách booking của user |
| `POST /api/bookings/checkout` | `CheckoutService.CheckoutAsync` | luồng checkout đầy đủ hơn, có transaction/F&B/voucher/inventory |

Hai luồng tạo booking đang cùng tồn tại:

1. `BookingService.CreateBookingAsync`:
   kiểm customer, showtime, ghế; tính giá ghế và F&B; tạo booking
   `PENDING_PAYMENT`; đặt ghế `LOCKED` 10 phút.
2. `CheckoutService.CheckoutAsync`:
   kiểm email/status customer, showtime còn bán, cutoff time, lock ownership,
   F&B/inventory, voucher và concurrency; lấy giá từ database; tạo booking,
   booking seat, F&B và voucher usage trong transaction.

Đối với luồng mới, frontend nên ưu tiên `/api/bookings/checkout` vì kiểm tra
nghiệp vụ đầy đủ hơn. Team cần thống nhất trước khi loại bỏ route cũ.

Checkout ghi các bảng tùy request:

- `BOOKING`
- `BOOKING_SEAT`
- `BOOKING_FB_ITEM`
- `VOUCHER_USAGE`
- trạng thái liên quan trong `SHOWTIME_SEAT`

### 6.9 SePay payment và phát hành ticket

Tạo payment:

```text
POST /api/payment
  -> PaymentController.CreatePayment
  -> IPaymentService.CreatePaymentAsync
  -> PaymentService
  -> BOOKING + CUSTOMER_PROFILE + PAYMENT_PROVIDER + PAYMENT
  -> trả bank name, bank account, amount, transaction code, expiry
```

Rule chính:

- chỉ chủ booking được thanh toán;
- booking phải `PENDING_PAYMENT`;
- payment provider phải `ACTIVE`;
- không tạo lại nếu đã có payment thành công;
- tái sử dụng pending payment cùng provider;
- transaction code được tạo bằng random bảo mật.

Webhook:

```text
POST /api/payment/sepay-webhook
  -> PaymentController.SepayWebhook
  -> IPaymentWebhookService.HandleSepayWebhookAsync
  -> PaymentWebhookService
  -> IWebhookSignatureVerifier/HmacVerifyHelper
  -> IPaymentService.ConfirmPaymentAsync
  -> PaymentService.ConfirmPaymentAsync
  -> transaction SQL Server
```

Khi callback hợp lệ và đúng amount:

1. `PAYMENT.paymentStatus = SUCCESS`;
2. lưu provider transaction code và raw callback;
3. `BOOKING.bookingStatus = PAID`;
4. các `SHOWTIME_SEAT` chuyển `BOOKED` và xóa thông tin lock;
5. tạo một `TICKET` trạng thái `UNUSED` cho mỗi `BOOKING_SEAT` chưa có ticket;
6. commit transaction.

Confirm payment có tính idempotent: payment đã `SUCCESS` thì trả về mà không
tạo ticket lần hai.

### 6.10 Chatbot

```text
POST /api/chatbot
  -> ChatbotController.Ask
  -> IChatbotService.AskAsync
  -> GeminiChatbotService
  -> IMovieService.GetMoviesAsync
  -> IShowtimeService.GetShowtimesAsync
  -> Google Gemini API
```

Chatbot lấy movie/showtime hiện tại làm context rồi gọi Gemini. Endpoint hiện
không yêu cầu JWT. `main` chưa lưu chat history vào database.

### 6.11 API kỹ thuật và background job

| Thành phần | Luồng |
|---|---|
| `GET /api/health` | `HealthController`, không truy cập DB |
| `GET /api/db-test/movies-count` | `DbTestController -> ICinemaDiagnosticsService -> CinemaDiagnosticsService -> MOVIE` |
| `GET /api/auth-test/customer` | kiểm policy `CanBookTicket`, dùng cho test |
| `GET /api/auth-test/admin` | kiểm policy `CanManageSystem`, dùng cho test |
| `PendingPaymentCleanupHostedService` | định kỳ xóa booking pending hết hạn, release ghế, xóa payment/F&B/voucher usage liên quan |

Hosted service được đăng ký trong `Program.cs`, chạy khi API khởi động và lặp
theo `BookingSettings:PendingPaymentCleanupIntervalSeconds`.

## 7. Đối chiếu các use case trọng tâm

### 7.1 UC001 - Đặt vé

SRS yêu cầu: chọn phim/suất, xem seat map, lock seat, tùy chọn F&B/voucher/reward
point, thanh toán online, tạo e-ticket QR và gửi thông báo.

| Bước UC001 | Trạng thái trên `main` | Code |
|---|---|---|
| Xem movie/cinema/showtime | Đã có cơ bản | `MoviesController`, `CinemasController`, `ShowtimesController` |
| Xem seat map | Đã có | `SeatsController.GetSeatMap` |
| Lock/unlock seat 10 phút | Đã có | `SeatService`, Redis hoặc in-memory lock store |
| Checkout ghế | Đã có | `CheckoutService` |
| Mua F&B trong checkout | Đã có | `CheckoutService.LoadAndValidateFoodItemsAsync` |
| Áp voucher | Đã có trong checkout | `CheckoutService.ValidateVoucherAsync` |
| Dùng reward point | Chưa có | response hiện để `RewardDiscount = 0` |
| Thanh toán online | Đã có SePay | `PaymentService`, `PaymentWebhookService` |
| Chuyển booking/ghế khi paid | Đã có | `PaymentService.ConfirmPaymentAsync` |
| Sinh ticket QR | Đã có | `PaymentService.ConfirmPaymentAsync` |
| Gửi e-ticket email/notification | Chưa có | chưa thấy call email/outbox sau payment |
| Tự dọn pending booking hết hạn | Đã có | `PendingPaymentCleanupHostedService` |

Kết luận: UC001 đã có đường chạy end-to-end chính, nhưng chưa hoàn chỉnh phần
reward point và gửi e-ticket/notification.

### 7.2 UC002 - Quét mã QR vé

SRS yêu cầu Staff/Manager:

- đọc Ticket ID/QR;
- đối chiếu booking, cinema, room, showtime và thời gian;
- chặn check-in lần hai;
- cập nhật ticket/check-in status;
- lưu check-in log;
- giới hạn Staff/Manager theo cinema được phân công.

Trạng thái `main`:

- có entity/DbSet `TICKET` và `CHECKIN_LOG`;
- có policy `CanScanTicket`;
- payment thành công có tạo ticket QR;
- **không có controller/service scan ticket**;
- **không có code cập nhật check-in**;
- **không có kiểm tra cinema scope**.

Kết luận: UC002 chưa triển khai, mới có dữ liệu nền và policy.

### 7.3 UC003 - Hủy suất chiếu và hoàn tiền

SRS yêu cầu Manager/Admin:

- kiểm quyền và cinema scope;
- chuyển showtime sang cancelled;
- tìm paid booking;
- cập nhật booking/refund;
- release seat;
- gọi payment gateway refund;
- notify customer;
- chuyển manual handling nếu auto-refund lỗi.

Trạng thái `main`:

- có entity/DbSet `SHOWTIME_CANCELLATION` và `REFUND`;
- có policy `CanCancelShowtimeAndRefund`;
- `ShowtimeService.DeleteShowtimeAsync` chặn xóa nếu có booking/refund;
- **không có endpoint cancel showtime**;
- **không có refund service**;
- **không có notification cho use case này**;
- **không có cinema scope cho Manager**.

Kết luận: UC003 chưa triển khai trên `main`. Không dùng
`DELETE /api/showtimes/{id}` thay cho use case hủy/refund.

## 8. Chức năng mới có policy/entity nhưng chưa có API hoàn chỉnh

| Chức năng trong SRS | Hạ tầng đang có | Còn thiếu |
|---|---|---|
| Movie CRUD | `MOVIE`, `CanManageMovie` | request DTO admin, controller CRUD, service CRUD |
| Cinema CRUD | `CINEMA`, policy hệ thống | controller/service CRUD |
| Ticket scan/check-in | `TICKET`, `CHECKIN_LOG`, `CanScanTicket` | scan controller/service và rule |
| Cancel showtime/refund | `SHOWTIME_CANCELLATION`, `REFUND`, policy | orchestration service/API/gateway/email |
| F&B management | `FB_ITEM`, inventory, policy | CRUD/stock API cho Staff/Manager/Admin |
| Voucher management | `VOUCHER`, `VOUCHER_USAGE`, policy | CRUD admin/manager |
| Review/feedback | `REVIEW`, policy | API create/edit/moderate |
| Reward point | profile và transaction entity | earn/redeem service |
| Branch dashboard | policy | query/report API và cinema scope |
| System dashboard | policy | Admin dashboard API |
| User/role management | role tables/policy, create Staff | list/update/block/change-role API |
| Chat history | chatbot API | persistence/API history |
| Manager provisioning | role/policy | internal create/assign Manager flow |

## 9. Vị trí database và bảng được dùng

`CinemaDbContext` nằm tại
`CinemaSystem.Infrastructure/Persistence/CinemaDbContext.cs`.

Nhóm auth:

- `ROLE`
- `USER`
- `CUSTOMER_PROFILE`
- `STAFF_PROFILE`
- `EMAIL_VERIFICATION_TOKEN`
- `REFRESH_TOKEN`

Nhóm catalog/vận hành:

- `CINEMA`
- `ROOM`
- `SEAT_TYPE`
- `SEAT`
- `MOVIE`
- `SHOWTIME`
- `SHOWTIME_SEAT`

Nhóm đặt vé/thanh toán:

- `BOOKING`
- `BOOKING_SEAT`
- `BOOKING_FB_ITEM`
- `PAYMENT_PROVIDER`
- `PAYMENT`
- `TICKET`
- `FB_ITEM`
- `CINEMA_FB_INVENTORY`
- `VOUCHER`
- `VOUCHER_USAGE`

Nhóm có entity nhưng use case chưa hoàn chỉnh:

- `CHECKIN_LOG`
- `SHOWTIME_CANCELLATION`
- `REFUND`
- `REVIEW`
- `REWARD_POINT_TRANSACTION`
- `NOTIFICATION`
- `AUDIT_LOG`

Ở Development, `Program.cs` gọi:

```text
IDatabaseMaintenanceService.MigrateAsync
  -> DatabaseMaintenanceService
  -> CinemaDbContext.Database.MigrateAsync

IDatabaseMaintenanceService.SeedAsync
  -> DatabaseMaintenanceService
  -> DbInitializer.SeedAsync
```

`DbInitializer` seed role, Admin khi có biến môi trường, cinema/F&B/test users
trong development. Đây là seed dữ liệu ứng dụng; schema chuẩn vẫn phải đồng bộ
với `docs/database/cinema-booking-schema.sql` và EF model.

## 10. Khoảng trống và điểm team cần thống nhất

1. **Manager scope chưa được enforce.** Các route room/seat/showtime chỉ kiểm
   role Manager/Admin, chưa đối chiếu `STAFF_PROFILE.cinemaId`.
2. **Chưa có cách tạo Manager qua API/seed.** Cần một use case nội bộ có audit,
   không mở qua public register.
3. **Có hai luồng tạo booking.** `POST /api/bookings` và
   `POST /api/bookings/checkout` cần được chuẩn hóa để tránh rule khác nhau.
4. **Public showtime trả cả status không phù hợp.** SRS/API contract yêu cầu ẩn
   cancelled/closed/completed cho khách.
5. **Cinema public chưa lọc inactive.**
6. **Admin tạo Staff tự chọn cinema đầu tiên.** Request cần cinema assignment rõ
   ràng nếu hệ thống nhiều chi nhánh.
7. **Policy không phải implementation.** Scan ticket, refund, dashboard, movie
   CRUD, voucher/F&B management vẫn chưa có use case code trên `main`.
8. **Ticket đã sinh nhưng chưa gửi cho Customer.**
9. **Chatbot chưa lưu lịch sử và endpoint đang anonymous.**
10. **Một số helper `ApplyCreate/Update/Delete` trong `SeatService` không có
    controller gọi trực tiếp.** Không tính là API đã expose.

## 11. Cách lần theo một request khi debug

Ví dụ login:

1. mở route trong `CinemaSystem/Controllers/AuthController.cs`;
2. tìm method interface tại
   `CinemaSystem.Application/Interfaces/IAuthService.cs`;
3. xem interface được map ở
   `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`;
4. mở `CinemaSystem.Infrastructure/Auth/AuthService.cs`;
5. lần theo `CinemaDbContext`, `IPasswordHasher`, `IJwtTokenService`;
6. kiểm policy tại `CinemaSystem/Program.cs`;
7. kiểm test tại `CinemaSystem.Tests/AuthServiceTests.cs` và
   `CinemaSystem.Tests/AuthApiIntegrationTests.cs`.

Ví dụ thanh toán thành công:

1. `PaymentController.SepayWebhook`;
2. `PaymentWebhookService.HandleSepayWebhookAsync`;
3. `HmacVerifyHelper.Verify`;
4. `PaymentService.ConfirmPaymentAsync`;
5. transaction cập nhật `PAYMENT`, `BOOKING`, `SHOWTIME_SEAT`, `TICKET`;
6. integration test trong `CinemaSystem.Tests/BookingApiIntegrationTests.cs`
   và unit test trong `CinemaSystem.Tests/PaymentServiceTests.cs`.

Khi thêm use case mới, team nên giữ cùng cấu trúc:

```text
Request/Response DTO
  -> Application interface
  -> Infrastructure implementation
  -> DI registration
  -> thin Controller + authorization
  -> unit test + integration test
  -> cập nhật tài liệu này
```
