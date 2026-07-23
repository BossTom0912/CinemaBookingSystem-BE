# Chính sách voucher bồi thường khi rạp hủy suất chiếu

## 1. Quyết định nghiệp vụ hiện hành

Luồng này thay thế hoàn tiền cho **đúng trường hợp Manager hoặc Admin hủy một
suất chiếu tương lai do lỗi/vận hành của rạp**:

1. Không tạo `REFUND`, `REFUND_CLAIM` hoặc yêu cầu khách cung cấp tài khoản
   ngân hàng.
2. Booking đã thanh toán chuyển sang `CANCELLED`; ticket cũ bị hủy.
3. Mỗi ghế đã thanh toán nhận một voucher vé xem phim 100%.
4. Mỗi booking nhận thêm một voucher combo gồm:
   `1 medium popcorn + 1 medium soft drink`.
5. Voucher vé dùng cho mọi phim, rạp, phòng, định dạng và loại ghế, bao gồm
   VIP, IMAX và 4DX. Voucher trừ đúng toàn bộ giá ghế được chọn nên khách không
   phải bù phần chênh lệch.
6. Voucher có hạn 180 ngày, gắn với khách nhận bồi thường, không chuyển nhượng
   và không quy đổi thành tiền.
7. Voucher bồi thường không được dùng chung với voucher khuyến mại thông
   thường trong cùng booking.

Các luồng hoàn tiền khác không bị xóa. Đổi lịch mà khách từ chối, thanh toán
đến muộn cho một booking hết hạn thông thường, và các trường hợp hoàn tiền hợp
lệ không xuất phát từ việc rạp hủy suất vẫn đi theo quy trình refund hiện có.

## 2. Nguồn quyết định

Nguồn đặc tả có thứ tự ưu tiên:

- `docs/requirements/business-rules.docx`, BR-93 đến BR-107: chính sách bồi
  thường, hạn dùng, phạm vi áp dụng, quyền sở hữu, idempotency, thanh toán đến
  muộn và đổi combo.
- `docs/requirements/business-rules.docx`, BR-38 đến BR-41 và BR-45 đến BR-46:
  giới hạn refund cho các trường hợp ngoài hủy suất và loại trừ việc tính tiền
  bù cho voucher này.
- `docs/database/cinema-booking-schema.sql`: script schema chuẩn duy nhất khi
  tạo lại database.
- Database đang tồn tại cần dùng EF migration hoặc data migration được review;
  không chạy script reset để giữ dữ liệu nghiệp vụ.

Các báo cáo `SCRUM-192-cancel-showtime-refund.md` và
`SCRUM-193-customer-assisted-refund.md` là tài liệu lịch sử của chính sách cũ,
không phải nguồn quyết định cho luồng hủy suất hiện hành.

## 3. Luồng hủy suất

Endpoint vẫn giữ nguyên:

```http
POST /api/manager/showtimes/{showtimeId}/cancel
```

Trong một transaction:

1. Kiểm tra suất chiếu còn ở tương lai và Manager chỉ thao tác trong rạp được
   phân công; Admin dùng quyền toàn hệ thống hiện có.
2. Chuyển `SHOWTIME.status = CANCELLED`, vô hiệu hóa toàn bộ ghế của suất và
   ghi `SHOWTIME_CANCELLATION`.
3. Với booking chưa thanh toán: hủy booking/payment/ticket và giải phóng mọi
   voucher bồi thường đang `RESERVED`.
4. Với booking đã thanh toán:
   - hủy booking và ticket;
   - khôi phục đúng voucher khuyến mại hoặc voucher bồi thường đã dùng trong
     booking bị hủy;
   - tạo một `CANCELLATION_COMPENSATION` theo source booking;
   - tạo một `COMPENSATION_TICKET` cho mỗi ghế đã trả tiền;
   - tạo đúng một `COMPENSATION_COMBO` cho booking.
5. Unique constraint theo source booking và khóa đồng thời của từng entitlement
   ngăn cấp hoặc đổi trùng khi request/webhook chạy lặp.
6. Sau commit, gửi email chứa trực tiếp mã voucher vé, mã combo và thời điểm
   hết hạn. Khách guest nhận email theo `BOOKING.guestEmail`.

## 4. Dùng voucher vé

### Khách đăng nhập

Khách lấy ví bồi thường:

```http
GET /api/customer/compensations
Authorization: Bearer <customer-token>
```

Khi checkout, gửi mã theo đúng số ghế muốn miễn phí:

```json
{
  "showtimeId": "SHO-NEW",
  "showtimeSeatIds": ["STS-VIP-01", "STS-4DX-02"],
  "compensationTicketCodes": ["TICKET-CODE-1", "TICKET-CODE-2"]
}
```

Quy tắc tính tiền:

```text
compensationDiscountAmount = tổng seatPrice của các ghế được ghép voucher
totalAmount = tiền ghế + F&B - compensationDiscountAmount
```

Vì giảm đúng `seatPrice`, voucher không có mức trần và không yêu cầu top-up cho
ghế/định dạng cao cấp. F&B mua thêm vẫn được tính tiền bình thường.

### Guest hoặc mua tại quầy

Nhân viên có thể gửi `compensationTicketCodes` qua counter checkout. Nếu không
có `CustomerProfileId`, hệ thống yêu cầu email và số điện thoại khớp booking
gốc trước khi cho dùng mã. Chỉ biết mã voucher là chưa đủ để chiếm quyền sử
dụng.

### Trạng thái giữ chỗ

```text
ISSUED -> RESERVED -> REDEEMED
             |
             +-> ISSUED khi checkout hủy, lỗi hoặc hết hạn
```

- Checkout còn chờ thanh toán chỉ giữ voucher ở `RESERVED`.
- Payment thành công mới chuyển sang `REDEEMED`.
- Booking có tổng tiền bằng 0 được xác nhận và redeem ngay.
- Cleanup payment hết hạn và hủy booking đều trả voucher về `ISSUED`.

## 5. Đổi voucher combo

Staff/Manager/Admin có quyền scan ticket gọi:

```http
POST /api/staff/compensations/combos/redeem
Authorization: Bearer <staff-token>
Content-Type: application/json

{
  "voucherCode": "COMBO-CODE"
}
```

Hệ thống yêu cầu staff profile còn hoạt động, kiểm tra hạn dùng/trạng thái rồi
ghi người đổi, rạp đổi và UTC time. `rowversion` bảo đảm hai quầy không thể đổi
cùng một combo thành công.

## 6. Thanh toán đến muộn

Nếu webhook báo thành công sau khi suất đã bị hủy:

1. Payment vẫn được ghi nhận `SUCCESS` để phản ánh đúng tiền đã thu.
2. Booking/ticket không được mở lại.
3. Không tạo refund cho trường hợp này.
4. Cấp cùng gói voucher bồi thường theo booking và gửi email mã voucher.
5. Webhook lặp chỉ trả lại gói đã cấp, không sinh thêm entitlement.

Thanh toán đến muộn cho booking hết hạn thông thường nhưng suất không bị hủy
vẫn giữ quy trình refund cũ.

## 7. Dữ liệu và triển khai

Các trường/bảng mới:

- `BOOKING.compensationDiscountAmount`
- `CANCELLATION_COMPENSATION`
- `COMPENSATION_TICKET`
- `COMPENSATION_COMBO`

Triển khai database đang có dữ liệu bằng EF migration hoặc một data migration
được review. Ví dụ áp dụng migration:

```powershell
dotnet ef database update --project CinemaSystem.Infrastructure --startup-project CinemaSystem
```

Không chạy schema reset trên môi trường cần giữ dữ liệu vì
`cinema-booking-schema.sql` là script drop/recreate chuẩn.

## 8. Điểm kiểm thử bắt buộc

- Một booking nhiều ghế nhận đúng một voucher/ghế và một combo.
- Hủy lặp hoặc webhook lặp không cấp trùng.
- Voucher trả toàn bộ giá ghế VIP/IMAX/4DX ở rạp khác.
- Không dùng chung voucher thường và voucher bồi thường.
- Voucher `RESERVED` được trả lại khi checkout thất bại/hết hạn.
- Payment thành công chuyển voucher sang `REDEEMED`.
- Guest phải khớp email và số điện thoại booking gốc.
- Hai staff đổi cùng một combo thì chỉ một request thành công.
- Luồng refund ngoài hủy suất vẫn hoạt động như trước.
