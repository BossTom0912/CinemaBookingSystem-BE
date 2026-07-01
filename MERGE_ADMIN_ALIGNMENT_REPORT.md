# MERGE & ADMIN ALIGNMENT REPORT

## 1. Branch Summary

- Target branch: `Tom/integration-main-manager-admin-test`
- Admin reference branch: `Admin` tại `a5c9bc5`
- Feature branch merged: `integration-manager-features` tại `7ac9f02`
- Admin merge commit: `4b2ddec`
- Feature merge commit: `b092928`
- Không push, force-push hoặc `reset --hard`.

## 2. Merge Result

- Admin merge status: thành công. Local `Admin` đã fast-forward theo remote trước khi merge.
- Feature merge status: thành công. Cây nội dung của feature đã có sẵn phần lớn trên target nên merge commit không tạo thêm net diff so với first parent.
- Conflicts found khi merge Admin:
  - `CinemaSystem.Domain/Constants/DomainConstants.cs`
  - `CinemaSystem.Infrastructure/Services/AdminRefundService.cs`
  - `CinemaSystem.Infrastructure/Services/ReviewService.cs`
  - `CinemaSystem.Infrastructure/Showtimes/ShowtimeService.cs`
  - `CinemaSystem/Program.cs`
- Conflicts found khi merge feature:
  - `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`
  - `CinemaSystem/Controllers/RoomsController.cs`
  - `CinemaSystem/Controllers/SeatsController.cs`
  - `CinemaSystem/Controllers/ShowtimesController.cs`
- Conflicts resolved:
  - Giữ `DomainConstants.AuditAction` và `DomainConstants.AuditEntity` theo cách tổ chức của Admin.
  - Giữ `app.UseStaticFiles()` và pipeline hiện tại của Admin trong `Program.cs`.
  - Giữ chữ ký API Admin mới hơn cho Room/Seat/Showtime, gồm `includeInactive`, actor ID và cờ `force`.
  - Không khôi phục `ICheckoutService` cũ vì target/Admin đang dùng `IBookingService`; tránh đăng ký DI trùng hoặc service đã lỗi thời.
  - Các conflict chỉ khác whitespace trong Admin services được giải quyết mà không thay đổi business logic Admin.
  - Sáu conflict khi áp lại refactor hardcode được giải quyết bằng cách dùng constant của Admin/Domain thay vì tạo bộ audit constant trùng lặp.

## 3. Admin Alignment Review

### Files aligned with Admin pattern

- Controllers:
  - `AdminRefundsController`
  - `ManagerRefundsController`
  - `ManagerDashboardController`
  - `ShowtimeCancellationsController`
  - `RoomsController`
  - `SeatsController`
  - `ShowtimesController`
- Application abstractions và contracts:
  - `ICinemaScopeAuthorizationService`
  - `IManagerDashboardService`
  - `IRefundService`
  - `IRefundClaimService`
  - `IManualRefundService`
  - `IRefundProcessor`
  - Các DTO trong `CinemaSystem.Contracts/Refunds` và `CinemaSystem.Contracts/Dashboard`
- Infrastructure/DI:
  - `CinemaScopeAuthorizationService`
  - `ManagerDashboardService`
  - `ShowtimeCancellationService`
  - Các refund service và đăng ký trong `DependencyInjection`
- Authorization dùng `AuthConstants.Roles`/policy; controller giữ mỏng và trả `ApiResponse`.
- Database access của Manager/refund nằm trong Infrastructure qua `CinemaDbContext`; controller không query DB trực tiếp.

### Files still inconsistent

- `AdminRefundService` và bộ `ShowtimeCancellationService`/`RefundProcessor` cùng xử lý hủy suất chiếu và hoàn tiền theo hai workflow. Đây là overlap nghiệp vụ, không phải đăng ký DI trùng, nhưng cần hợp nhất use case ở task riêng.
- `ShowtimeService` còn luồng hủy/refund legacy bên cạnh `ShowtimeCancellationService`, gồm prefix cancellation legacy `STC` trong khi workflow mới dùng `SHC`.
- Trạng thái booking/refund hiện có ở cả `DomainConstants` và `BookingConstants`. Phần merge dùng `DomainConstants` cho audit/entity theo Admin và `BookingConstants` cho workflow booking mới, nhưng vẫn có nguy cơ lệch giá trị về sau.
- `UnsupportedPaymentRefundGateway` cố ý chuyển refund sang manual vì chưa có adapter payout thực của nhà cung cấp.

### Risky areas

- Hai đường hủy/refund có thể tạo bản ghi hoặc chuyển trạng thái khác nhau tùy endpoint gọi.
- Schema cần đủ các bảng/cột refund claim, manual refund, customer refund request và audit; task này không chạy DB nên chưa xác nhận migration trên DB thật.
- Manager cinema scope phụ thuộc `STAFF_PROFILE.CinemaId`; dữ liệu profile thiếu sẽ trả forbidden.
- File local `appsettings.Development.json` bị ignore và không được sửa/commit, nhưng có cấu hình nhạy cảm cục bộ; phải tiếp tục giữ ngoài Git.
- So sánh `Admin...HEAD` còn chứa nhiều thay đổi có sẵn từ target trước task (docs, test và build artifacts); chúng không được tạo bởi hai merge commit của task này.

## 4. Hardcode Audit

### Values refactored

| File | Line/function | Value | Type of hardcode | Action taken |
|---|---|---|---|---|
| `CinemaSystem.Application/Common/BookingConstants.cs` | `RefundPolicy`, `ManagerDashboard`, `EntityIdPrefix` | `32`, `100m`, `2`, các prefix refund/audit | Security policy, dashboard magic numbers, IDs | Gom thành named constants. |
| `CinemaSystem.Contracts/Refunds/RefundContractConstants.cs` | Toàn file | Giới hạn `2..1000`, amount bounds, regex, mask | Refund contract/schema limits | Tạo contract constants và dùng lại trong DTO/service. |
| `CinemaSystem.Infrastructure/Configuration/RefundSettings.cs` | `RefundSettings` | localhost URL, `/refunds/claim`, `5`, `1` | URL/path/config thresholds | Gom thành named defaults; runtime vẫn override qua configuration. |
| `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs` | `AddInfrastructureServices` | localhost URL và claim lifetime | Config hardcode | Dùng `RefundSettings`, thêm validate minimum. |
| `CinemaSystem.Infrastructure/Dashboard/ManagerDashboardService.cs` | occupancy calculation | `100m`, `2`, `All cinemas` | Revenue/ticket overview magic values | Dùng `BookingConstants.ManagerDashboard`. |
| `CinemaSystem.Infrastructure/Security/RefundClaimIssuer.cs` | `Create` | `32`, `RFC`, `RFT`, minimum lifetime | Token/ID/refund policy | Dùng named constants. |
| `CinemaSystem.Infrastructure/Refunds/*` | ID creation, workflow projection, masking, audit | `REF`, `NOT`, `AUD`, `RPT`, status/audit strings, `******`, suffix `4` | Refund IDs/status/masking | Dùng `BookingConstants`, `DomainConstants` và `RefundContractConstants`. |
| `CinemaSystem.Infrastructure/Showtimes/ShowtimeCancellationService.cs` | cancel/refund/audit/email | `SHC`, `REF`, `NOT`, `AUD`, route, reason/failure length | Cancellation/refund hardcode | Dùng constants/config tập trung. |
| `CinemaSystem.Infrastructure/Services/AdminRefundService.cs` | tạo refund | `REF` | Refund ID prefix | Dùng `BookingConstants.EntityIdPrefix.Refund`. |
| `CinemaSystem.Infrastructure/Services/BookingService.cs` | tạo refund | `REF` | Refund ID prefix | Dùng `BookingConstants.EntityIdPrefix.Refund`. |
| `CinemaSystem.Infrastructure/Services/PaymentService.cs` | late-payment refund/email | `REF`, `/refunds/claim` | Refund ID và URL path | Dùng `EntityIdPrefix` và `RefundSettings.ClaimRoute`. |
| `CinemaSystem.Infrastructure/Showtimes/ShowtimeService.cs` | legacy refund creation | `REF` | Refund ID prefix | Dùng `BookingConstants.EntityIdPrefix.Refund`. |
| `CinemaSystem/Controllers/AdminRefundsController.cs` | `GetRefunds` | `PENDING`, `1`, `10`, `UNAUTHORIZED` | Status/paging/error code | Dùng status/error constants và named paging defaults. |
| `CinemaSystem/Controllers/CustomerRefundClaimsController.cs` | unauthorized branch | `UNAUTHORIZED` | Error code | Dùng `BookingConstants.ErrorCodes.Unauthorized`. |
| `CinemaSystem/Controllers/ShowtimeCancellationsController.cs` | unauthorized branch | `UNAUTHORIZED` | Error code | Dùng `BookingConstants.ErrorCodes.Unauthorized`. |

Không phát hiện role name, user/cinema/room/movie/showtime ID cụ thể, connection string thật hoặc Manager numeric limit mới bị hardcode trong phần code audit. Role production dùng `AuthConstants`.

### Values retained with justification

| File | Line/function | Value | Type of hardcode | Reason retained |
|---|---|---|---|---|
| `RefundSettings.cs`, `appsettings.Development.example.json` | development fallback | `http://localhost:5173` | URL | Chỉ là fallback/template local, không phải production endpoint; production phải override qua config/environment. |
| `RefundSettings.cs` | default lifetime | `5` phút | Refund policy | Business rule hiện tại yêu cầu claim link tồn tại 5 phút; được đặt tên và có thể override. |
| `RefundContractConstants.cs` | validation constants | amount/length/regex values | Contract/schema | Phản ánh `DECIMAL(18,2)` và độ dài cột/request; không phải config môi trường. |
| Controllers | route attributes | `api/admin/...`, `api/manager/...` | HTTP route | Là public API contract, phải là compile-time route template. |
| Services | `400`, `401`, `403`, `404`, `409`, `410` | HTTP result codes | Protocol constants | Là mã HTTP chuẩn; chuyển Infrastructure sang phụ thuộc ASP.NET `StatusCodes` sẽ làm sai hướng dependency hiện tại. |
| `AdminRefundsController` | `1`, `10` qua named constants | Pagination defaults | API default | Giữ tương thích Admin API; đã loại bỏ số vô danh khỏi signature. |
| `BookingConstants.ManagerDashboard` | `All cinemas` | Response label | Display contract | Nhãn system scope hiện tại; nếu cần đa ngôn ngữ nên đổi response contract/FE ở task riêng. |
| `appsettings.Development.example.json` | sample SQL string | placeholder connection string | Documentation config | Chỉ là template có placeholder, không chứa credential thật và runtime vẫn đọc configuration. |

## 5. Build/Test Result

- Command run: `dotnet build CinemaSystem.sln --no-restore`
- Result: thành công, `0` error, `3` nullable warnings.
- Warnings:
  - `CinemaSystem.Tests/CinemaApiIntegrationTests.cs:35`
  - `CinemaSystem.Tests/ReviewServiceTests.cs:152`
  - `CinemaSystem.Tests/RoomShowtimeServiceTests.cs:180`
- Command run: `dotnet test CinemaSystem.sln --no-build --no-restore`
- Result: thành công, `231/231` passed, `0` failed, `0` skipped.
- Không chạy FE, DB live hoặc E2E.

## 6. Next Recommended Step

1. Review và commit riêng phần hardcode refactor/report đang để uncommitted.
2. Hợp nhất workflow legacy `AdminRefundService`/`ShowtimeService` với workflow `ShowtimeCancellationService`/`RefundProcessor` trước khi test DB/E2E.
3. Sau khi được duyệt, chạy migration/schema verification rồi mới chạy FE + BE + E2E cho Manager/Admin.

