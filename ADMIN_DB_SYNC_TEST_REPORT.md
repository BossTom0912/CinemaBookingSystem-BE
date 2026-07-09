# ADMIN MERGE, DATABASE SYNC & TEST REPORT

## 1. Phạm vi thực hiện

* Nhánh Admin tham chiếu: `Admin`
* Commit Admin mới nhất đã fetch: `745693f`
* Nhánh tích hợp: `Tom/integration-main-manager-admin-test`
* Mục tiêu:
  * Merge code mới từ Admin.
  * Giải quyết conflict theo kiến trúc và nghiệp vụ Admin.
  * Giữ các extension Manager không phá vỡ Admin.
  * Tạo lại một database thống nhất từ schema hợp nhất.
  * Build, chạy test và smoke test backend.

## 2. Kết quả merge

* Đã fetch toàn bộ remote và xác nhận Admin local khớp remote.
* Đã merge Admin vào nhánh tích hợp bằng `--no-commit --no-ff`.
* Số file conflict: 24.
* Cách giải quyết:
  * Dùng phiên bản Admin cho các conflict thuộc auth, cinema, movie, room, booking, payment, review, seat, showtime, configuration và schema.
  * Xóa các SQL patch/migration rời mà Admin đã loại bỏ.
  * Giữ `docs/database/cinema-booking-schema.sql` làm schema thống nhất.
* Không commit.
* Không push.

## 3. Extension Manager được giữ lại

Sau khi lấy logic Admin, build/integration test phát hiện các đăng ký Manager bị mất. Các phần sau đã được bổ sung lại theo cấu trúc DI của Admin:

* `ICinemaScopeAuthorizationService`
* `IManagerDashboardService`
* `IShowtimeCancellationService`
* `IRefundService`
* `IRefundClaimService`
* `IManualRefundService`
* `IRefundProcessor`
* `IRefundClaimIssuer`
* `ISensitiveDataProtector`
* Payment refund gateway
* Concrete registrations cần cho Room, Seat và Showtime flows

Các điều chỉnh tương thích:

* `RoomService.GetRoomsAsync` tiếp tục lọc theo `cinemaScopeId`.
* Manager ngoài phạm vi rạp tiếp tục bị từ chối.
* Admin tiếp tục có quyền bypass cinema scope.
* Không cho xóa cứng showtime đã có cancellation/refund history.
* Late payment của booking/showtime đã hủy tiếp tục tạo refund claim.

## 4. Lỗi phát hiện và đã sửa

### DB-001 — SQL filtered index không chạy với sqlcmd mặc định

* Nguyên nhân: schema chưa chủ động bật `QUOTED_IDENTIFIER`.
* Sửa:
  * Thêm `SET ANSI_NULLS ON`.
  * Thêm `SET QUOTED_IDENTIFIER ON`.

### DB-002 — Seed tiếng Việt bị mojibake

* Nguyên nhân: sqlcmd đọc file UTF-8 bằng code page mặc định.
* Sửa:
  * Ghi rõ phải chạy sqlcmd với `-f 65001`.
  * Tạo lại toàn bộ DB bằng UTF-8.
  * Xác minh chính xác `Hành động` và `Tiếng Việt` tồn tại trong DB.

### MERGE-001 — Mất DI registrations của Manager/refund

* Triệu chứng: Manager dashboard và các API liên quan trả `400` do không resolve được service.
* Sửa: bổ sung lại các service registrations theo cấu trúc `AddInfrastructureServices` của Admin.

### MERGE-002 — RoomService không khớp interface cinema scope

* Triệu chứng: build lỗi `RoomService` không implement `GetRoomsAsync(string?, bool, CancellationToken)`.
* Sửa: giữ logic RoomService Admin và bổ sung bộ lọc cinema scope.

### MERGE-003 — Late payment chưa tạo refund claim

* Triệu chứng: refund được tạo nhưng thiếu claim để Customer cung cấp thông tin nhận tiền.
* Sửa: tích hợp `IRefundClaimIssuer` vào PaymentService Admin và tạo claim cùng refund.

### ADMIN-001 — Create Movie trả lỗi 500

* Nguyên nhân: `ToDetailResponse` đọc `MovieGenre.Genre` trước khi navigation được load.
* Sửa: reload movie bằng `Include(MovieGenres).ThenInclude(Genre)` sau khi lưu.

### TEST-001 — Test fixture chưa theo contract Admin mới

* Đã cập nhật:
  * `IsDurationConfirmed` khi tạo phim.
  * Trạng thái moderation theo logic Admin.
  * Error code webhook theo Admin.
  * Options mới của Room/Movie/Review/Seat services.
  * Payment fixture có showtime hợp lệ, đúng quan hệ FK của DB thật.

## 5. Kết quả database

Script sử dụng:

```powershell
sqlcmd -d master -C -b -f 65001 -i docs\database\cinema-booking-schema.sql
```

Không ghi connection string hoặc mật khẩu vào log/báo cáo.

Kết quả sau reset:

| Hạng mục | Kết quả |
|---|---:|
| Database | `CinemaBookingDB` |
| User tables | 38 |
| ROLE | 4 |
| CINEMA | 2 |
| MOVIE | 4 |
| ROOM | 4 |
| SHOWTIME | 5 |
| SHOWTIME_SEAT | 142 |
| USER | 1 |
| STAFF_PROFILE | 0 |
| Unicode genre verified | Pass |
| Unicode language verified | Pass |

Lưu ý:

* Schema chỉ seed một Customer mẫu.
* Không seed sẵn Admin/Manager/Staff profile.
* Admin cần được tạo qua `DbInitializer` với environment/user secrets phù hợp.

## 6. Build và test

### Build

```powershell
dotnet build CinemaSystem.sln --no-restore
```

Kết quả:

* Pass.
* 0 warning.
* 0 error.

### Automated tests

```powershell
dotnet test CinemaSystem.sln --no-build --no-restore
```

Kết quả:

* Passed: 231
* Failed: 0
* Skipped: 0

Nhóm Manager/cancel/refund riêng:

* Passed: 21
* Failed: 0

## 7. Smoke test backend

* BE URL: `http://localhost:5070`
* Swagger: HTTP 200.
* `GET /api/movies?page=1&limit=10`:
  * `success = true`
  * Số phim đọc từ DB: 4
  * Message: `Movies retrieved successfully.`
* Không có lỗi trong stderr khi khởi động và gọi API.

## 8. Trạng thái cuối

* Merge conflict: đã giải quyết.
* Database: đã đồng nhất và xác minh Unicode.
* Build: pass.
* Test: 231/231 pass.
* Smoke test: pass.
* Commit: chưa thực hiện.
* Push: chưa thực hiện.
