# CinemaSystem - Báo cáo tổng hợp và SQL bàn giao

Đây là điểm vào duy nhất để tra cứu trạng thái nhánh, báo cáo kỹ thuật và SQL
của dự án. Các báo cáo chi tiết vẫn được giữ trong thư mục này để truy vết,
nhưng không được xem là nguồn thay thế yêu cầu, business rules hoặc schema
canonical.

## Trạng thái `main_local`

- Mốc tích hợp gần nhất: `36b0364` - merge
  `origin/voucher_refund_fixLogic` vào `main_local`.
- Build Release: 0 error, 90 warning nullable.
- Full regression: 343 passed, 0 failed, 0 skipped.
- VNPAY và voucher/refund đã cùng tồn tại trên nhánh kiểm thử.
- Chưa nên merge vào `main` production trước khi xử lý cinema scope của
  notification và chốt chính sách migration DB khi startup.

Bằng chứng merge, conflict và test:
[`main-local-voucher-refund-fix-logic-merge-test-2026-07-24.md`](main-local-voucher-refund-fix-logic-merge-test-2026-07-24.md).

## SQL duy nhất được hỗ trợ

Schema canonical:
[`../database/cinema-booking-schema.sql`](../database/cinema-booking-schema.sql).

- Đây là file `.sql` schema duy nhất trong `docs`.
- Script xóa và tạo lại `CinemaBookingDB`; chỉ dùng cho database local/demo có
  thể xóa dữ liệu.
- Database cần giữ dữ liệu phải dùng EF/data migration được review riêng.
- Không copy SQL từ báo cáo cũ và không chạy feature patch rời.

Chạy reset local:

```powershell
sqlcmd -S . -E -b -f 65001 -i "docs\database\cinema-booking-schema.sql"
```

Hướng dẫn SQL và fixture:
[`../database/README.md`](../database/README.md).

## Báo cáo hiện hành theo chức năng

| Phạm vi | Báo cáo |
|---|---|
| Manager cinema scope | [`SCRUM-190-manager-cinema-scope.md`](SCRUM-190-manager-cinema-scope.md) |
| Hủy suất và refund lịch sử | [`SCRUM-192-cancel-showtime-refund.md`](SCRUM-192-cancel-showtime-refund.md) |
| Chính sách voucher bồi thường hiện hành | [`showtime-cancellation-compensation-voucher.md`](showtime-cancellation-compensation-voucher.md) |
| Customer-assisted/manual refund | [`SCRUM-193-customer-assisted-refund.md`](SCRUM-193-customer-assisted-refund.md) |
| Thay đổi DB cho refund | [`SCRUM-193-customer-assisted-refund-db-changes.md`](SCRUM-193-customer-assisted-refund-db-changes.md) |
| Manager revenue/ticket overview | [`SCRUM-195-manager-revenue-ticket-overview.md`](SCRUM-195-manager-revenue-ticket-overview.md) |
| Ticket scan | [`SCRUM-198-ticket-scan.md`](SCRUM-198-ticket-scan.md) |
| Thay đổi DB cho ticket scan | [`SCRUM-198-ticket-scan-db-changes.md`](SCRUM-198-ticket-scan-db-changes.md) |
| Counter F&B transaction retry | [`counter-fb-order-execution-strategy-fix-2026-07-20.md`](counter-fb-order-execution-strategy-fix-2026-07-20.md) |
| Customer movie/booking flow | [`customer-flow-movie-view-booking.md`](customer-flow-movie-view-booking.md) |
| Auth Sprint 1 | [`sprint-1-auth-implementation.md`](sprint-1-auth-implementation.md) |
| Forgot/reset password | [`forgot-password-implementation.md`](forgot-password-implementation.md) |

Khi nội dung refund giữa các báo cáo cũ khác với chính sách hiện hành, ưu tiên
`showtime-cancellation-compensation-voucher.md` và code/test trên nhánh hiện tại.

## Báo cáo tích hợp và kiểm tra

| Phạm vi | Báo cáo |
|---|---|
| `main_local` + voucher/refund + VNPAY | [`main-local-voucher-refund-fix-logic-merge-test-2026-07-24.md`](main-local-voucher-refund-fix-logic-merge-test-2026-07-24.md) |
| `main` + Manager/Admin | [`main-manager-admin-integration-test-2026-06-28.md`](main-manager-admin-integration-test-2026-06-28.md) |
| Manager/Admin merge và DB | [`MangerAndAdmin_1-admin-merge-report.md`](MangerAndAdmin_1-admin-merge-report.md) |
| Hardcode audit | [`hardcode-audit-2026-07-03.md`](hardcode-audit-2026-07-03.md) |

## Tài liệu kế hoạch và FE tham khảo

- [`scrum-157-checkout-implementation-plan.md`](scrum-157-checkout-implementation-plan.md)
- [`manager-fe-ui-requirements.md`](manager-fe-ui-requirements.md)

Các file trên là tài liệu kế hoạch hoặc lịch sử. Hành vi runtime phải được xác
minh lại bằng controller, service, EF mapping và test hiện tại.

## Fixture phát triển

Các fixture nằm tại `docs/database` và không phải schema deployment:

- `dev-seed-admin-manager-staff.txt`
- `dev-seed-bookable-showtimes.txt`
- `dev-seed-paid-ticket-ready-to-scan.txt`
- `dev-seed-10-movies-booking-payment-qr.txt`
- `dev-seed-voucher-compensation-flow.txt`

Chỉ chạy có chủ đích trên local/test database.

## Cleanup ngày 2026-07-24

- Chuyển hai báo cáo DB SCRUM-193 và SCRUM-198 về cùng `docs/reports`.
- Loại hai ghi chú merge DB bị trùng vì nội dung đã có trong schema canonical
  và các báo cáo tích hợp.
- Loại file tạm `New Text Document.txt` chứa prompt-injection yêu cầu lộ dữ
  liệu/credential; file này không có giá trị dự án.
- Giữ nguyên vị trí SQL canonical để không làm gãy hướng dẫn và quy trình
  deployment hiện có.
