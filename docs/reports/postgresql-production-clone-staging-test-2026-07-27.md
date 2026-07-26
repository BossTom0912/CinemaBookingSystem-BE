# Báo cáo kiểm thử PostgreSQL production clone trên staging

Ngày kiểm tra: 2026-07-27  
Repository: `CinemaSystem_BE`  
Nhánh: `Tom/postgresql-mainlocal`  
Production branch: `main`  
Phạm vi: chỉ production clone và tài nguyên staging; không ghi hoặc migrate production.

## Kết luận tạm thời

**T1-T8 PASS. Chưa merge vào `main` cho đến khi T9 Render web staging PASS.**

- Production PostgreSQL đã được backup ở chế độ chỉ đọc và restore thành công vào
  database Render staging riêng.
- Preflight đã nhận đúng schema legacy thực tế rồi chạy đủ năm migration PostgreSQL.
- Schema, dữ liệu, API, authorization, seat-lock concurrency, booking, voucher,
  cancellation và refund đều đã được kiểm tra trên PostgreSQL staging thật.
- Không gọi SePay/VNPAY thật. Payment dùng cho refund là giao dịch fixture chỉ tồn
  tại trong staging.
- Production không bị migrate, seed hoặc thay đổi dữ liệu.

## Tài nguyên và ranh giới an toàn

| Hạng mục | Kết quả |
|---|---|
| Render environment | `Staging` |
| Render PostgreSQL | `cinemabooking-staging-db` |
| Database | `cinema_booking_staging` |
| PostgreSQL | 18.4 |
| Production web service | Không thay đổi; vẫn theo dõi `main` |
| Email staging | Mock |
| VNPAY staging | Disabled |
| Secret/connection string | Không ghi vào source hoặc báo cáo |

Database staging dùng gói Free và có thời hạn của Render. Đây không phải database
production và không được dùng làm nơi lưu dữ liệu lâu dài.

## Backup và restore production clone

| Kiểm tra | Kết quả |
|---|---|
| `pg_dump --format=custom --no-owner --no-acl` | PASS |
| Production public tables | 52 tổng, 51 bảng ứng dụng |
| Production database size | Khoảng 10 MB |
| Staging trước restore | 0 bảng |
| `pg_restore --no-owner --no-acl` | PASS |
| Staging sau restore | 52 bảng, khoảng 10 MB |

Backup cục bộ chứa dữ liệu production chỉ được giữ tạm trong quá trình kiểm tra và
phải xóa sau khi hoàn tất T9.

## Migration/preflight trên schema production clone

Lần chạy đầu tiên phát hiện đúng hai khác biệt legacy dự kiến:

- `VOUCHER.roomId` và `VOUCHER.showtimeId` chưa tồn tại;
- năm cột `rowVersion` đã là `bytea` đúng nullability nhưng chưa có default
  sequence-backed.

Preflight trước đó từ chối cả các khác biệt hợp lệ này. Bản sửa chỉ cho phép đúng
hai cột được migration sau thêm vào và đúng năm `rowVersion` legacy thiếu default;
mọi sai khác type, nullability hoặc default khác vẫn bị từ chối.

Sau sửa:

| Kiểm tra | Kết quả |
|---|---|
| Legacy schema adoption | PASS |
| Năm migration PostgreSQL mới | Đủ 5 |
| Legacy history cũ | Được giữ lại |
| `rowVersion` | 5 cột `bytea`, đúng nullability và có default |
| Trigger | 5 trigger, bao phủ INSERT và UPDATE |
| Validated CHECK constraints | 70 |
| `VOUCHER.roomId/showtimeId` | Có, nullable `varchar` |
| API startup sau migration | PASS |
| `GET /api/movies` sau migration | HTTP 200 |

Production clone có tổng cộng sáu history row: một migration legacy
`20260724074345_InitialCreate` và năm migration PostgreSQL hiện hành. Điều kiện
đúng là **history chứa đủ năm migration PostgreSQL**, không yêu cầu history chỉ có
đúng năm row.

## Đối chiếu dữ liệu

Sau migration, cả production và staging đều có 51 bảng ứng dụng. Tổng row tăng từ
4 lên 11, chỉ do reconciliation seed có chủ đích:

| Bảng | Production | Staging |
|---|---:|---:|
| `ROLE_ASSIGNMENT_RULE` | 0 | 3 |
| `ROLE_PROVISIONING_POLICY` | 0 | 4 |

Không có bảng nghiệp vụ production nào bị giảm row.

## Build và test tự động

| Cổng | Kết quả |
|---|---|
| Release build | PASS, 0 error |
| EF pending model changes | Không có |
| Local regression | 345 passed, 0 failed |
| PostgreSQL integration local | 4 skipped do local admin connection chưa cấu hình |
| PostgreSQL integration GitHub | PENDING cho commit mới |
| `git diff --check` | PASS |

Test mới `ExistingProductionLegacySchema_IsAdoptedAndUpgraded` tái hiện đúng shape
legacy thực tế: thiếu hai cột voucher và thiếu default trên năm cột `rowVersion`.
GitHub workflow dùng PostgreSQL 18 thật và phải PASS trước khi T9 được coi là hoàn
tất.

## Smoke test và phân quyền trên PostgreSQL staging

| Test | Kết quả |
|---|---|
| `GET /api/health` | 200 |
| `GET /api/movies` | 200 |
| `GET /api/showtimes` | 200 |
| `GET /api/cinemas` | 200 |
| Login Customer | 200 |
| Login Staff | 200 |
| Login Manager | 200 |
| Login Admin | 200 |
| Customer vào Customer policy | 200 |
| Customer vào Admin policy | 403 |
| Admin vào Admin policy | 200 |
| Admin vào Customer policy | 403 |
| Anonymous vào endpoint protected | 401 |
| Staff hủy showtime/refund | 403 |
| Manager đọc showtime trong cinema scope | 200 |

Tài khoản và dữ liệu dùng cho test được tạo riêng trong staging, không copy hoặc
thay đổi tài khoản production.

## Seat locking, booking, voucher và cancellation

Hai customer gửi lock đồng thời cho cùng ghế:

- một request trả `200`, ghế `LOCKED`;
- một request trả `409`, `SEAT_LOCKED`;
- điều kiện đúng chính xác một winner: PASS.

Luồng booking:

1. Claim voucher `PGSTAGING10`: PASS.
2. Validate voucher cho đơn 110.000 VND: PASS.
3. Lock ghế bằng customer sở hữu booking: PASS.
4. Tạo booking `PENDING_PAYMENT`: PASS.
5. Tổng tiền sau voucher: 99.000 VND.
6. Hủy booking: PASS.
7. Booking chuyển `CANCELLED`, ghế trở lại `AVAILABLE`.
8. `VOUCHER_USAGE` chuyển `CANCELLED`, voucher không bị tiêu hao.

## Showtime cancellation và refund

Một booking staging thứ hai được tạo qua API rồi đánh dấu đã thanh toán bằng
payment fixture `SUCCESS` trong staging. Manager hủy showtime qua API:

| Kiểm tra | Kết quả |
|---|---|
| Showtime status | `CANCELLED` |
| Paid booking được xử lý | 1 |
| Refund tạo mới | 1 |
| Refund amount | 110.000 VND |
| Refund status | `PENDING` |
| Refund claim | 1 |
| Compensation xử lý | 1 booking |
| Showtime seats | 2 `UNAVAILABLE` |

Luồng này chỉ xác nhận tạo refund/claim và trạng thái DB. Không gửi tiền thật và
không gọi payment gateway production.

## Connection pool

Sau chuỗi smoke/business/concurrency test:

| Trạng thái `pg_stat_activity` | Connection |
|---|---:|
| active | 1 (phiên kiểm tra hiện tại) |
| idle | 1 (API pool) |
| `max_connections` | 100 |

Không thấy connection tăng bất thường trong lần kiểm tra. Render web staging vẫn
phải cấu hình application pool nhỏ hơn giới hạn database và được quan sát lại sau
deploy.

## Ma trận T1-T10

| Task | Trạng thái | Bằng chứng |
|---|---|---|
| T1 PostgreSQL | PASS | PostgreSQL 18.4, kết nối SQL thành công |
| T2 Database staging | PASS | Render PostgreSQL staging riêng |
| T3 Clone production | PASS | Backup/restore 52 bảng |
| T4 Migration | PASS | Preflight + đủ 5 migration |
| T5 Schema/data | PASS | bytea/default/trigger/CHECK/row-count |
| T6 API/authorization | PASS | Public API + 4 role |
| T7 Business flow | PASS | Booking/voucher/cancel/refund |
| T8 Concurrency/pool | PASS | One-winner seat lock; pool 2/100 |
| T9 Render web staging | PENDING | Chưa tạo/deploy web service |
| T10 Merge `main` | NOT EXECUTED | User yêu cầu chỉ push nhánh, không merge |

## Điều kiện còn lại

1. Push commit lên `Tom/postgresql-mainlocal`.
2. GitHub PostgreSQL integration trên commit đó phải PASS.
3. Tạo Render web service staging từ đúng nhánh, dùng database staging và secret
   staging riêng.
4. Log phải cho thấy migration/startup thành công, không có schema/database error.
5. Lặp lại health, public API, auth và seat-lock check qua URL staging.
6. Chỉ cập nhật kết luận sau khi T9 PASS; không merge `main` trong đợt này.
