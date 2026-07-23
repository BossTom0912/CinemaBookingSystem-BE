# Hướng dẫn FE: Voucher sự kiện và quyền lợi sự cố

Tài liệu này phân biệt hai loại quyền lợi mà giao diện khách hàng không được trộn lẫn:

| Khu vực UI | Mục đích | Nguồn dữ liệu | Cách sử dụng |
|---|---|---|---|
| **Voucher sự kiện** | Khuyến mãi/campaign mà khách nhận hoặc đổi | `VOUCHER`, `CUSTOMER_VOUCHER` | Giảm giá cho một đơn đặt vé theo điều kiện campaign |
| **Quyền lợi sự cố** | Bồi thường khi rạp hủy suất chiếu đã thanh toán | `CANCELLATION_COMPENSATION` và các quyền lợi con | Vé phim 100% và combo bắp nước do lỗi của rạp |

> Chính sách hiện hành: quyền lợi sự cố dùng được cho mọi phim, rạp, loại phòng và ghế (bao gồm IMAX, 4DX, VIP); không quy đổi tiền mặt, không chuyển nhượng; hạn dùng 180 ngày. Một booking bị hủy nhận một vé bồi thường cho mỗi ghế đã thanh toán và một combo `01 bắp vừa + 01 nước vừa` cho cả booking.

## 1. Trang "Ví của tôi"

Nên có một route ví chung, ví dụ `/my-vouchers`, với hai tab độc lập.

### Tab A — Voucher sự kiện

Tải ví voucher đã sở hữu:

```http
GET /api/vouchers/my-wallet
Authorization: Bearer <customer-token>
```

Hiển thị mã, mức giảm, điều kiện áp dụng, hạn dùng và trạng thái.

Nếu UI có khu "Khám phá ưu đãi", dùng thêm:

```http
GET /api/vouchers
POST /api/vouchers/{voucherId}/claim
```

### Tab B — Quyền lợi sự cố

Tải các quyền lợi do hủy suất chiếu:

```http
GET /api/customer/compensations
Authorization: Bearer <customer-token>
```

Ví dụ dữ liệu trả về:

```json
[
  {
    "compensationId": 12,
    "sourceBookingId": 456,
    "status": "ACTIVE",
    "issuedAt": "2026-07-18T10:00:00Z",
    "expiresAt": "2027-01-14T10:00:00Z",
    "tickets": [
      {
        "compensationTicketId": 31,
        "voucherCode": "CMP-TICKET-AAA",
        "status": "ACTIVE"
      }
    ],
    "combo": {
      "compensationComboId": 9,
      "voucherCode": "CMP-COMBO-AAA",
      "displayName": "01 bắp vừa + 01 nước vừa",
      "status": "ACTIVE"
    }
  }
]
```

UI cần hiển thị rõ booking/suất chiếu nguồn (`sourceBookingId`), số vé bồi thường còn dùng, hạn dùng, quyền áp dụng mọi định dạng và mã QR/mã chữ của combo.

Không gọi `GET /api/vouchers/my-wallet` hoặc `GET /api/vouchers/validate` để đọc/kiểm tra quyền lợi sự cố. Đây không phải voucher campaign.

## 2. Checkout đặt vé

Khi mở checkout, FE nên tải song song hai nguồn:

```http
GET /api/vouchers/my-wallet
GET /api/customer/compensations
```

Giao diện chọn ưu đãi phải tách hai vùng:

1. **Voucher sự kiện**: tối đa một voucher campaign.
2. **Dùng quyền lợi sự cố**: chọn mã vé bồi thường theo số ghế đang đặt.

### Đặt vé bằng voucher sự kiện

FE có thể kiểm tra trước để hiển thị lý do không hợp lệ:

```http
GET /api/vouchers/validate?code=EVENT10&bookingAmount=250000
```

Khi tạo booking:

```http
POST /api/bookings
Authorization: Bearer <customer-token>
Content-Type: application/json
```

```json
{
  "showtimeId": 123,
  "showtimeSeatIds": [101, 102],
  "voucherCode": "EVENT10"
}
```

### Đặt vé bằng quyền lợi sự cố

Không validate qua API voucher campaign. Gửi trực tiếp các mã vé bồi thường khi tạo booking:

```http
POST /api/bookings
Authorization: Bearer <customer-token>
Content-Type: application/json
```

```json
{
  "showtimeId": 123,
  "showtimeSeatIds": [101, 102],
  "compensationTicketCodes": [
    "CMP-TICKET-AAA",
    "CMP-TICKET-BBB"
  ]
}
```

### Quy tắc FE bắt buộc

- Không gửi đồng thời `voucherCode` và `compensationTicketCodes`.
- Tối đa một mã vé bồi thường cho một ghế đang đặt.
- Không tự chặn IMAX, 4DX, VIP hoặc loại ghế trên FE.
- Backend là nguồn tính tiền cuối cùng. Đọc `totalAmount` và `compensationDiscountAmount` từ response tạo booking.
- Nếu `totalAmount` bằng `0`, bỏ qua QR/cổng thanh toán và chuyển thẳng tới trang đặt vé thành công.
- Nếu còn tiền (ví dụ đồ ăn, thức uống hoặc ghế không được bồi thường), tạo thanh toán cho đúng `totalAmount` backend trả về.

Ví dụ response cần dùng:

```json
{
  "totalAmount": 0,
  "compensationDiscountAmount": 240000,
  "status": "PAID"
}
```

## 3. Nhân viên đổi combo bồi thường

Trong Staff Ticket Scanner nên có tab/nút **Đổi combo bồi thường**. Nhân viên quét QR hoặc nhập mã và gọi:

```http
POST /api/staff/compensations/combos/redeem
Authorization: Bearer <staff-or-manager-token>
Content-Type: application/json
```

```json
{
  "voucherCode": "CMP-COMBO-AAA"
}
```

Luồng này chỉ đổi combo tại quầy; không đi qua checkout, `claim voucher`, hay `validate voucher` của campaign.

## 4. Manager/Admin hủy suất chiếu

Cả Manager và Admin phải gọi API hủy chuẩn khi suất đã có booking:

```http
POST /api/manager/showtimes/{showtimeId}/cancel
Authorization: Bearer <manager-or-admin-token>
Content-Type: application/json
```

```json
{
  "reason": "Sự cố kỹ thuật phòng chiếu"
}
```

FE hiển thị kết quả dựa trên các trường:

```json
{
  "paidBookingsCompensated": 3,
  "ticketVouchersIssued": 5,
  "comboVouchersIssued": 3
}
```

Không ghi hoặc hiển thị đây là "refund" / hoàn tiền trong UI hủy suất chiếu. `DELETE /api/showtimes/{id}` chỉ nên dùng để xóa suất chưa phát sinh booking/draft; UI Admin phải chuyển sang endpoint hủy chuẩn ở trên cho suất đã có khách đặt.

## 5. Lịch sử đặt vé và thông báo sau hủy

Khi khách mở lịch sử hoặc quay lại app sau thông báo suất bị hủy, tải:

```http
GET /api/bookings/my-bookings
GET /api/customer/compensations
```

FE đối chiếu `bookingId` với `sourceBookingId` để hiển thị ở booking đã hủy:

> Suất chiếu đã bị hủy bởi rạp. Bạn đã nhận 2 vé xem phim bất kỳ và 1 combo bắp nước. Xem tại tab "Quyền lợi sự cố".

## 6. Thay đổi cần làm ở FE hiện tại

- Thêm route/trang ví và hai tab dữ liệu; hiện chưa có route ví quyền lợi sự cố.
- Bổ sung `compensationTicketCodes?: string[]` vào payload checkout và gửi xuống `POST /api/bookings`.
- Bổ sung `compensationDiscountAmount` vào model response checkout/booking.
- Bổ sung danh sách mã bồi thường vào key idempotency checkout để thay đổi lựa chọn mã không bị phát lại request cũ.
- Thay phần UI voucher trong checkout bằng hai vùng chọn riêng, không dùng chung một input/một state.
- Bổ sung tab đổi combo ở scanner nhân viên.
- Đổi Admin UI hủy suất chiếu có booking từ `DELETE /api/showtimes/{id}` sang `POST /api/manager/showtimes/{id}/cancel`.

## 7. API map ngắn gọn

| Actor | Màn hình | API |
|---|---|---|
| Customer | Ví voucher sự kiện | `GET /api/vouchers/my-wallet` |
| Customer | Nhận voucher campaign | `GET /api/vouchers`, `POST /api/vouchers/{voucherId}/claim` |
| Customer | Kiểm tra voucher campaign | `GET /api/vouchers/validate` |
| Customer | Ví quyền lợi sự cố | `GET /api/customer/compensations` |
| Customer | Checkout bằng voucher campaign | `POST /api/bookings` với `voucherCode` |
| Customer | Checkout bằng vé bồi thường | `POST /api/bookings` với `compensationTicketCodes` |
| Customer | Lịch sử booking | `GET /api/bookings/my-bookings` |
| Staff/Manager/Admin | Đổi combo bồi thường | `POST /api/staff/compensations/combos/redeem` |
| Manager/Admin | Hủy suất chiếu đã có booking | `POST /api/manager/showtimes/{id}/cancel` |
