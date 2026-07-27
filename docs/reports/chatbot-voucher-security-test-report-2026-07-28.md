# Báo cáo kiểm thử bảo mật chatbot và voucher

Ngày kiểm tra: 2026-07-28
Thời điểm kết thúc: 01:51:51 (UTC+07:00)
Nhánh: `Tom/ticketscan2-postgres-integration`
HEAD nền: `056d31cb1bb2a75a21f6ff7e1aabbcfd0b20a08b`

## Kết luận

**PASS trong phạm vi source code và automated test.**

- Full regression: **381 passed, 0 failed, 5 skipped, tổng 386 test**.
- Nhóm test bảo mật chatbot/voucher: **11 passed, 0 failed, 0 skipped**.
- `GeminiChatbotService` không còn query bảng voucher trực tiếp.
- Không còn so khớp target bằng `TargetCustomerIds.Contains(...)`.
- Không còn chuỗi `ALL_CUSTOMERS` hoặc `SPECIFIC_CUSTOMERS` hardcode trong logic được kiểm tra.
- Kill switch mẫu đang ở trạng thái fail-closed:
  `Chatbot:ExposePublicVouchers = false`.

Kết quả trên được chạy với toàn bộ thay đổi hiện có trong working tree. Các thay
đổi bảo mật chưa được commit, push hoặc deploy tại thời điểm lập báo cáo.

## Mục tiêu kiểm thử

1. Chatbot chỉ được gửi voucher public hợp lệ sang Gemini.
2. Voucher private, compensation, account-bound, hết hạn hoặc cấu hình mâu
   thuẫn không xuất hiện trong payload Gemini.
3. Customer không thể claim hoặc apply voucher của customer khác.
4. Target customer phải được so khớp chính xác; `CUS_1` không được khớp với
   `CUS_10`.
5. Create/update phải từ chối cấu hình voucher không nhất quán.
6. Voucher refund/compensation phát sinh từ booking phải private và gắn đúng
   customer.
7. Khi kill switch tắt, provider voucher không được gọi và không có mã voucher
   nào được gửi ra ngoài.

## Thành phần được kiểm tra

- `CinemaSystem.Infrastructure/Services/GeminiChatbotService.cs`
- `CinemaSystem.Infrastructure/Services/ChatbotVoucherContextProvider.cs`
- `CinemaSystem.Infrastructure/Services/VoucherAccessPolicy.cs`
- `CinemaSystem.Infrastructure/Services/VoucherService.cs`
- `CinemaSystem.Infrastructure/Services/BookingService.cs`
- `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`
- `CinemaSystem.Infrastructure/Configuration/ChatbotSettings.cs`
- `CinemaSystem.Application/Interfaces/IChatbotVoucherContextProvider.cs`
- `CinemaSystem.Application/Interfaces/IVoucherAccessPolicy.cs`
- `CinemaSystem.Contracts/Chatbot/PublicVoucherChatContext.cs`

## Lệnh và kết quả

### Full regression

```powershell
dotnet test CinemaSystem.sln -m:1 --verbosity minimal
```

Kết quả:

| Chỉ số | Giá trị |
|---|---:|
| Passed | 381 |
| Failed | 0 |
| Skipped | 5 |
| Tổng | 386 |
| Thời gian test | 30 giây |
| Target framework | .NET 8.0 |
| Configuration | Debug |

Restore và build của Domain, Contracts, Application, Infrastructure, API và
Tests đều hoàn thành trước khi test được thực thi.

### Nhóm security regression

```powershell
dotnet test CinemaSystem.sln --no-build -m:1 --verbosity minimal `
  --filter "FullyQualifiedName~VoucherAccessPolicyTests|FullyQualifiedName~GeminiChatbotVoucherDisclosureTests"
```

Kết quả: **11 passed, 0 failed, 0 skipped**, thời gian 4 giây.

Các test đã chạy:

1. `DependencyInjection_ResolvesRealGeminiChatbotService`
2. `AskAsync_SendsOnlyPublicVoucherCodesToGemini`
3. `AskAsync_WhenVoucherExposureDisabled_DoesNotLoadOrSendVoucherCodes`
4. `ValidateAndClaim_PrivateAllCustomersEvenWhenAssigned_AreRejected`
5. `ValidateAndClaim_TargetIdentifierPrefix_DoesNotGrantAccess`
6. `CreateVoucher_PrivateAllCustomers_IsRejected`
7. `CreateVoucher_SpecificCustomerWithoutValidCustomer_IsRejected`
8. `CreateVoucher_PublicCompensationVoucher_IsRejected`
9. `UpdateVoucher_PrivateAllCustomers_IsRejectedAndOriginalIsPreserved`
10. `AssignedPrivateVoucher_CanBeUsedOnlyByAssignedCustomer`
11. `LegacyVoucherWithoutExplicitAudience_FailsClosed`

Test Gemini sử dụng `GeminiChatbotService` thật cùng fake HTTP handler. Toàn bộ
payload gửi sang handler được kiểm tra; không mock toàn bộ `IChatbotService`.

### Kiểm tra tĩnh

| Kiểm tra | Số kết quả |
|---|---:|
| Query voucher trực tiếp trong `GeminiChatbotService` | 0 |
| `TargetCustomerIds.Contains(...)` trong production code | 0 |
| Chuỗi hardcode `"ALL_CUSTOMERS"` trong logic liên quan | 0 |
| Chuỗi hardcode `"SPECIFIC_CUSTOMERS"` trong logic liên quan | 0 |
| Kill switch mẫu đặt `false` | Có |

`git diff --check` trả exit code 0. Git chỉ báo notice chuyển đổi line ending
LF/CRLF; không có whitespace error.

## Các rule đã được chứng minh

Voucher chỉ được disclosure công khai khi đồng thời thỏa mãn:

- trạng thái active;
- `IsPrivate == false`;
- target type là `DomainConstants.VoucherTargetType.AllCustomers`;
- không chứa target customer;
- đã đến ngày bắt đầu và chưa hết hạn;
- chưa đạt usage limit.

Policy dùng chung kiểm soát các luồng disclosure, validate/apply và claim.
Voucher specific chỉ hợp lệ khi customer có assignment trong
`CustomerVoucher` hoặc target identifier khớp chính xác với customer profile,
user hoặc email.

Voucher thiếu audience hoặc có metadata mâu thuẫn bị từ chối theo cơ chế
fail-closed. Create/update cũng từ chối:

- private đi cùng `ALL_CUSTOMERS`;
- specific nhưng không có customer hợp lệ;
- specific nhưng không private;
- public compensation;
- compensation không có customer assignment.

## Xử lý sự cố dữ liệu

Hai mã private quan sát được trong sự cố đã được xoay mã trong database được
backend cấu hình. Mã thay thế không được ghi vào source, test output hoặc báo
cáo này. Assignment và quyền lợi của customer được giữ nguyên.

## Phạm vi chưa kiểm chứng

1. Năm test `PostgresMigrationIntegrationTests` bị skip vì biến
   `POSTGRES_TEST_CONNECTION` chưa được cấu hình tới một PostgreSQL test database
   cô lập:
   - `FreshSchema_MigratesAndEnforcesPostgresContracts`
   - `SeedBankDirectory_MigrationDoesNotInjectReferenceData`
   - `ExistingProductionLegacySchema_IsAdoptedAndUpgraded`
   - `ExistingSchema_WithSameNamedWrongIndex_IsRejectedBeforeAdoption`
   - `ExistingMatchingSchema_IsAdoptedAndRemainsIdempotent`
2. `RUN_REAL_EMAIL_TESTS` không được bật; không gửi SMTP thật trong lần chạy này.
3. Không gọi Gemini API thật; fake HTTP handler được dùng để kiểm tra chính xác
   dữ liệu outbound mà không làm lộ dữ liệu ra dịch vụ ngoài.
4. Chưa deploy backend và chưa chạy browser E2E trên môi trường production sau
   thay đổi.

## Điều kiện triển khai

1. Deploy code với biến môi trường:

   ```text
   Chatbot__ExposePublicVouchers=false
   ```

2. Rà soát và phân loại các voucher legacy. Chỉ voucher thực sự public mới được
   đặt `IsPrivate=false`, `TargetType=ALL_CUSTOMERS` và không có target customer.
3. Sau khi deploy và kiểm tra payload/log an toàn mới chuyển:

   ```text
   Chatbot__ExposePublicVouchers=true
   ```

4. Chạy lại full regression và PostgreSQL migration integration trên staging
   trước khi phát hành production.
