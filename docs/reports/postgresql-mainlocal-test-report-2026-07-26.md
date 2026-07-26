# Báo cáo kiểm thử nhánh PostgreSQL `Tom/postgresql-mainlocal`

Ngày kiểm tra: 2026-07-26
Repository: `CinemaSystem_BE`
Nhánh đang kiểm tra: `Tom/postgresql-mainlocal`
Commit nhánh trước các thay đổi chưa commit: `b29fd463e88a81add875dc2b28909dd0e2d04a6c`
Commit `main` đang chạy trên Render: `653ba23f1aed23506646d8aa69a033b1d5f50852`

## Kết luận

**NO-GO cho merge/deploy production tại thời điểm lập báo cáo.**

- Restore, Release build và toàn bộ 343 test tự động đều PASS.
- PostgreSQL 18.4 local đang chạy và chấp nhận kết nối TCP tại `127.0.0.1:5432`.
- Chưa chạy được câu lệnh SQL có xác thực vì phiên kiểm tra không nhận mật khẩu PostgreSQL.
- Chưa restore production clone vào staging, nên migration/raw SQL chưa được thực thi trên PostgreSQL thật.
- Nhánh hiện tại không có initial migration và model snapshot chỉ có 20 dòng rỗng; database PostgreSQL trống chưa thể bootstrap an toàn bằng bộ migration hiện tại.

Không có thao tác ghi, migrate, seed hoặc thay đổi nào được thực hiện trên production.

## Trạng thái Git

| Kiểm tra | Kết quả |
|---|---|
| Nhánh hiện tại | `Tom/postgresql-mainlocal` |
| So với `github/main` | `0 behind / 38 ahead` |
| `main` có nằm trong lịch sử nhánh không | Có |
| File tracked đang thay đổi trước báo cáo | 20 |
| File mới chưa track trước báo cáo | 2 |
| `git diff --check` | PASS |

Báo cáo phản ánh toàn bộ working tree hiện tại, bao gồm các thay đổi chưa commit.

## T1 - PostgreSQL local

| Kiểm tra | Kết quả | Trạng thái |
|---|---|---|
| Windows service `postgresql-x64-18` | `Running`, startup `Automatic` | PASS |
| PostgreSQL client | `psql (PostgreSQL) 18.4` | PASS |
| TCP `127.0.0.1:5432` | `accepting connections` | PASS |
| `SELECT version()` | Chưa chạy: `no password supplied` | PENDING |
| `SELECT current_database(), current_user` | Chưa chạy: cần xác thực | PENDING |
| `SELECT current_setting('port')` | Chưa chạy: cần xác thực | PENDING |

`psql` được cài tại `D:\FPT\DB\bin` nhưng chưa có trong biến `PATH`.

## Restore, build và test tự động

Các lệnh đã chạy từ repository root, với real SMTP không được bật:

```powershell
dotnet restore CinemaSystem.sln
dotnet build CinemaSystem.sln --no-restore --configuration Release --no-incremental -m:1
dotnet test CinemaSystem.sln --no-build --no-restore --configuration Release -m:1
```

| Cổng kiểm tra | Kết quả |
|---|---|
| Restore | PASS - tất cả project up-to-date |
| Release build | PASS - 0 error, 90 nullable warning |
| Full regression | PASS - 343 passed, 0 failed, 0 skipped |
| Thời gian test | 30 giây |

### Giới hạn của kết quả 343/343

Có 20 file test sử dụng `UseInMemoryDatabase`. EF InMemory không thực thi đầy đủ:

- cú pháp PostgreSQL trong raw SQL/migration;
- foreign key, partial unique index và database-generated default;
- trigger cập nhật `rowVersion`;
- transaction, lock và unique-violation behavior của Npgsql;
- connection pooling và giới hạn connection trên Render.

Vì vậy 343/343 là bằng chứng regression application logic, không phải bằng chứng PostgreSQL integration hoàn chỉnh.

## Audit tĩnh PostgreSQL

### Đã đạt

- Runtime chỉ đăng ký `Npgsql.EntityFrameworkCore.PostgreSQL` phiên bản `8.0.11`.
- `CinemaDbContext` được đăng ký bằng `UseNpgsql`.
- Không tìm thấy `UseSqlServer`, `Microsoft.Data.SqlClient`, `SqlException`, `OBJECT_ID`, `COL_LENGTH`, `dbo.`, `sysutcdatetime()` hoặc `getdate()` trong code runtime/migration hiện tại.
- Database timestamp defaults trong EF mapping đã dùng `CURRENT_TIMESTAMP`.
- Duplicate-key handling đã dùng `PostgresException` và `PostgresErrorCodes.UniqueViolation`.
- Migration PostgreSQL có `DO $$`, `bytea` và trigger concurrency cho `rowVersion`.
- Startup ngoài môi trường `Testing` sẽ dừng nếu migration thất bại.

### Blocker nghiêm trọng

1. `20260724065659_InitialCreate.cs` của production `main` không còn trong nhánh hiện tại.
2. `CinemaDbContextModelSnapshot.cs` chỉ có 20 dòng và không chứa model schema.
3. Các upgrade migration hiện tại có ID từ `20260711` đến `20260723`, sớm hơn initial migration production `20260724`.
4. Trên database trống, EF sẽ thử chạy các upgrade migration trước khi có bảng nền và có thể thất bại.
5. Không được copy nguyên initial migration production vì file đó còn chứa `sysutcdatetime()` và `getdate()` không hợp lệ trên PostgreSQL.

Do đó bộ migration hiện tại chỉ nên thử trên **staging clone của production**, không chạy trên database PostgreSQL trống.

## Ma trận T1-T10

| Task | Trạng thái | Bằng chứng / điều kiện còn thiếu |
|---|---|---|
| T1 - PostgreSQL local | PARTIAL PASS | Service, client và TCP PASS; SQL auth queries còn PENDING |
| T2 - Tạo `CinemaBookingStaging` | PENDING | Chưa xác minh bằng SQL có xác thực |
| T3 - Clone production vào staging | PENDING | Chưa có backup/restore |
| T4 - Chạy migration PostgreSQL | BLOCKED | Phụ thuộc T3; không chạy trên DB trống |
| T5 - Schema và dữ liệu | STATIC PASS / LIVE PENDING | Audit source PASS; cần query schema/data trên clone |
| T6 - API cơ bản | AUTOMATED PASS / LIVE PENDING | Cần chạy API với Npgsql thật |
| T7 - Nghiệp vụ quan trọng | AUTOMATED PASS / LIVE PENDING | Cần booking/voucher/refund trên staging clone |
| T8 - Concurrency/performance | PENDING | Cần PostgreSQL lock, trigger, pool và `pg_stat_activity` |
| T9 - Render staging | PENDING | Chưa tạo service staging |
| T10 - Merge `main` | BLOCKED | Chỉ thực hiện khi T1-T9 PASS |

## Các bước tiếp theo

### 1. Hoàn tất T1 và T2 trong pgAdmin

Chạy trên `CinemaBookingStaging`:

```sql
SELECT version();
SELECT current_database(), current_user;
SELECT current_setting('port');
```

### 2. Hoàn tất T3

- Backup PostgreSQL production bằng external connection string.
- Restore vào `CinemaBookingStaging` hoặc một PostgreSQL staging riêng trên Render.
- Không chạy `docs/database/cinema-booking-schema.sql`; đây là SQL Server reset script legacy.
- Không đưa connection string hoặc password vào source/report/chat.

### 3. Chạy T4-T5 trên clone

Khởi động API với `ConnectionStrings__DefaultConnection` trỏ vào staging clone. Migration phải hoàn tất và API phải khởi động không lỗi.

```sql
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_name IN
(
    'SHOWTIME_SEAT',
    'REFUND_CLAIM',
    'MANUAL_REFUND_PROCESS'
)
AND column_name = 'rowVersion'
ORDER BY table_name;
```

Kết quả phải có `20260726000000_ConfigurePostgresRowVersionConcurrency`; các `rowVersion` tồn tại phải là `bytea`.

### 4. Chạy T6-T8

- Public movies/showtimes.
- Login và authorization Customer/Staff/Manager/Admin.
- Seat map và hai client lock cùng một ghế.
- Pending booking, voucher apply/cancel/release.
- Showtime cancellation và refund trên dữ liệu staging.
- Theo dõi `pg_stat_activity`; connection count phải ổn định và không vượt pool đã cấu hình.
- Không thực hiện thanh toán thật; chỉ dùng VNPAY Sandbox khi callback staging đã được cấu hình.

## Điều kiện chuyển sang GO

Chỉ chuyển kết luận sang GO khi:

1. SQL auth T1 và database T2 PASS.
2. Production clone restore thành công.
3. Toàn bộ migration chạy thành công trên clone.
4. Schema, index, trigger và dữ liệu được đối chiếu.
5. API/business/concurrency test trên PostgreSQL thật PASS.
6. Render staging healthy và không có migration/database error trong log.

Hướng dẫn thao tác chi tiết: [`../database/postgresql-staging-test-guide.md`](../database/postgresql-staging-test-guide.md).
