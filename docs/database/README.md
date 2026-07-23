# Database scripts

Thư mục này có **một script schema chuẩn**:
[`cinema-booking-schema.sql`](cinema-booking-schema.sql). Không chạy patch
feature rời hoặc copy từng đoạn SQL từ báo cáo cũ.

Script xóa toàn bộ database `CinemaBookingDB`, sau đó tạo lại đầy đủ bảng,
khóa ngoại, constraint, index và seed chuẩn. Không dùng trên shared, staging
hoặc production database nếu chưa có backup và phê duyệt xóa dữ liệu.

Database đang có dữ liệu cần giữ không được chạy script này. Hãy dùng một data
migration được review riêng cho đúng trạng thái database đó.

## Chạy bằng SQL Server

Chạy từ thư mục gốc repository. Tùy môi trường, thay `-E` bằng thông tin xác
thực SQL Server phù hợp.

### Reset toàn bộ database local/demo

```powershell
sqlcmd -S . -E -b -f 65001 -i "docs\database\cinema-booking-schema.sql"
```

Script tạo lại `CinemaBookingDB`, toàn bộ bảng, khóa ngoại, index, constraint
và seed chuẩn; bao gồm checkout, ticket scan, customer voucher,
refund/manual-refund, banner, F&B fulfillment và cancellation-compensation
voucher.

Sau khi chạy, xác minh nhanh:

```sql
SELECT [name]
FROM sys.tables
WHERE [name] IN
(
    N'BANNER',
    N'CUSTOMER_VOUCHER',
    N'REFUND_CUSTOMER_CONFIRMATION',
    N'CANCELLATION_COMPENSATION',
    N'COMPENSATION_TICKET',
    N'COMPENSATION_COMBO'
)
ORDER BY [name];
```

## Development fixtures

Các file dưới đây không phải schema deployment. Chỉ chạy có chủ đích trên
database local/test, sau khi schema đã tồn tại:

| File | Dùng để |
|---|---|
| `dev-seed-admin-manager-staff.txt` | Tạo tài khoản admin/manager/staff để kiểm thử. |
| `dev-seed-paid-ticket-ready-to-scan.txt` | Tạo booking đã thanh toán và ticket sẵn sàng quét. |
| `dev-seed-10-movies-booking-payment-qr.txt` | Tạo dữ liệu phim/booking/thanh toán phục vụ luồng khách hàng. |
| `dev-seed-voucher-compensation-flow.txt` | Tạo suất 2D, IMAX và VIP cho luồng đặt vé → hủy suất → voucher bồi thường. |

Ví dụ chạy fixture bồi thường:

```powershell
sqlcmd -S . -d CinemaBookingDB -E -b -f 65001 -i "docs\database\dev-seed-voucher-compensation-flow.txt"
```

Các fixture được viết để rerun trong phạm vi dữ liệu định danh của chúng. Dù
vậy, không chạy chúng vào production.

## Quy tắc bảo trì

- Thêm thay đổi schema mới vào `cinema-booking-schema.sql`; không tạo patch
  feature rời.
- Seed fixture test phải giữ tách khỏi script schema chuẩn.
- Khi một patch feature đã được gộp vào script chuẩn, xóa patch rời để tránh
  người khác chạy trùng hoặc dùng schema cũ.
