# Chức năng đã triển khai và bản đồ tìm code trên `main`

## 1. Mục đích

Tài liệu này giúp giải thích code theo một đường cố định, không phải tìm ngẫu
nhiên:

```text
HTTP route
  -> CinemaSystem/Controllers/<Feature>Controller.cs
  -> CinemaSystem.Application/Interfaces/I<Feature>Service.cs
  -> CinemaSystem.Infrastructure/<folder>/<Feature>Service.cs
  -> CinemaSystem.Infrastructure/Persistence/CinemaDbContext.cs
     hoặc Redis / SMTP / Gemini / SePay / wwwroot
  -> ServiceResult
  -> Controller đóng gói ApiResponse
  -> client
```

Baseline được rà soát: `origin/main` commit `63576e7`, đã merge vào local
`main`. Comment điều hướng trong code dùng cùng quy ước:

- **Điểm vào**: request bắt đầu ở controller nào.
- **Xử lý**: service chịu trách nhiệm nghiệp vụ nào.
- **Đi tiếp**: interface được DI ánh xạ sang class/folder nào, rồi tới DB hay
  dịch vụ ngoài nào.

## 2. Vai trò của từng project

| Project/folder | Trách nhiệm |
|---|---|
| `CinemaSystem/Controllers` | Route, đọc JWT/claim, model validation, HTTP status |
| `CinemaSystem/Program.cs` | Middleware, Swagger, JWT, authorization policy |
| `CinemaSystem.Application/Interfaces` | Hợp đồng use case mà controller gọi |
| `CinemaSystem.Application/Common` | Role, policy, constant, `ServiceResult` |
| `CinemaSystem.Contracts` | Request/response DTO, `ApiResponse` |
| `CinemaSystem.Infrastructure` | Nghiệp vụ runtime, EF Core, Redis, SMTP, Gemini, SePay, file |
| `CinemaSystem.Domain/Entities` | Entity ánh xạ bảng DB |
| `CinemaSystem.Tests` | Unit/integration test |

Project không có Repository riêng. `CinemaDbContext` hiện đóng vai trò truy cập
DB và unit of work.

## 3. Bảng DI: interface đi tới class nào

File ánh xạ:
`CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`.

| Interface controller/use case gọi | Class chạy thật | Folder |
|---|---|---|
| `IAuthService` | `AuthService` | `CinemaSystem.Infrastructure/Auth` |
| `IAdminService` | `AdminService` | `CinemaSystem.Infrastructure/Auth` |
| `ICustomerService` | `CustomerService` | `CinemaSystem.Infrastructure/Services` |
| `ICinemaService` | `CinemaService` | `CinemaSystem.Infrastructure/Cinemas` |
| `IMovieService` | `MovieService` | `CinemaSystem.Infrastructure/Movies` |
| `IRoomService` | `RoomService` | `CinemaSystem.Infrastructure/Rooms` |
| `ISeatService` | `SeatService` | `CinemaSystem.Infrastructure/Services` |
| `IShowtimeService` | `ShowtimeService` | `CinemaSystem.Infrastructure/Showtimes` |
| `IBookingService` | `BookingService` | `CinemaSystem.Infrastructure/Services` |
| `IPaymentService` | `PaymentService` | `CinemaSystem.Infrastructure/Services` |
| `IPaymentWebhookService` | `PaymentWebhookService` | `CinemaSystem.Infrastructure/Services` |
| `IReviewService` | `ReviewService` | `CinemaSystem.Infrastructure/Services` |
| `IAiModerationService` | `GeminiModerationService` | `CinemaSystem.Infrastructure/Services` |
| `IChatbotService` | `GeminiChatbotService` | `CinemaSystem.Infrastructure/Services` |
| `IAdminRefundService` | `AdminRefundService` | `CinemaSystem.Infrastructure/Services` |
| `IFileStorageService` | `LocalFileStorageService` | `CinemaSystem.Infrastructure/Services` |
| `ISeatLockStore` | `RedisSeatLockStore` hoặc `InMemorySeatLockStore` | `CinemaSystem.Infrastructure/Services` |
| `IEmailSender` | `SmtpEmailSender` | `CinemaSystem.Infrastructure/Email` |
| `IEmailService` | SMTP adapter/mock | `CinemaSystem.Infrastructure/Email` |
| `IJwtTokenService` | `JwtTokenService` | `CinemaSystem.Infrastructure/Identity` |
| `IPasswordHasher` | `Pbkdf2PasswordHasher` | `CinemaSystem.Infrastructure/Security` |
| `IOtpGenerator` | `CryptoOtpGenerator` | `CinemaSystem.Infrastructure/Security` |
| `IWebhookSignatureVerifier` | `HmacVerifyHelper` | `CinemaSystem.Infrastructure/Services` |
| `IDatabaseMaintenanceService` | `DatabaseMaintenanceService` | `CinemaSystem.Infrastructure/Data` |

## 4. Chức năng đã có

### 4.1 Authentication và tài khoản

Điểm vào: `CinemaSystem/Controllers/AuthController.cs`.

| API | Service method | Đích xử lý |
|---|---|---|
| `POST /api/auth/register` | `RegisterCustomerAsync` | USER, CUSTOMER_PROFILE, OTP, email |
| `POST /api/auth/verify-email` | `VerifyEmailAsync` | EMAIL_VERIFICATION_TOKEN, USER |
| `POST /api/auth/login` | `LoginAsync` | password hash, JWT, REFRESH_TOKEN |
| `POST /api/auth/google-login` | `GoogleLoginAsync` | Google token, USER, JWT |
| `POST /api/auth/refresh-token` | `RefreshTokenAsync` | rotate REFRESH_TOKEN, JWT |
| `POST /api/auth/logout` | `LogoutAsync` | revoke REFRESH_TOKEN |
| `POST /api/auth/resend-verification-otp` | `ResendVerificationOtpAsync` | OTP cooldown + email |
| `POST /api/auth/forgot-password` | `ForgotPasswordAsync` | reset OTP + email |
| `POST /api/auth/reset-password` | `ResetPasswordAsync` | password hash + revoke token |

Class xử lý:
`CinemaSystem.Infrastructure/Auth/AuthService.cs`.

Public register chỉ tạo Customer. Role thật khi login được đọc từ USER -> ROLE,
không lấy từ request.

### 4.2 Admin tạo Staff

```text
POST /api/admin/staff
  -> AdminController
  -> IAdminService
  -> Auth/AdminService
  -> USER + STAFF_PROFILE + EMAIL_VERIFICATION_TOKEN
  -> IEmailService
```

Main chưa có API công khai để tạo Manager.

### 4.3 Customer profile

Điểm vào: `CustomersController`.
Đích: `CustomerService` trong `Infrastructure/Services`.

- Xem/cập nhật profile.
- Đổi mật khẩu.
- Yêu cầu và xác nhận đổi email.
- Xem booking history.

### 4.4 Movie

Điểm vào: `MoviesController`.
Đích: `MovieService` trong `Infrastructure/Movies`.

- Danh sách phim phân trang, lọc status và genre.
- Chi tiết phim.
- Tăng view.
- Manager/Admin tạo, cập nhật, xóa mềm phim.
- Upload/lưu poster qua `IFileStorageService`.
- Quan hệ MOVIE_GENRE, GENRE, LANGUAGE.
- `MovieHighlightClassificationJob` cập nhật HOT/TRENDING theo lịch nền.

### 4.5 Cinema và room

Cinema:

```text
GET /api/cinemas
  -> CinemasController
  -> ICinemaService
  -> Infrastructure/Cinemas/CinemaService
  -> CINEMA
```

Room:

- Staff/Manager/Admin xem danh sách và chi tiết.
- Manager/Admin tạo, sửa, xóa mềm room.
- Sinh ma trận ghế cho room.
- Đích xử lý: `Infrastructure/Rooms/RoomService`.

### 4.6 Seat và seat lock

Điểm vào: `SeatsController`.
Đích: `Infrastructure/Services/SeatService`.

- CRUD seat cho Manager/Admin.
- Staff/Manager/Admin xem layout.
- Customer lock/unlock seat.
- Customer/Staff/Manager/Admin xem seat map.
- Redis được dùng khi có `Redis:ConnectionString`; nếu không có thì dùng
  `InMemorySeatLockStore`.
- DB liên quan: SEAT, SHOWTIME_SEAT, BOOKING_SEAT.

### 4.7 Showtime

Điểm vào: `ShowtimesController`.
Đích: `Infrastructure/Showtimes/ShowtimeService`.

- Danh sách và chi tiết công khai.
- Manager/Admin tạo, cập nhật, đổi phòng và xóa showtime.
- Kiểm tra movie/room active, thời lượng phim, cleaning buffer và overlap.
- Sinh lại SHOWTIME_SEAT khi cần.
- Luồng có booking/refund có thể gọi tiếp `IAdminRefundService`.

### 4.8 Booking

Điểm vào: `BookingsController`.
Đích: `Infrastructure/Services/BookingService`.

- Tạo booking.
- Xem chi tiết booking của chính Customer.
- Danh sách booking của Customer.
- Xác nhận/từ chối thay đổi thời gian bằng token.
- Customer hủy booking đủ điều kiện.
- Dữ liệu: BOOKING, BOOKING_SEAT, SHOWTIME_SEAT, BOOKING_FB_ITEM, VOUCHER_USAGE,
  PAYMENT, TICKET, REFUND.

`CheckoutService` và `/api/bookings/checkout` đã bị loại khỏi main mới. Luồng
tạo đơn hiện tập trung tại `BookingService.CreateBookingAsync`.

### 4.9 Payment SePay

```text
POST /api/payment
  -> PaymentController
  -> IPaymentService.CreatePaymentAsync
  -> PaymentService
  -> PAYMENT

POST /api/payment/sepay-webhook
  -> PaymentController
  -> IPaymentWebhookService
  -> PaymentWebhookService
  -> IWebhookSignatureVerifier
  -> IPaymentService.ConfirmPaymentAsync
  -> PAYMENT + BOOKING + SHOWTIME_SEAT + TICKET/REFUND
```

`PendingPaymentCleanupHostedService` định kỳ hủy giao dịch quá hạn và giải
phóng tài nguyên liên quan.

### 4.10 Review và AI moderation

Điểm vào: `ReviewsController`.
Đích: `ReviewService`.

- Customer tạo/sửa review nếu booking hợp lệ.
- Đọc review đã duyệt theo phim.
- Customer xem review của mình.
- Admin duyệt review.
- ReviewService gọi `GeminiModerationService`.
- ReviewService gọi `IMovieService` để cập nhật average rating/review count.

### 4.11 Chatbot Gemini

```text
POST /api/chatbot
  -> ChatbotController
  -> IChatbotService
  -> GeminiChatbotService
  -> IMovieService + IShowtimeService + Gemini API + CHAT_HISTORY
```

### 4.12 Admin refund

Điểm vào: `AdminRefundsController`.
Đích: `AdminRefundService`.

- Admin xem danh sách refund phân trang.
- Admin xác nhận refund theo booking.
- Service xử lý SHOWTIME, BOOKING, PAYMENT, REFUND, TICKET, seat lock và email.

### 4.13 Upload, health và DB diagnostics

- `UploadController`: lưu ảnh vào `CinemaSystem/wwwroot/images`.
- `HealthController`: health check đơn giản.
- `DbTestController`: kiểm tra kết nối DB bằng movie count.

## 5. Policy có nhưng use case chưa hoàn chỉnh

Các policy sau tồn tại trong `Program.cs`, nhưng không nên kết luận chức năng đã
hoàn chỉnh nếu chưa tìm thấy controller/service:

- `CanScanTicket`: chưa có Ticket/Checkin controller hoàn chỉnh.
- `CanManageFoodAndBeverage`: chưa có F&B management controller.
- `CanManageVoucher`: chưa có Voucher management controller.
- `CanViewBranchDashboard`: chưa có ManagerDashboard controller/service.
- `CanViewSystemDashboard`: chưa có system dashboard controller/service.
- Manager chưa bị giới hạn theo `STAFF_PROFILE.cinemaId` ở room/seat/showtime.

## 6. Cách tìm code nhanh khi giải thích

### Khi biết URL

1. Tìm `[Route]` và `[Http...]` trong `CinemaSystem/Controllers`.
2. Xem interface được inject ở đầu controller.
3. Mở interface cùng tên trong `CinemaSystem.Application/Interfaces`.
4. Tra ánh xạ trong
   `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`.
5. Mở implementation trong Infrastructure.
6. Tìm `_dbContext.<DbSet>` để biết bảng đích.

### Khi biết tên bảng

1. Mở entity trong `CinemaSystem.Domain/Entities`.
2. Mở mapping tại `CinemaSystem.Infrastructure/Persistence/CinemaDbContext.cs`.
3. Dùng tìm kiếm `_dbContext.<DbSet>` để tìm service sử dụng.
4. Từ service tìm ngược interface và controller.

### Khi biết policy/role

1. Tìm policy trong `CinemaSystem.Application/Common/AuthConstants.cs`.
2. Xem role mapping tại `CinemaSystem/Program.cs`.
3. Tìm `[Authorize(...)]` trong Controllers.
4. Đừng coi policy là chức năng hoàn chỉnh nếu không có route/service.

## 7. Các file đã được gắn comment điều hướng

Nhóm entry point:

- `Program.cs`
- Auth, Admin, AdminRefund, Booking, Customer, Movie, Cinema, Room, Seat,
  Showtime, Review, Payment, Chatbot, Upload, Health và DB test controllers.

Nhóm xử lý:

- `DependencyInjection.cs`
- Auth/Admin/Cinema/Movie/Room/Showtime services.
- Booking/Customer/Seat/Payment/Webhook/Review/AdminRefund services.
- Gemini chatbot và moderation services.

Comment được đặt ở class hoặc action quan trọng, tập trung vào trách nhiệm và
đích chuyển tiếp. Không comment lại các dòng cú pháp hiển nhiên để tránh làm
code khó đọc và nhanh lỗi thời.
