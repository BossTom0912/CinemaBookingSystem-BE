# Báo cáo hotfix: hủy lịch chiếu không tạo hoàn tiền mặt

- Ngày thực hiện: 28/07/2026 (GMT+7)
- Phạm vi: backend CinemaSystem, dữ liệu PostgreSQL production và Railway production
- Nhánh triển khai: `Tom/ticketscan2-postgres-integration`
- Commit mã nguồn đã triển khai: `8c2ddcb00a0b40e45d00dc2c040b189c817c1df7`
- Kết luận: **PASS** trong phạm vi đã kiểm thử. Ứng dụng không crash trong cửa sổ theo dõi sau deploy.

## 1. Hiện tượng và nguyên nhân gốc

Ảnh lỗi cho thấy bốn booking bị hủy nhưng vẫn có refund `PENDING` trị giá 75.000đ. Luồng cũ có hai vấn đề:

1. `ShowtimeCancellationService` chỉ phát voucher bồi thường khi request truyền mã bồi thường tùy chỉnh, nhưng đồng thời vẫn tạo `REFUND`/`REFUND_CLAIM` tiền mặt cho booking đã thanh toán có `TotalAmount > 0`.
2. Voucher bồi thường 100% chỉ bù phần ghế. Khi booking còn F&B trị giá 75.000đ, `TotalAmount` vẫn là 75.000đ; vì vậy lúc hủy lịch chiếu hệ thống vừa phát voucher bồi thường vừa tạo yêu cầu hoàn 75.000đ.
3. Luồng legacy `DELETE /api/showtimes/{id}` cũng âm thầm hủy booking và tạo refund, nhưng không đi qua đầy đủ luồng bồi thường.
4. Các refund này không thể được xác nhận đúng theo luồng thông thường vì booking đã ở trạng thái `CANCELLED`, trong khi endpoint xác nhận hoàn tiền kỳ vọng booking ở `REFUND_PENDING`.

Hành vi trên trái BR-93: rạp chủ động hủy lịch chiếu phải bồi thường bằng voucher, không hoàn tiền mặt/không tạo refund claim. BR-94, BR-96, BR-98, BR-101 và BR-104 tiếp tục được giữ: số voucher theo ghế/combo, không đổi bồi thường ra tiền và khôi phục voucher đã dùng đúng một lần.

## 2. Thay đổi mã nguồn

### Luồng hủy lịch chiếu

- `ShowtimeCancellationService` luôn phát bồi thường cho booking `PAID` khi rạp hủy lịch chiếu.
- Không còn tạo `REFUND`, `REFUND_CLAIM` hoặc link khai báo ngân hàng trong luồng này.
- Các trường refund cũ trong response vẫn được giữ để tương thích API nhưng luôn trả về 0.
- Email hủy lịch chiếu chỉ thông báo voucher bồi thường, không thông báo số tiền/link hoàn tiền.
- Transaction vẫn bao trọn hủy lịch, hủy booking/ticket, khôi phục voucher cũ và phát voucher bồi thường mới.

### Endpoint DELETE cũ

- `DELETE /api/showtimes/{id}` trả `409 SHOWTIME_HAS_BOOKINGS` nếu lịch chiếu có booking.
- Client/admin phải dùng endpoint hủy lịch chiếu chuyên dụng để đảm bảo chính sách bồi thường nhất quán.

### Dọn dữ liệu production

Migration `20260728115000_VoidInvalidShowtimeCancellationRefunds` chỉ tác động bản ghi thỏa đồng thời:

- refund đang `PENDING`;
- claim đang `PENDING_INFO`;
- có `showtimeCancellationId`;
- tồn tại `CANCELLATION_COMPENSATION` cùng `sourceBookingId` và `showtimeCancellationId`.

Migration giữ lịch sử thay vì xóa:

- refund chuyển sang `FAILED` với marker `VOIDED_DUPLICATE_SHOWTIME_CANCELLATION_REFUND_COMPENSATION_ISSUED`;
- claim chuyển sang `REVOKED`;
- token claim chưa dùng được thu hồi;
- không đụng tới refund `SUCCESS`, `PROCESSING` hoặc trường hợp không khớp điều kiện hẹp trên.

### Điều chỉnh test hạ tầng

Full suite phát hiện `NotificationService.MarkAllAsReadAsync` dùng `ExecuteUpdateAsync` không tương thích EF InMemory. Service nay dùng bulk update cho relational provider và tracked update cho InMemory; đây là sửa riêng, không thay đổi nghiệp vụ production.

## 3. Kết quả kiểm thử local

| Hạng mục | Kết quả |
|---|---|
| `dotnet build CinemaSystem.sln --configuration Release -m:1` | PASS, 0 lỗi, 89 cảnh báo nullable có sẵn |
| Test tập trung showtime cancellation + admin CRUD | PASS 24/24 |
| Test tập trung notification InMemory | PASS 1/1 |
| Toàn bộ solution | PASS 380, FAIL 0, SKIP 5, TOTAL 385 |
| `git diff --check` | PASS |
| Sinh SQL cho migration PostgreSQL | PASS; dùng cấu hình giả chỉ trong process, không kết nối DB thật và không ghi secret |

Năm test PostgreSQL integration bị skip vì máy local không có `CINEMA_TEST_POSTGRES_ADMIN_CONNECTION`/database test riêng. Test kiểm tra migration tồn tại trong assembly vẫn chạy và pass.

Regression mới bao gồm trường hợp: booking đã dùng voucher bồi thường 100% cho ghế và còn F&B 75.000đ; khi hủy lịch chiếu thay thế, hệ thống không tạo cash refund/claim, khôi phục voucher cũ và phát bồi thường mới.

## 4. Deploy Railway production

- Railway project: `6ae9df8b-f5ec-43cc-bc08-65a69a791447`
- Service: `afedf757-8258-42a1-9b3e-753601ffe06c`
- Environment: production (`b0302c79-4fe5-4a09-af23-a5a84aa8fe5a`)
- Deployment: `77800807-74ce-44d4-b21c-17cd8cf7e9c9`
- Docker image digest: `sha256:2d3f09fac32c5202762b224b7a0f48dd7ddc8b41409996f533dc7c3005c520de`
- Trạng thái chốt: `Active`

Build log xác nhận `dotnet publish` Release và push image thành công. Deploy log xác nhận:

- migration `20260728115000_VoidInvalidShowtimeCancellationRefunds` được áp dụng;
- `Now listening on: http://[::]:8080`;
- `Application started` trong môi trường Production;
- snapshot log không có `fail:` hoặc `Unhandled exception`.

## 5. Đối soát dữ liệu trước và sau migration

Trước deploy:

- invalid pending refunds: 4;
- pending claims: 4;
- active unused claim tokens: 1;
- migration hotfix chưa được áp dụng.

Sau deploy:

- pending refunds khớp điều kiện: 0;
- refund được void/đổi sang `FAILED`: 4;
- claim được `REVOKED`: 4;
- active unused claim tokens: 0;
- migration hotfix đã có trong history.

Chỉ chạy `SELECT` thủ công trên Neon để đối soát. Toàn bộ thay đổi dữ liệu được thực hiện bởi EF migration lúc app khởi động, không chạy `UPDATE` thủ công.

Neon có point-in-time restore 6 giờ; mốc an toàn trước deploy được ghi nhận khoảng 11:45 GMT+7 ngày 28/07/2026.

## 6. Smoke test và theo dõi crash

Ứng dụng bắt đầu chạy khoảng 11:47:52 GMT+7. Các lần kiểm tra đều trả:

- `/api/health`: HTTP 200;
- `/api/db-test/movies-count`: HTTP 200, `MOVIE` count = 13.

| Thời điểm (GMT+7) | Health | DB-backed endpoint |
|---|---:|---:|
| 11:49:48 | 200 | 200 |
| 11:50:33 | 200 | 200 |
| 11:51:19 | 200 | 200 |
| 11:52:04 | 200 | 200 |
| 11:52:49 | 200 | 200 |
| 11:54:55 | 200 | 200 |

Lần chốt cuối cách thời điểm app start hơn 7 phút, vượt cửa sổ crash cũ khoảng 1–2 phút. Railway vẫn `Active`, không thấy tín hiệu crash trong trạng thái/log được kiểm tra.

## 7. Phần chưa xác minh

- Không chạy năm PostgreSQL integration test local vì thiếu database test riêng.
- Không tạo booking thật rồi hủy trực tiếp trên production để tránh tác động dữ liệu/khách hàng.
- Không gửi email production thật.
- Không xác minh danh sách bằng UI admin sau migration vì phiên đăng nhập trên trình duyệt đã hết hạn; kết quả dọn bốn bản ghi được xác minh trực tiếp bằng dữ liệu PostgreSQL và migration history.

## 8. Trạng thái bàn giao

- Mã nguồn hotfix đã được push lên cả nhánh hotfix và nhánh Railway đang auto-deploy.
- Checkout gốc đang có WIP của người dùng được giữ nguyên; toàn bộ sửa đổi thực hiện trong worktree sạch `C:\tmp\CinemaSystem_BE_refund_hotfix_20260728`.
- Báo cáo này được commit/push riêng trên nhánh hotfix, không đẩy thêm vào nhánh deploy để tránh kích hoạt một deployment chỉ thay đổi tài liệu.
