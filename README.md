# CinemaSystem Backend

ASP.NET Core 8 Web API for an online movie ticket booking and cinema
management system.

## Features

- Customer registration and email OTP verification
- JWT login, refresh-token rotation, logout, and role authorization
- Forgot-password and password-reset flows
- Staff account provisioning by administrators
- Cinema, room, seat, and showtime management
- Temporary seat locking with in-memory or Redis storage
- SePay payment creation and webhook confirmation

## Architecture

| Project | Responsibility |
| --- | --- |
| `CinemaSystem` | API controllers, middleware, authentication, Swagger, and DI entry point |
| `CinemaSystem.Application` | Use-case interfaces, application constants, and service contracts |
| `CinemaSystem.Contracts` | Request and response DTOs |
| `CinemaSystem.Domain` | Domain entities and business concepts |
| `CinemaSystem.Infrastructure` | EF Core, SQL Server, JWT, SMTP, Redis, and payment implementations |
| `CinemaSystem.Tests` | Automated tests |

Project documents are indexed in [`docs/README.md`](docs/README.md).

## Requirements

- .NET 8 SDK
- SQL Server
- Redis is optional; the application can use the in-memory seat-lock store
- Gmail SMTP credentials are optional when mock email mode is enabled

## Local Setup

1. Clone the repository.
2. Create the local settings file:

   ```powershell
   Copy-Item CinemaSystem/appsettings.Development.example.json CinemaSystem/appsettings.Development.json
   ```

3. Add local credentials to `CinemaSystem/appsettings.Development.json`.
4. Create the database using
   [`docs/database/cinema-booking-schema.sql`](docs/database/cinema-booking-schema.sql).
5. Restore, build, and test:

   ```powershell
   dotnet restore CinemaSystem.sln
   dotnet build CinemaSystem.sln
   dotnet test CinemaSystem.sln
   ```

6. Run the API:

   ```powershell
   dotnet run --project CinemaSystem
   ```

Swagger is available at `/swagger` in the development environment.

## Configuration

Keep credentials in `appsettings.Development.json`, .NET User Secrets, or
environment variables. Never commit database passwords, JWT secrets, SMTP app
passwords, webhook secrets, tokens, or production account details.

Important configuration sections:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings`
- `EmailSettings`
- `SepaySettings`
- `Redis:ConnectionString`

Optional seed accounts are created only when their password variables are set:

- `ADMIN_PASSWORD` and optional `ADMIN_EMAIL`
- `DEV_STAFF_PASSWORD`
- `DEV_CUSTOMER_PASSWORD`

## Git Workflow

Create feature branches from `main` and merge through a pull request or merge
request:

```powershell
git switch main
git pull
git switch -c feature/short-description
```

Before opening a PR/MR, run:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.sln
# CinemaSystem Backend

ASP.NET Core 8 Web API for an online movie ticket booking and cinema
management system.

## Features

- Customer registration and email OTP verification
- JWT login, refresh-token rotation, logout, and role authorization
- Forgot-password and password-reset flows
- Staff account provisioning by administrators
- Cinema, room, seat, and showtime management
- Temporary seat locking with in-memory or Redis storage
- SePay payment creation and webhook confirmation

## Architecture

| Project | Responsibility |
| --- | --- |
| `CinemaSystem` | API controllers, middleware, authentication, Swagger, and DI entry point |
| `CinemaSystem.Application` | Use-case interfaces, application constants, and service contracts |
| `CinemaSystem.Contracts` | Request and response DTOs |
| `CinemaSystem.Domain` | Domain entities and business concepts |
| `CinemaSystem.Infrastructure` | EF Core, SQL Server, JWT, SMTP, Redis, and payment implementations |
| `CinemaSystem.Tests` | Automated tests |

Project documents are indexed in [`docs/README.md`](docs/README.md).

## Requirements

- .NET 8 SDK
- SQL Server
- Redis is optional; the application can use the in-memory seat-lock store
- Gmail SMTP credentials are optional when mock email mode is enabled

## Local Setup

1. Clone the repository.
2. Create the local settings file:

   ```powershell
   Copy-Item CinemaSystem/appsettings.Development.example.json CinemaSystem/appsettings.Development.json
   ```

3. Add local credentials to `CinemaSystem/appsettings.Development.json`.
4. Create the database using
   [`docs/database/cinema-booking-schema.sql`](docs/database/cinema-booking-schema.sql).
5. Restore, build, and test:

   ```powershell
   dotnet restore CinemaSystem.sln
   dotnet build CinemaSystem.sln
   dotnet test CinemaSystem.sln
   ```

6. Run the API:

   ```powershell
   dotnet run --project CinemaSystem
   ```

Swagger is available at `/swagger` in the development environment.

## Configuration

Keep credentials in `appsettings.Development.json`, .NET User Secrets, or
environment variables. Never commit database passwords, JWT secrets, SMTP app
passwords, webhook secrets, tokens, or production account details.

Important configuration sections:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings`
- `EmailSettings`
- `SepaySettings`
- `Redis:ConnectionString`

Optional seed accounts are created only when their password variables are set:

- `ADMIN_PASSWORD` and optional `ADMIN_EMAIL`
- `DEV_STAFF_PASSWORD`
- `DEV_CUSTOMER_PASSWORD`

## Git Workflow

Create feature branches from `main` and merge through a pull request or merge
request:

```powershell
git switch main
git pull
git switch -c feature/short-description
```

Before opening a PR/MR, run:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.sln
```

## Recent Architecture & Business Logic Updates

Dưới đây là tổng hợp các ý tưởng nghiệp vụ và kiến trúc hệ thống đã được tích hợp gần đây (Đổi giờ chiếu, Đổi phòng, và Quản lý bảo trì):

### 1. Quản lý trạng thái liên đới (Cascading Maintenance)
*   **Ghế & Phòng chiếu:** Khi một Ghế bị **Xóa**, Phòng chiếu (`Room`) chứa ghế đó sẽ tự động chuyển sang trạng thái `MAINTENANCE` (Bảo trì). (Nếu chỉ cập nhật ghế thành Vô hiệu hóa, chỉ ghế đó bị đánh dấu bảo trì trong các suất chiếu tương lai).
*   **Phòng chiếu & Suất chiếu:** Khi một Phòng chiếu bị Bảo trì hoặc Xóa, tất cả các Suất chiếu (`Showtime`) thuộc phòng đó đang mở bán (`OPEN`) sẽ tự động chuyển sang trạng thái `SUSPENDED` (Đình chỉ).
*   *Lưu ý:* Hệ thống **không tự động hoàn tiền (refund)** lúc này để Admin có cơ hội xử lý (chuyển phòng khác).

### 2. Nghiệp vụ Đổi Phòng Chiếu (Re-seat / Change Room)
*   Được thiết kế để giải quyết các Suất chiếu bị `SUSPENDED`. Admin gọi API `POST /api/showtimes/{showtimeId}/change-room`.
*   **Ánh xạ ghế (Seat Mapping):** Cho phép truyền `SeatMapping` tự định nghĩa (Ghế cũ -> Ghế mới). Nếu rỗng, hệ thống sẽ tự động ghép dựa trên mã ghế (`SeatCode`).
*   **Tự động hóa:** Sau khi đổi phòng và chuyển ghế cho các vé đã mua thành công, Suất chiếu sẽ tự động trở lại trạng thái `OPEN`. Đồng thời hệ thống gửi Email thông báo đổi phòng cho tất cả các khách hàng đã thanh toán.

### 3. Nghiệp vụ Đổi Giờ Chiếu & Token Xác Nhận (Time Change Approval)
*   **Lệch giờ $\ge$ 15 phút:** Khi Admin cập nhật giờ chiếu lệch quá 15 phút so với giờ cũ, vé của khách sẽ bị đưa vào trạng thái chờ `ProcessingUnstable`. 
*   **Mã hóa Token không cần DB:** Backend tạo bảo mật `HMACSHA256` dựa trên `BookingId` để sinh ra link xác nhận mà không cần tạo thêm bảng trong Database.
*   **Quyền quyết định của User:** Hệ thống tự gửi mail song ngữ (Anh-Việt) kèm 2 lựa chọn (Links):
    1.  *Chấp nhận:* Gọi API `GET /api/bookings/{id}/confirm-time-change?accept=true` $\rightarrow$ Vé về lại `PAID`.
    2.  *Không chấp nhận & Hủy vé:* Truyền `accept=false` $\rightarrow$ Vé chuyển thành `PendingRefund` và sinh ra bản ghi `Refund`.
*   **Lệch giờ < 15 phút:** Chỉ gửi Email thông báo song ngữ cho khách hàng mà không làm gián đoạn trạng thái vé.

### 4. Quy trình Cập nhật & Xóa (Movie, Showtime, Seat)
*   **Với Phim (Movie):**
    *   *Cập nhật thời lượng (Duration):* Nếu thay đổi thời lượng, hệ thống tự động tìm tất cả suất chiếu đang mở (OPEN) của phim đó, **hủy toàn bộ suất chiếu** và **tự động hoàn tiền (Refund)** cho khách hàng đã mua vé với lý do thay đổi độ dài.
    *   *Xóa phim:* Xóa mềm (Inactive). Hệ thống cũng sẽ tự động hủy toàn bộ suất chiếu đang mở và tiến hành hoàn tiền tự động cho khách.
*   **Với Suất chiếu (Showtime):**
    *   *Xóa suất chiếu:* Nếu chưa có vé bán ra, hệ thống xóa vĩnh viễn (Hard Delete). Nếu đã có vé được bán, hệ thống **hủy suất chiếu (Cancelled)**, giải phóng toàn bộ ghế, chuyển vé sang `PendingRefund`, tạo bản ghi `Refund` và tự động gửi Email thông báo hoàn tiền.
    *   *Cập nhật giờ/phòng:* Kích hoạt tính năng chờ xử lý *ProcessingUnstable* và xác nhận qua Token như mô tả ở Mục 3.
*   **Với Ghế (Seat):**
    *   *Cập nhật Bảo trì (IsActive = false):* Ghế bị chuyển thành Inactive. Tại các suất chiếu tương lai, ghế này tự động đổi thành trạng thái `MAINTENANCE`. Nếu ghế đã bị khách đặt, một Background Job (Hangfire) sẽ tự động gửi Email thông báo và hỗ trợ khách hàng.
    *   *Xóa ghế:* (Tác động rất lớn) Việc xóa một ghế sẽ đẩy cả Phòng chiếu vào trạng thái `MAINTENANCE`, đồng thời **Đình chỉ (SUSPENDED)** toàn bộ các suất chiếu đang mở của phòng chiếu đó.

### 5. Xử lý thanh toán muộn (Late Payment Handling)
*   Khi khách hàng thanh toán qua SePay nhưng vé đã bị đánh dấu là Hết hạn (EXPIRED) hoặc Suất chiếu đã bị Hủy (CANCELLED).
*   Hệ thống sẽ không đánh dấu vé là PAID mà sẽ tự động cập nhật thành REFUND_PENDING và sinh ra bản ghi REFUND để hoàn tiền lại cho khách hàng.
*   Luồng dọn dẹp vé chưa thanh toán (Cronjob) sử dụng Soft Cancel (cập nhật trạng thái) thay vì xóa cứng (Hard Delete) để giữ lại đối soát dòng tiền cho các giao dịch trễ.

### 6. Ràng buộc quan hệ 1-1 của Entity Framework (Showtime Cancellation)
*   Hệ thống bắt lỗi nghiêm ngặt với các quan hệ 1-1 (Ví dụ: 1 Suất chiếu chỉ có 1 Biên bản hủy SHOWTIME_CANCELLATION).
*   Khi Admin cố tình thao tác xóa nhiều lần, hệ thống sử dụng chung biên bản hủy cũ thay vì tạo mới, giúp tránh lỗi ShowtimeId IS NULL khi EF Core tự động nullify khóa ngoại.
