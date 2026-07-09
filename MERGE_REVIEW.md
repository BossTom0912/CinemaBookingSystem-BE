# Báo cáo review sau merge Manager features

## 1. Phạm vi

- Nhánh: `integration-manager-features`.
- Mốc so sánh: `main...integration-manager-features`.
- Chỉ review các file được thay đổi bởi ba merge:
  - `LimitOfManager`
  - `CancelPerformanceAndGenerateDataRefund`
  - `RevenueAndTicketOverview`
- Không chạy FE, BE, DB, build, unit test, integration test hoặc E2E test.

## 2. Files reviewed

### Application

- `CinemaSystem.Application/Common/BookingConstants.cs`
- `CinemaSystem.Application/Common/CinemaScopeAuthorizationResult.cs`
- `CinemaSystem.Application/Common/PaymentRefundGatewayRequest.cs`
- `CinemaSystem.Application/Common/PaymentRefundGatewayResult.cs`
- `CinemaSystem.Application/Common/RefundClaimIssue.cs`
- `CinemaSystem.Application/Interfaces/ICinemaScopeAuthorizationService.cs`
- `CinemaSystem.Application/Interfaces/IManagerDashboardService.cs`
- `CinemaSystem.Application/Interfaces/IManualRefundService.cs`
- `CinemaSystem.Application/Interfaces/IPaymentRefundGateway.cs`
- `CinemaSystem.Application/Interfaces/IRefundClaimIssuer.cs`
- `CinemaSystem.Application/Interfaces/IRefundClaimService.cs`
- `CinemaSystem.Application/Interfaces/IRefundProcessor.cs`
- `CinemaSystem.Application/Interfaces/IRefundService.cs`
- `CinemaSystem.Application/Interfaces/IRoomService.cs`
- `CinemaSystem.Application/Interfaces/ISensitiveDataProtector.cs`
- `CinemaSystem.Application/Interfaces/IShowtimeCancellationService.cs`

### Contracts

- `CinemaSystem.Contracts/Dashboard/ManagerDashboardQueryRequest.cs`
- `CinemaSystem.Contracts/Dashboard/ManagerDashboardResponse.cs`
- `CinemaSystem.Contracts/Refunds/AssignManualRefundResponse.cs`
- `CinemaSystem.Contracts/Refunds/BankResponse.cs`
- `CinemaSystem.Contracts/Refunds/ManualRefundConfirmationRequest.cs`
- `CinemaSystem.Contracts/Refunds/ManualRefundResponse.cs`
- `CinemaSystem.Contracts/Refunds/RefundClaimResponse.cs`
- `CinemaSystem.Contracts/Refunds/RefundProcessingResponse.cs`
- `CinemaSystem.Contracts/Refunds/RefundQueryRequest.cs`
- `CinemaSystem.Contracts/Refunds/RefundResponse.cs`
- `CinemaSystem.Contracts/Refunds/RequestRefundLinkRequest.cs`
- `CinemaSystem.Contracts/Refunds/ResolveRefundClaimRequest.cs`
- `CinemaSystem.Contracts/Refunds/SaveRefundBankAccountRequest.cs`
- `CinemaSystem.Contracts/Showtimes/CancelShowtimeRequest.cs`
- `CinemaSystem.Contracts/Showtimes/CancelShowtimeResponse.cs`

### Domain

- `CinemaSystem.Domain/Entities/BankDirectory.cs`
- `CinemaSystem.Domain/Entities/CustomerProfile.cs`
- `CinemaSystem.Domain/Entities/CustomerRefundRequest.cs`
- `CinemaSystem.Domain/Entities/ManualRefundProcess.cs`
- `CinemaSystem.Domain/Entities/Refund.cs`
- `CinemaSystem.Domain/Entities/RefundClaim.cs`
- `CinemaSystem.Domain/Entities/RefundClaimToken.cs`
- `CinemaSystem.Domain/Entities/User.cs`

### Infrastructure

- `CinemaSystem.Infrastructure/Auth/CinemaScopeAuthorizationService.cs`
- `CinemaSystem.Infrastructure/CinemaSystem.Infrastructure.csproj`
- `CinemaSystem.Infrastructure/Configuration/RefundSettings.cs`
- `CinemaSystem.Infrastructure/Dashboard/ManagerDashboardService.cs`
- `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`
- `CinemaSystem.Infrastructure/Persistence/CinemaDbContext.cs`
- `CinemaSystem.Infrastructure/Refunds/ManualRefundService.cs`
- `CinemaSystem.Infrastructure/Refunds/RefundClaimService.cs`
- `CinemaSystem.Infrastructure/Refunds/RefundProcessor.cs`
- `CinemaSystem.Infrastructure/Refunds/RefundService.cs`
- `CinemaSystem.Infrastructure/Refunds/UnsupportedPaymentRefundGateway.cs`
- `CinemaSystem.Infrastructure/Rooms/RoomService.cs`
- `CinemaSystem.Infrastructure/Security/RefundClaimIssuer.cs`
- `CinemaSystem.Infrastructure/Security/SensitiveDataProtector.cs`
- `CinemaSystem.Infrastructure/Services/PaymentService.cs`
- `CinemaSystem.Infrastructure/Services/SeatService.cs`
- `CinemaSystem.Infrastructure/Showtimes/ShowtimeCancellationService.cs`
- `CinemaSystem.Infrastructure/Showtimes/ShowtimeService.cs`

### API

- `CinemaSystem/Controllers/AdminRefundsController.cs`
- `CinemaSystem/Controllers/CustomerRefundClaimsController.cs`
- `CinemaSystem/Controllers/ManagerDashboardController.cs`
- `CinemaSystem/Controllers/ManagerRefundsController.cs`
- `CinemaSystem/Controllers/RoomsController.cs`
- `CinemaSystem/Controllers/SeatsController.cs`
- `CinemaSystem/Controllers/ShowtimeCancellationsController.cs`
- `CinemaSystem/Controllers/ShowtimesController.cs`
- `CinemaSystem/appsettings.Development.example.json`

### Tests

- `CinemaSystem.Tests/ControllerMoqCoverageTests.cs`
- `CinemaSystem.Tests/Infrastructure/CinemaScopeTestData.cs`
- `CinemaSystem.Tests/Infrastructure/NoOpRefundProcessor.cs`
- `CinemaSystem.Tests/ManagerCinemaScopeApiIntegrationTests.cs`
- `CinemaSystem.Tests/ManagerDashboardApiIntegrationTests.cs`
- `CinemaSystem.Tests/MissingCoverageTests.cs`
- `CinemaSystem.Tests/PaymentServiceTests.cs`
- `CinemaSystem.Tests/RefundProcessorTests.cs`
- `CinemaSystem.Tests/RoomShowtimeApiIntegrationTests.cs`
- `CinemaSystem.Tests/RoomShowtimeServiceTests.cs`
- `CinemaSystem.Tests/SeatCrudApiIntegrationTests.cs`
- `CinemaSystem.Tests/ShowtimeCancellationApiIntegrationTests.cs`

### Documentation và database artifacts

- `docs/README.md`
- `docs/architecture/backend-system-design-clean-architecture.docx`
- `docs/architecture/conceptual-erd-explanation.docx`
- `docs/database/cinema-booking-schema.sql`
- `docs/database/customer-assisted-refund-patch.txt`
- `docs/reports/SCRUM-190-manager-cinema-scope.md`
- `docs/reports/SCRUM-192-cancel-showtime-refund.md`
- `docs/reports/SCRUM-193-customer-assisted-refund.md`
- `docs/reports/SCRUM-195-manager-revenue-ticket-overview.md`
- `docs/requirements/business-rules.docx`
- `docs/requirements/srs-group-2.docx`
- `docs/testing/manual-refund-api-test-guide.md`

## 3. Hardcoded values found

| Nhóm | Giá trị tìm thấy | Vị trí ban đầu | Đánh giá |
|---|---|---|---|
| URL | `http://localhost:5173` | `RefundSettings`, DI và file config example | URL môi trường không được rải trong service/DI. |
| URL path | `/refunds/claim` | Hai service tạo email claim | Route contract bị lặp. |
| Role/status | `ACTIVE` | Cinema scope và cancellation service | Trùng với status đã có trong `BookingConstants`. |
| Entity ID prefix | `RFC`, `RFT`, `SHC`, `REF`, `NOT`, `AUD`, `RPT`, `MRP`, `CRR` | Các refund/cancellation service | Prefix dữ liệu bị lặp ở nhiều service. |
| Refund workflow | `SUCCESS`, `PENDING`, `MANUAL_REQUIRED`, `AWAITING_CUSTOMER_INFO`, `FULFILLED` | Refund query/claim service | Trùng status nghiệp vụ và dễ lệch schema. |
| Audit identifiers | `CANCEL_SHOWTIME`, `PROCESS_REFUND`, `SUBMIT_REFUND_CLAIM`, `REISSUE_REFUND_CLAIM_LINK`, `ASSIGN_MANUAL_REFUND`, `CONFIRM_MANUAL_REFUND` | Refund/cancellation services | Identifier audit phải thống nhất. |
| Audit entity | `SHOWTIME`, `REFUND`, `REFUND_CLAIM` | Refund/cancellation services | Identifier entity audit bị lặp. |
| Refund limits | `3`, `4`, `5`, `6`, `20`, `50`, `255`, `500`, `1000` | Data annotations và service validation | Là giới hạn contract/schema nhưng bị rải rác. |
| Refund amount | `0.01`, `9999999999999999` | Manual refund request | Biên `DECIMAL(18,2)` bị đặt trực tiếp trong attribute. |
| Token security | `32` bytes | `RefundClaimIssuer` | Entropy token là policy bảo mật, không nên là số vô danh. |
| Dashboard | `100m`, `2` chữ số | Tính occupancy rate | Magic number của phép tính ticket overview. |
| Masking | `******`, lấy `4` số cuối | Refund response/claim service | Quy tắc che tài khoản bị lặp. |
| Error code | `UNAUTHORIZED` | Ba controller mới | Đã có constant dùng chung. |
| Connection string | Chuỗi SQL mẫu | `appsettings.Development.example.json` | Chỉ là template cấu hình, không phải connection string runtime. |
| Test IDs/URLs | `CIN_*`, `USR_*`, `SHW_*`, URL proof mẫu | Các integration test | Fixture định danh, không đi vào production. |

Không phát hiện role name production mới bị hardcode. Các controller/service sử dụng
`AuthConstants.Roles`, `AuthConstants.RoleIds` và authorization policies.

Không phát hiện connection string thật, mật khẩu, JWT secret hoặc API key thật được thêm
bởi ba merge.

## 4. Values refactored

### Application constants

`BookingConstants` được mở rộng để quản lý tập trung:

- refund workflow statuses;
- refund token entropy;
- dashboard label, percentage multiplier và số chữ số làm tròn;
- entity ID prefixes;
- customer refund request statuses;
- audit actions và audit entity names.

### Contract constants

Thêm `CinemaSystem.Contracts/Refunds/RefundContractConstants.cs` để quản lý:

- độ dài ID;
- giới hạn transaction code, proof URL, note và request reason;
- giới hạn claim token;
- giới hạn bank code, account number và account holder;
- regex account number;
- số chữ số tài khoản được phép hiển thị;
- quy tắc mask;
- giới hạn cancellation reason và failure reason;
- biên số tiền phù hợp `DECIMAL(18,2)`.

Các request DTO refund/showtime đã chuyển sang sử dụng constants này.

### Configuration

`RefundSettings` hiện là nguồn duy nhất cho:

- development frontend URL;
- claim route;
- default/minimum claim-token lifetime.

DI không còn lặp trực tiếp URL hoặc số `5`; mọi giá trị vẫn có thể override bằng
`RefundSettings:FrontendBaseUrl` và `RefundSettings:ClaimTokenMinutes`.

### Infrastructure

Đã thay các giá trị rải rác trong:

- cinema scope authorization;
- manager dashboard calculation;
- refund query projection;
- refund claim issuing;
- manual refund;
- automatic refund processor;
- showtime cancellation;
- audit creation;
- account masking và failure-reason truncation.

Ba controller refund/cancellation dùng lại
`BookingConstants.ErrorCodes.Unauthorized`.

## 5. Values retained with justification

### `http://localhost:5173`

Giữ đúng một named constant `RefundSettings.DevelopmentFrontendBaseUrl` và một giá trị
trong `appsettings.Development.example.json`.

Lý do:

- đây là fallback dành riêng cho local development;
- các test hiện có khởi tạo `RefundSettings` trực tiếp;
- production có thể và phải override bằng configuration/environment variable;
- đây không phải secret hoặc production endpoint.

### `ClaimTokenMinutes = 5`

Giữ dưới tên `RefundSettings.DefaultClaimTokenMinutes`.

Lý do: SRS và business rule quy định link refund claim có hiệu lực 5 phút. Giá trị vẫn
cho phép override qua configuration khi policy được thay đổi chính thức.

### API route strings

Các route như `api/manager/refunds`, `api/admin/refunds` và
`api/manager/showtimes` được giữ trong attributes.

Lý do: đây là public HTTP contract và ASP.NET Core yêu cầu route template tại endpoint;
chúng không phải URL môi trường.

### HTTP status numbers

Các giá trị `400`, `401`, `403`, `404`, `409`, `410` được giữ trong
`ServiceResult`.

Lý do: đây là mã HTTP chuẩn, không phải Manager/refund limit. Chuyển sang
`Microsoft.AspNetCore.Http.StatusCodes` trong Infrastructure/Application sẽ làm layer
nghiệp vụ phụ thuộc vào ASP.NET Core, trái với Clean Architecture hiện tại.

### EF Core mapping lengths và column names

Các số trong `CinemaDbContext`, ví dụ `HasMaxLength(50)` hoặc
`decimal(18,2)`, được giữ.

Lý do: đây là database-first mapping phản ánh schema SQL, không phải runtime policy.
Thay chúng bằng config sẽ làm mapping lệch database.

### Test fixture IDs, dates, amounts và URLs

Các ID `CIN_*`, `USR_*`, `SHW_*`, thời gian, số tiền và HTTPS proof URL trong test được
giữ.

Lý do: test cần dữ liệu cố định, dễ đọc và tái lập. Chúng không được dùng trong
production và không chứa credential thật.

### Connection string mẫu

Chuỗi trong `appsettings.Development.example.json` được giữ.

Lý do: đây là template có placeholder `YOUR_LOCAL_DB_PASSWORD`; runtime vẫn đọc
`ConnectionStrings:DefaultConnection` từ file local, user secrets hoặc environment.
Không có connection string thật được commit.

### Refund account-number regex

Regex `^[0-9]{6,20}$` được giữ dưới một compile-time constant.

Lý do: Data Annotation yêu cầu biểu thức cố định; giới hạn 6-20 là contract nghiệp vụ,
không phải cấu hình môi trường.

### `All cinemas`

Giữ dưới `BookingConstants.ManagerDashboard.AllCinemasLabel`.

Lý do: đây là nhãn response cho Admin có system scope, không phải role name hoặc ID.
Đưa vào config không tạo giá trị vận hành; nếu FE cần đa ngôn ngữ thì nên trả `null`
cinema và dịch nhãn ở FE trong một task riêng.

### `UnsupportedPaymentRefundGateway`

Giữ nguyên adapter.

Lý do: dự án chưa có hợp đồng payout/refund chính thức của provider. Adapter này ngăn
hệ thống ghi nhận hoàn tiền thành công giả và chuyển workflow sang manual processing.

## 6. Trạng thái xác minh

- Đã kiểm tra diff tĩnh và loại bỏ hardcode production thuộc phạm vi yêu cầu.
- Chưa chạy compiler hoặc bất kỳ test nào theo yêu cầu Task 2.
- Build/test phải được thực hiện ở task tiếp theo trước khi merge nhánh này vào nhánh
  phát hành.
