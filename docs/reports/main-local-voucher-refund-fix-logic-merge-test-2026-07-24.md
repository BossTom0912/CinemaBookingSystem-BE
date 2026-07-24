# Báo cáo merge `voucher_refund_fixLogic` vào `main_local`

Ngày kiểm tra: 2026-07-24

Nhánh đích: `main_local` tại `2747a351c774026a080d70d753dabf9ac4419117`

Nhánh nguồn: `origin/voucher_refund_fixLogic` tại `7da70c24e5c20289bb64f6c590e293691ef430ab`

## Kết luận nhanh

- Đã lấy nhánh remote mới nhất vì nhánh local `voucher_refund_fixLogic` tại `53d41a6` đã cũ và đã nằm hoàn toàn trong `main_local`.
- Merge có 2 conflict tại `DatabaseMaintenanceService.cs` và `BookingService.cs`; cả hai đã được xử lý thủ công.
- Đã loại migration `20260723000000_EnsureVoucherColumnsExist.cs` bị trùng với migration an toàn đang có trên `main_local`.
- Build Release thành công: **0 error, 90 warning**.
- Toàn bộ test thành công: **343 passed, 0 failed, 0 skipped**.
- Merge phù hợp để tiếp tục kiểm tra trên `main_local`, nhưng **chưa nên đưa vào `main` chính** trước khi xử lý phạm vi phân quyền của module notification và quyết định chính sách migrate DB ở production.

## Phạm vi thay đổi chính

- Voucher: bổ sung bộ lọc/đối tượng áp dụng, voucher riêng tư, số vé yêu cầu và các test tương ứng.
- Refund/cancellation: sửa giải phóng ghế, xóa ticket/booking-seat đúng thứ tự quan hệ và giữ luồng hoàn voucher/đền bù.
- Notification: bổ sung heartbeat, lọc người nhận, gửi theo nhóm, sửa/xóa notification và metadata phản hồi.
- Email: truyền tên khách hàng động vào template và mở rộng nội dung voucher.
- Showtime/F&B/refund admin: đồng bộ các sửa nghiệp vụ từ nhánh nguồn.
- VNPAY: nhánh nguồn đã có merge VNPAY; bản xử lý giữ nguyên logic VNPAY hiện tại trên `main_local`.

## Xử lý conflict và điều chỉnh khi merge

### `DatabaseMaintenanceService.cs`

Giữ phiên bản `main_local`. Khác biệt chỉ là comment; logic backfill voucher hiện tại không cần thay đổi.

### `BookingService.cs`

Giữ cấu trúc hiện tại của `main_local`, sau đó đưa vào đúng phần sửa nghiệp vụ từ commit refund:

- Ngắt navigation `ShowtimeSeat.BookingSeat`, `BookingSeat.Ticket` và `BookingSeat.ShowtimeSeat` trước khi xóa.
- Gom ticket rồi dùng `RemoveRange` để tránh lỗi thứ tự khóa ngoại.
- Áp dụng cùng cách giải phóng cho booking hết hạn và khách từ chối đổi giờ chiếu.
- Truyền `customerName` vào email đổi ghế.
- Không trả chi tiết exception nội bộ ra response HTTP; chi tiết vẫn được ghi log.

### Migration voucher trùng

Không nhận `20260723000000_EnsureVoucherColumnsExist.cs` từ nhánh nguồn vì:

- `main_local` đã có `20260723000000_EnsureVoucherPromotionColumns.cs` cùng chức năng.
- Migration nguồn đặt `ALTER TABLE` và `UPDATE` trong cùng SQL batch, có thể lỗi compile trên SQL Server khi cột chưa tồn tại.
- Migration hiện tại tách riêng DDL và backfill, có kiểm tra bảng/cột và là forward-only để tránh mất dữ liệu.

### Test gửi SMTP thật

Hai test SMTP thật từng tự chạy khi máy có file `appsettings.Development.json` bị Git ignore, làm full suite phụ thuộc mạng ngoài. Đã yêu cầu opt-in rõ ràng:

```powershell
$env:RUN_REAL_EMAIL_TESTS='true'
dotnet test CinemaSystem.sln --filter EmailSystemBusinessRulesTests
```

Mặc định, full suite không gửi email thật.

## Kết quả kiểm thử

### Baseline trước merge

Trên `main_local` sau merge VNPAY (`2747a35`):

- Tổng: 336
- Passed: 331
- Failed: 5
- Nhóm lỗi: 4 test cancellation/compensation-refund và 1 test `RoomShowtimeService` phụ thuộc thời điểm.

### Sau merge

Lệnh đã chạy:

```powershell
dotnet restore CinemaSystem.sln
dotnet build CinemaSystem.sln --no-restore --configuration Release --no-incremental -m:1
dotnet test CinemaSystem.sln --no-build --no-restore --configuration Release -m:1 --logger "console;verbosity=minimal"
```

Kết quả cuối trên checkout thật:

- Restore: thành công.
- Build: thành công, 0 error, 90 warning nullable hiện hữu.
- Test: **343/343 passed**, 0 failed, 0 skipped, khoảng 29 giây.
- `git diff --cached --check`: sạch.
- Không còn conflict marker hoặc file unmerged.

## Phát hiện còn tồn tại trước khi merge vào `main`

### Major — Notification chưa giới hạn theo phạm vi rạp

Các endpoint gửi/lọc/sửa/xóa notification cho phép Manager thao tác trên dữ liệu toàn hệ thống theo role hoặc ID. Service chưa dùng cinema scope để giới hạn nhân viên, booking, phòng và suất chiếu thuộc rạp của Manager. Điều này có thể làm lộ email/trạng thái online hoặc cho phép tác động notification ngoài rạp phụ trách.

Khuyến nghị: truyền danh tính người gọi vào mọi use case quản trị notification và áp dụng `ICinemaScopeAuthorizationService`/cinema scope trước khi query hoặc mutate.

### Major — Tự migrate DB khi khởi động mọi môi trường

`Program.cs` gọi `DatabaseMaintenanceService.MigrateAsync()` ngoài điều kiện Development và chỉ log warning nếu thất bại. Production có thể tiếp tục chạy với schema chưa đồng bộ.

Khuyến nghị: quyết định rõ một trong hai chính sách: migration bắt buộc và fail-fast khi startup, hoặc migration riêng trong deployment pipeline; không nên âm thầm chạy tiếp với schema lỗi.

### Minor — Heartbeat chỉ lưu trong memory của một process

`UserHeartbeatTracker` dùng `ConcurrentDictionary` trong singleton. Trạng thái online mất khi restart và không đồng bộ giữa nhiều instance. Nếu production scale-out, nên chuyển sang Redis hoặc shared store có TTL.

### Minor — Cảnh báo nullable

Build còn 90 warning, chủ yếu CS8601/CS8602/CS8604. Không chặn merge kiểm thử này nhưng nên giảm dần, ưu tiên refund, booking và notification vì đây là các luồng vừa thay đổi.

## Quyết định

- **Đã đạt:** merge vào nhánh kiểm thử `main_local`, build và full regression test.
- **Chưa đạt:** đưa thẳng lên `main` production cho đến khi xử lý hoặc chấp nhận có chủ đích hai vấn đề Major ở trên.
- Không push remote trong lần thực hiện này.
