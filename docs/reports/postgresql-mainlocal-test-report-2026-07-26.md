# Báo cáo kiểm thử nhánh PostgreSQL `Tom/postgresql-mainlocal`

Ngày kiểm tra: 2026-07-26
Repository: `CinemaSystem_BE`
Nhánh đang kiểm tra: `Tom/postgresql-mainlocal`
Mốc nhánh trước đợt hoàn thiện PostgreSQL: `8652cc1647f99b9ea0b9d7563f967978001d95fd`
Commit `main` đang chạy trên Render: `653ba23f1aed23506646d8aa69a033b1d5f50852`

## Kết luận

**NO-GO cho merge/deploy production tại thời điểm cập nhật báo cáo.**

- Restore, Release build và toàn bộ 348 test tự động đều PASS.
- PostgreSQL 18.4 tạm, cô lập tại `127.0.0.1:55432` đã chạy SQL xác thực thành công.
- 3 integration test PostgreSQL thật đã PASS: fresh migration, legacy adoption/idempotency và fail-fast khi index trùng tên nhưng sai định nghĩa.
- Database trống đã áp dụng thành công baseline đầy đủ và migration concurrency, tạo 51 bảng ứng dụng.
- Clone legacy tổng hợp không có migration history đã vượt qua preflight, được nhận baseline, chạy migration còn lại, khởi động API Production và trả HTTP 200.
- Clone cố ý thiếu cột hoặc index đã bị từ chối trước khi ghi baseline; fail-fast hoạt động đúng.
- `rowVersion` dùng sequence và trigger PostgreSQL; INSERT rồi UPDATE hai lần trong cùng transaction vẫn sinh hai giá trị khác nhau.
- Data reconciliation tạo đúng 4 role, 4 provisioning policy, 3 assignment rule; fixture voucher legacy được link claim và release trạng thái `APPLIED` đúng.
- Chưa restore **production clone thật** và chưa deploy **Render staging**, nên chưa có bằng chứng schema/dữ liệu production vượt qua preflight.

Không có thao tác ghi, migrate, seed hoặc thay đổi nào được thực hiện trên production.

## Trạng thái Git

| Kiểm tra | Kết quả |
|---|---|
| Nhánh hiện tại | `Tom/postgresql-mainlocal` |
| So với `github/main` | `0 behind / 39 ahead` trước thay đổi hiện tại |
| `main` có nằm trong lịch sử nhánh không | Có |
| Trạng thái nhánh remote | `github/Tom/postgresql-mainlocal` tại `8652cc1` trước cập nhật hiện tại |
| `git diff --check` | PASS |

Báo cáo phản ánh toàn bộ nội dung đã kiểm thử trước khi push nhánh.

## T1 - PostgreSQL local

| Kiểm tra | Kết quả | Trạng thái |
|---|---|---|
| Windows service `postgresql-x64-18` | `Running`, startup `Automatic` | PASS |
| PostgreSQL client/server tạm | PostgreSQL 18.4 | PASS |
| TCP test | `127.0.0.1:55432` accepting connections | PASS |
| `SELECT version()` | PostgreSQL 18.4, 64-bit | PASS |
| `SELECT current_database(), current_user` | `cinema_fresh`, `postgres` | PASS |
| Migration history | 5 migration đúng thứ tự | PASS |

`psql` được cài tại `D:\FPT\DB\bin` nhưng chưa có trong biến `PATH`. Cluster
test dùng thư mục riêng dưới `C:\tmp`, không dùng database service cổng 5432 và
không chạm production.

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
| Release build | PASS - 0 error, 91 warning hiện hữu/generator |
| Full regression | PASS - 348 passed, 0 failed, 0 skipped |
| PostgreSQL integration | PASS - 3 passed trên PostgreSQL 18.4 thật |
| Thời gian test | 25 giây ở lần chạy regression cuối |

### Phạm vi của kết quả 348/348

Có 20 file test sử dụng `UseInMemoryDatabase`. EF InMemory không thực thi đầy đủ:

- cú pháp PostgreSQL trong raw SQL/migration;
- foreign key, partial unique index và database-generated default;
- trigger cập nhật `rowVersion`;
- transaction, lock và unique-violation behavior của Npgsql;
- connection pooling và giới hạn connection trên Render.

345 test application tiếp tục chứng minh regression logic. Ba test mới dùng
PostgreSQL thật để kiểm tra migration/schema; production clone và Render staging
vẫn là các cổng riêng chưa thể thay thế bằng fixture tổng hợp.

## Audit tĩnh PostgreSQL

### Đã đạt

- Runtime chỉ đăng ký `Npgsql.EntityFrameworkCore.PostgreSQL` phiên bản `8.0.11`.
- `CinemaDbContext` được đăng ký bằng `UseNpgsql`.
- Không tìm thấy `UseSqlServer`, `Microsoft.Data.SqlClient`, `SqlException`, `OBJECT_ID`, `COL_LENGTH`, `dbo.`, `sysutcdatetime()` hoặc `getdate()` trong code runtime/migration hiện tại.
- Database timestamp defaults trong EF mapping đã dùng `CURRENT_TIMESTAMP`.
- Duplicate-key handling đã dùng `PostgresException` và `PostgresErrorCodes.UniqueViolation`.
- Migration PostgreSQL có `DO $$`, `bytea` và trigger concurrency cho `rowVersion`.
- Startup ngoài môi trường `Testing` sẽ dừng nếu migration thất bại.

### Blocker đã xử lý trong working tree

1. Đã thay chuỗi upgrade rời rạc bằng `20260726135020_InitialPostgresBaseline`, sinh trực tiếp từ model Npgsql hiện tại.
2. `CinemaDbContextModelSnapshot.cs` đã chứa model đầy đủ thay vì snapshot rỗng.
3. Baseline không còn `sysutcdatetime()`, `getdate()` hoặc filtered-index SQL Server.
4. `20260726135102_ConfigurePostgresRowVersionConcurrency` tạo và rollback đủ 5 trigger, function và sequence.
5. `20260726140854_ReconcilePostgresData` giữ lại seed policy và voucher backfill từ chuỗi migration cũ.
6. Database legacy chỉ được nhận baseline sau khi đối chiếu type/nullability/default presence của cột, định nghĩa index, key và định nghĩa foreign key.
7. `20260726142719_ConfigurePostgresCheckConstraints` đưa 70 business CHECK constraint của 51 bảng đang mapped vào EF model; startup chỉ hoàn tất khi tất cả đã được validate.
8. `.github/workflows/postgresql-ci.yml` chạy toàn bộ test với PostgreSQL 18 thật cho push nhánh này và pull request vào `main`.
9. Đã cherry-pick `f2885cf` mới nhất từ `main_local`; migration voucher theo suất chiếu/phòng được tái sinh thành `20260726144437_AddVoucherShowtimeIdAndRoomIdPostgres` thuần Npgsql.

### Blocker còn lại

1. Chưa có backup/restore production clone để chứng minh schema thật khớp preflight.
2. Chưa chạy smoke/business/concurrency test trên dữ liệu clone production.
3. Chưa có Render staging service dùng nhánh này và staging database riêng.

## Ma trận T1-T10

| Task | Trạng thái | Bằng chứng / điều kiện còn thiếu |
|---|---|---|
| T1 - PostgreSQL local | PASS | PostgreSQL 18.4 tạm, SQL auth và TCP PASS |
| T2 - Tạo database staging | LOCAL PASS | `cinema_fresh` và các clone test riêng đã tạo; Render staging vẫn PENDING |
| T3 - Clone production vào staging | PENDING | Chưa có backup/restore |
| T4 - Chạy migration PostgreSQL | LOCAL PASS / PROD-CLONE PENDING | Fresh DB, idempotent rerun, Down/Up, data backfill và legacy adoption PASS |
| T5 - Schema và dữ liệu | LOCAL PASS / PROD-CLONE PENDING | 51 bảng, 5 history row, 70 CHECK, bytea, trigger PASS; cần clone thật |
| T6 - API cơ bản | LOCAL POSTGRES PASS / PROD-CLONE PENDING | Production-mode API trả HTTP 200 từ PostgreSQL thật |
| T7 - Nghiệp vụ quan trọng | AUTOMATED PASS / LIVE PENDING | Cần booking/voucher/refund trên staging clone |
| T8 - Concurrency/performance | PARTIAL PASS | Trigger tạo version mới trong cùng transaction PASS; cần two-client seat lock/pool trên staging |
| T9 - Render staging | PENDING | Chưa tạo service staging |
| T10 - Merge `main` | BLOCKED | Chỉ thực hiện khi T1-T9 PASS |

## Các bước tiếp theo

### 1. Chuẩn bị database staging cho production clone

Chạy trên `CinemaBookingStaging`:

```sql
SELECT version();
SELECT current_database(), current_user;
SELECT current_setting('port');
```

### 2. Restore production clone (T3)

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

Kết quả phải có `20260726135020_InitialPostgresBaseline`,
`20260726135102_ConfigurePostgresRowVersionConcurrency`,
`20260726140854_ReconcilePostgresData` và
`20260726142719_ConfigurePostgresCheckConstraints` và
`20260726144437_AddVoucherShowtimeIdAndRoomIdPostgres`; các `rowVersion` tồn tại phải là
`bytea` và đủ trigger INSERT/UPDATE.

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

1. Production clone restore thành công vào database staging riêng.
2. Full schema preflight và toàn bộ migration chạy thành công trên clone.
4. Schema, index, trigger và dữ liệu được đối chiếu.
5. API/business/concurrency test trên PostgreSQL thật PASS.
6. Render staging healthy và không có migration/database error trong log.

Hướng dẫn thao tác chi tiết: [`../database/postgresql-staging-test-guide.md`](../database/postgresql-staging-test-guide.md).
