# Database scripts

Thư mục này chỉ có hai script schema chuẩn. Không chạy các patch feature rời
hoặc copy từng đoạn SQL từ báo cáo cũ.

## Chọn đúng script

| Trường hợp | Script | Ảnh hưởng dữ liệu |
|---|---|---|
| Tạo mới database local/demo | [`cinema-booking-schema.sql`](cinema-booking-schema.sql) | **Xóa toàn bộ database `CinemaBookingDB` hiện có**, sau đó tạo lại schema và seed chuẩn. |
| Nâng cấp database đang dùng | [`cinema-booking-schema-upgrade.sql`](cinema-booking-schema-upgrade.sql) | Giữ dữ liệu nghiệp vụ; chỉ thêm/sửa schema theo các guard idempotent và thêm dữ liệu tham chiếu khi thiếu. |

> Không dùng script reset trên shared, staging hay production database nếu chưa
> có backup và phê duyệt xóa dữ liệu.

## Chạy bằng SQL Server

Chạy từ thư mục gốc repository. Tùy môi trường, thay `-E` bằng thông tin xác
thực SQL Server phù hợp.

### 1. Reset toàn bộ database local

```powershell
sqlcmd -S . -E -b -f 65001 -i "docs\database\cinema-booking-schema.sql"
```

Script tạo lại `CinemaBookingDB`, toàn bộ bảng, khóa ngoại, index, constraint
và seed tham chiếu mặc định.

### 2. Nâng cấp, không xóa dữ liệu

```powershell
sqlcmd -S . -d CinemaBookingDB -E -b -f 65001 -i "docs\database\cinema-booking-schema-upgrade.sql"
```

Script có guard `IF OBJECT_ID`, `COL_LENGTH` và kiểm tra index/constraint để
có thể chạy lại. Nó bao gồm các thay đổi đang được hỗ trợ như checkout,
ticket scan, customer voucher, refund/manual-refund, banner, F&B fulfillment
và cancellation-compensation voucher.

Sau khi chạy, xác minh nhanh:

```sql
SELECT [name]
FROM sys.tables
WHERE [name] IN
(
    N'BANNER',
    N'CUSTOMER_VOUCHER',
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

- Thêm thay đổi schema mới vào **cả hai** script chuẩn khi thay đổi hỗ trợ cả
  database mới lẫn database đang tồn tại.
- Script upgrade không được chứa `DROP DATABASE`, `DROP TABLE`, `DELETE` hay
  `TRUNCATE` dữ liệu nghiệp vụ.
- Seed reference phải idempotent; seed fixture test phải giữ tách khỏi script
  upgrade.
- Khi một patch feature đã được gộp vào hai script chuẩn, xóa patch rời để
  tránh người khác chạy trùng hoặc dùng schema cũ.
