# NOTE KIẾN TRÚC DỰ ÁN CINEMA SYSTEM

## Tổng quan kiến trúc

| Layer                | Project                     | Vai trò                                  |
| -------------------- | --------------------------- | ---------------------------------------- |
| API Layer            | CinemaSystem                | Nhận Request, trả Response               |
| Application Layer    | CinemaSystem.Application    | Chứa Interface và Business Rules         |
| Domain Layer         | CinemaSystem.Domain         | Chứa Entity và Business Model            |
| Infrastructure Layer | CinemaSystem.Infrastructure | Triển khai Database, JWT, Email, Payment |
| Contract Layer       | CinemaSystem.Contracts      | DTO Request/Response                     |
| Test Layer           | CinemaSystem.Tests          | Unit Test và Integration Test            |

---

## 1. CinemaSystem.Domain

### Mục đích

Tầng lõi của hệ thống.

Chứa các đối tượng nghiệp vụ chính.

### Thư mục

```text
Entities/
├── User
├── Role
├── CustomerProfile
├── StaffProfile
├── Cinema
├── Room
├── Seat
├── Showtime
├── Booking
├── Payment
└── ...
```

### Chứa

✅ Entity

✅ Enum

✅ Business Model

### Không chứa

❌ Controller

❌ EF Core

❌ SQL Query

❌ JWT

❌ SMTP

❌ SePay

---

## 2. CinemaSystem.Application

### Mục đích

Định nghĩa nghiệp vụ và các hợp đồng (Contract) mà hệ thống cần.

### Thư mục

```text
Application
├── Interfaces
├── Common
└── Auth
```

### Interface hiện có

```text
IAuthService
ICinemaService
IRoomService
ISeatService
IShowtimeService

IJwtTokenService
IEmailService
IPaymentService

ISeatLockStore
IPasswordHasher
IOtpGenerator
```

### Vai trò

Ví dụ:

```csharp
public interface IJwtTokenService
{
    string GenerateToken(...);
}
```

Application chỉ biết:

```text
"Cần tạo JWT"
```

Không biết:

```text
"Tạo JWT bằng thư viện nào"
```

---

## 3. CinemaSystem.Infrastructure

### Mục đích

Triển khai các Interface từ Application.

### Thư mục

```text
Infrastructure
├── Auth
├── Persistence
├── Services
├── Identity
├── Email
├── Data
├── Security
└── Configuration
```

### Chức năng

| Folder      | Chức năng                |
| ----------- | ------------------------ |
| Persistence | DbContext, EF Core       |
| Identity    | JWT Service              |
| Email       | SMTP Email               |
| Security    | Hash Password, OTP       |
| Services    | Payment, Seat Lock       |
| Data        | Seed Data, Database Init |

### Ví dụ

```text
JwtTokenService.cs
PaymentService.cs
PaymentWebhookService.cs
CinemaDbContext.cs
Pbkdf2PasswordHasher.cs
```

---

## 4. CinemaSystem (API)

### Mục đích

Expose REST API cho Client.

### Thư mục

```text
Controllers
Middlewares
Filters
Mapping
```

### Controllers

```text
AuthController
AdminController
PaymentController
RoomsController
SeatsController
ShowtimesController
CinemasController
```

### Flow

```text
Client
 ↓
Controller
 ↓
Service
 ↓
DbContext
 ↓
Database
```

### Nhiệm vụ

✅ Nhận Request

✅ Validate cơ bản

✅ Gọi Service

✅ Trả Response

### Không nên chứa

❌ Business Logic

❌ SQL Query

---

## 5. CinemaSystem.Contracts

### Mục đích

Chứa DTO giao tiếp giữa Client và API.

### Request DTO

```text
LoginRequest
RegisterRequest
CreateRoomRequest
CreateShowtimeRequest
CreatePaymentRequest
```

### Response DTO

```text
AuthResponse
RoomResponse
SeatResponse
ShowtimeResponse
CreatePaymentResponse
```

### Lợi ích

Không trả Entity trực tiếp ra ngoài.

Ví dụ:

```text
Entity User
↓
UserResponse
↓
API Response
```

---

## 6. CinemaSystem.Tests

### Mục đích

Kiểm thử hệ thống.

### Test hiện có

```text
AuthServiceTests
PaymentServiceTests
RoomShowtimeServiceTests
```

### Loại test

✅ Unit Test

✅ Integration Test

---

# Luồng hoạt động tổng thể

```text
Client
   ↓
Controller
   ↓
Application Interface
   ↓
Infrastructure Service
   ↓
DbContext
   ↓
SQL Server
```

Ví dụ Login:

```text
AuthController
   ↓
IAuthService
   ↓
AuthService
   ↓
CinemaDbContext
   ↓
User Table
```

---

# Repository Pattern

## Dự án hiện tại

```text
AuthService
      ↓
CinemaDbContext
```

Service gọi trực tiếp DbContext.

Ví dụ:

```csharp
_db.Users
_db.Bookings
_db.Payments
```

---

## Có Repository hay không?

### Hiện tại

✅ Không có Repository

✅ Vẫn đúng kiến trúc

✅ EF Core đã đóng vai trò Repository + Unit Of Work

### Không bắt buộc thêm

```text
IUserRepository
UserRepository
IBookingRepository
BookingRepository
```

trừ khi:

* Dự án rất lớn
* Nhiều Database
* CQRS phức tạp
* Yêu cầu từ giảng viên

---

# Đánh giá kiến trúc

| Tiêu chí                           | Đánh giá           |
| ---------------------------------- | ------------------ |
| Tách Domain                        | ✅                  |
| Tách DTO                           | ✅                  |
| Dependency Injection               | ✅                  |
| JWT Authentication                 | ✅                  |
| Payment Integration                | ✅                  |
| Middleware                         | ✅                  |
| Unit Test                          | ✅                  |
| Repository Pattern                 | ❌ (không bắt buộc) |
| Clean Architecture chuẩn tuyệt đối | ⚠️ Chưa hoàn toàn  |

---

# Kết luận

Kiến trúc hiện tại là:

Layered Architecture + Clean Architecture Simplified

Mức độ hoàn thiện:

8/10

Phù hợp cho:

✅ Đồ án tốt nghiệp

✅ Dự án học tập

✅ Hệ thống quản lý rạp phim quy mô vừa

Không cần bổ sung Repository Pattern nếu không có yêu cầu đặc biệt.
