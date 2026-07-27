# Báo cáo kiểm thử PostgreSQL production clone trên staging

Ngày kiểm tra: 2026-07-27  
Repository: `CinemaSystem_BE`  
Nhánh: `Tom/postgresql-mainlocal`  
Production branch: `main`  
Phạm vi: chỉ production clone và tài nguyên staging; không ghi hoặc migrate production.

## Kết luận

**T1-T9 PASS. Nhánh đã sẵn sàng để review; chưa merge vào `main` theo yêu cầu.**

- Production PostgreSQL đã được backup ở chế độ chỉ đọc và restore thành công vào
  database Render staging riêng.
- Preflight đã nhận đúng schema legacy thực tế rồi chạy đủ năm migration PostgreSQL.
- Schema, dữ liệu, API, authorization, seat-lock concurrency, booking, voucher,
  cancellation và refund đều đã được kiểm tra trên PostgreSQL staging thật.
- Không gọi SePay/VNPAY thật. Payment dùng cho refund là giao dịch fixture chỉ tồn
  tại trong staging.
- Production không bị migrate, seed hoặc thay đổi dữ liệu.
- GitHub PostgreSQL integration đã PASS trên commit `abeb9a6`.
- Render web staging đã `Live`; public health/API/auth/concurrency đều PASS.

## Tài nguyên và ranh giới an toàn

| Hạng mục | Kết quả |
|---|---|
| Render environment | `Staging` |
| Render PostgreSQL | `cinemabooking-staging-db` |
| Database | `cinema_booking_staging` |
| PostgreSQL | 18.4 |
| Render web staging | `cinemabookingsystem-be-staging` |
| Service ID | `srv-d9j6ir7avr4c73bv8npg` |
| Public URL | `https://cinemabookingsystem-be-staging.onrender.com` |
| Source commit | `abeb9a6` |
| Instance | Free |
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

Backup cục bộ chứa dữ liệu production đã được xóa khỏi Windows Temp sau khi T9
hoàn tất. Sáu file log API local tạm cũng đã được xóa.

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
| PostgreSQL integration GitHub | PASS trên PostgreSQL 18, run `30218181131` |
| `git diff --check` | PASS |

Test mới `ExistingProductionLegacySchema_IsAdoptedAndUpgraded` tái hiện đúng shape
legacy thực tế: thiếu hai cột voucher và thiếu default trên năm cột `rowVersion`.
GitHub workflow đã chạy toàn bộ suite với PostgreSQL 18 thật và PASS.

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

Render được cấu hình `Maximum Pool Size=10`. Sau khi smoke test public hoàn tất và
API local đã dừng:

| `application_name` | Trạng thái | Connection |
|---|---|---:|
| `CinemaSystem-Staging` | idle | 1 |
| `psql` (phiên kiểm tra) | active | 1 |
| Tổng |  | 2 |
| `max_connections` |  | 100 |

Không thấy connection tăng bất thường; application pool được giới hạn ở 10.

## Render web staging

Service được tạo trong environment `Staging` với:

- branch `Tom/postgresql-mainlocal`;
- Dockerfile `CinemaSystem/Dockerfile`, build context repository root;
- Free instance tại Singapore;
- health check `/api/health`;
- Auto-Deploy `On Commit`;
- database internal connection riêng, email mock, VNPAY disabled và secret staging.

Log first deploy:

| Kiểm tra | Kết quả |
|---|---:|
| Source | `abeb9a6` |
| Deploy status | `Live` |
| Migration | Database already up to date |
| Application startup | PASS |
| Runtime database exception | Không có |
| Health check log | HTTP 200 |

Public verification:

| Test | Kết quả |
|---|---|
| `/api/health` | 200 |
| `/api/movies` | 200 |
| `/api/showtimes` | 200 |
| `/api/cinemas` | 200 |
| Login Admin/Manager/Staff/2 Customer | 200 |
| Admin policy: Admin / Customer | 200 / 403 |
| Customer policy: Customer / Anonymous | 200 / 401 |
| Manager cinema scope | 200 |
| Hai client lock cùng ghế | một 200, một 409 `SEAT_LOCKED` |
| Winner unlock | 200 |

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
| T9 Render web staging | PASS | Live, public health/API/auth/concurrency PASS |
| T10 Merge `main` | NOT EXECUTED | User yêu cầu chỉ push nhánh, không merge |

## Bàn giao

1. Commit bằng chứng cuối cần được push tiếp lên `Tom/postgresql-mainlocal`.
2. Push báo cáo sẽ kích hoạt Auto-Deploy staging và GitHub CI thêm một lần; cả hai
   phải được quan sát lại.
3. Không merge `main` trong đợt này.
4. Database/web Free có thể sleep và database có ngày hết hạn theo Render; cần nâng
   cấp hoặc tạo lại nếu muốn dùng staging lâu dài.
