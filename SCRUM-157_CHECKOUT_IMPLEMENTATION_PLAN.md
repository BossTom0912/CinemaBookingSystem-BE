# SCRUM-157 - Checkout API Implementation Plan

Ngày lập: 07/06/2026  
Nhánh: `Scrum-157-Checkout`

## 1. Mục tiêu

Xây dựng API Checkout cho Customer đã đăng nhập:

```http
POST /api/bookings/checkout
Authorization: Bearer <access-token>
```

Checkout chịu trách nhiệm:

1. Xác minh các ghế đã được chính Customer khóa và khóa còn hiệu lực.
2. Tính lại toàn bộ giá ở Backend.
3. Kiểm tra F&B, voucher và điểm thưởng nếu được áp dụng.
4. Tạo `BOOKING` ở trạng thái chờ thanh toán.
5. Tạo snapshot ghế, F&B và voucher usage.
6. Trả `bookingId`, chi tiết tiền và thời hạn thanh toán cho FE.

Checkout không chịu trách nhiệm:

- Không tích hợp VNPAY/MoMo.
- Không tạo payment URL.
- Không nhận payment callback.
- Không chuyển booking sang `PAID`.
- Không chuyển ghế sang `BOOKED`.
- Không phát hành QR ticket.
- Không gửi email vé.

Luồng tổng thể:

```text
Lock Seat
  -> SCRUM-157 Checkout
  -> SCRUM-158 CreatePayment
  -> SCRUM-159 PaymentWebhook
  -> SCRUM-160 Generate E-Ticket
```

## 2. Căn cứ nghiệp vụ

Các business rule liên quan trực tiếp:

| Rule | Áp dụng trong Checkout |
|---|---|
| BR-07 | Không checkout khi đã qua thời gian đóng bán online. |
| BR-08 | Booking chưa được xác nhận trước payment success. |
| BR-09 | Ghế chỉ được giữ tạm khi chưa thanh toán. |
| BR-11 | Một ghế trong một suất chỉ được bán thành công cho một Customer. |
| BR-12 | Ghế được khóa tạm, ví dụ 10 phút. |
| BR-13 | Hết thời gian phải giải phóng ghế. |
| BR-15 | Không chọn ghế booked hoặc locked bởi người khác. |
| BR-16 | Có thể kiểm tra rule chống ghế mồ côi. |
| BR-17 đến BR-24 | Kiểm tra phân loại độ tuổi nếu đủ dữ liệu. |
| BR-25 đến BR-29 | Backend chịu trách nhiệm tính và hiển thị giá. |
| BR-30, BR-33 | Checkout không chuyển Paid và không tạo ticket. |
| BR-47 đến BR-49 | F&B là tùy chọn và có thể sửa trước payment. |
| BR-53 đến BR-57 | Validate voucher; chưa trừ lượt trước payment success. |
| BR-58, BR-59 | Validate điểm và phải lưu được lịch sử sử dụng. |

## 3. Dependency và giả định

### 3.1. Seat Lock phải tồn tại trước về mặt nghiệp vụ

Checkout không tự biến ghế `AVAILABLE` thành `LOCKED`. Nó chỉ chấp nhận ghế đã được Seat Lock API khóa:

```text
seatStatus     = LOCKED
lockedByUserId = currentUserId
lockedUntil    > utcNow
```

SCRUM-157 có thể được code và test trước Seat Lock bằng fixture/seed dữ liệu khóa ghế. Tuy nhiên chưa thể test E2E từ UI nếu Seat Lock API chưa tồn tại.

### 3.2. Contract với CreatePayment

CreatePayment chỉ cần nhận:

```json
{
  "bookingId": "BKG_xxx"
}
```

CreatePayment phải đọc `BOOKING.totalAmount` từ DB. Không được nhận hoặc tin `amount` từ FE.

### 3.3. Contract với PaymentWebhook

Webhook thành công phải xử lý booking do Checkout tạo:

```text
PENDING_PAYMENT -> PAID
LOCKED seats     -> BOOKED
APPLIED voucher  -> CONFIRMED
reserved points  -> actual REDEEM
reserved F&B     -> actual deduction
generate tickets
```

## 4. Các quyết định cần chốt trước khi code toàn bộ scope

### 4.1. Reward point chưa có nơi lưu trạng thái pending

Schema chỉ có:

- `CUSTOMER_PROFILE.rewardPoints`
- `REWARD_POINT_TRANSACTION`

`BOOKING` không có `rewardPointsToUse` hoặc `rewardDiscountAmount`.  
`REWARD_POINT_TRANSACTION` không có trạng thái `RESERVED/PENDING/CONFIRMED/CANCELLED`.

Nếu Checkout chỉ trả số điểm trong response, PaymentWebhook không biết booking đã yêu cầu dùng bao nhiêu điểm. Nếu trừ điểm ngay ở Checkout thì vi phạm BR-56/BR-58 theo tinh thần chỉ xác nhận quyền lợi sau payment và gây phức tạp khi timeout.

Khuyến nghị:

1. Bổ sung dữ liệu reservation cho điểm trước khi triển khai reward point trong SCRUM-157; hoặc
2. Chốt SCRUM-157 phase 1 chưa nhận `rewardPointsToUse`.

Không nên lưu tạm thông tin quan trọng này trong memory/cache không bền vững.

### 4.2. F&B chưa có cơ chế reserve inventory

`CINEMA_FB_INVENTORY` chỉ có `quantity`, không có `reservedQuantity`.

Nếu chỉ kiểm tra tồn kho tại Checkout nhưng trừ ở payment success, nhiều booking pending có thể cùng nhìn thấy một lượng tồn và oversell. Nếu trừ ngay tại Checkout, timeout/payment fail phải hoàn tồn chính xác.

Khuyến nghị chốt một trong hai:

1. Thêm cơ chế reserve inventory; hoặc
2. Trừ tồn ở Checkout và bắt buộc release trong timeout/payment-fail transaction; hoặc
3. Phase 1 chỉ tạo line item và chấp nhận revalidate khi payment success, đồng thời ghi nhận đây là rủi ro nghiệp vụ.

Phương án 1 sạch nhất.

### 4.3. `BOOKING_SEAT` unique có thể khóa ghế vĩnh viễn sau booking fail

`UQ_BOOKING_SEAT_SHOWTIME_SEAT` đảm bảo một `showtimeSeatId` chỉ xuất hiện một lần trong toàn bộ `BOOKING_SEAT`.

Nếu booking pending bị `CANCELLED` nhưng dòng `BOOKING_SEAT` vẫn còn, ghế đó không thể được thêm vào booking mới dù đã release.

Cần chốt:

1. Retry payment trên cùng booking trong thời hạn cho phép.
2. Khi unpaid booking timeout/cancel, xóa `BOOKING_SEAT` để ghế có thể bán lại; hoặc
3. Điều chỉnh schema để lưu lịch sử reservation mà không cản lần bán sau.

Với schema hiện tại, phương án thực dụng là xóa line của unpaid booking khi release, nhưng sẽ mất lịch sử ghế của booking thất bại.

## 5. API Contract

### 5.1. Authorization

```csharp
[Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
```

User ID lấy từ JWT claim `userId`. Không nhận `userId` hoặc `customerProfileId` từ request.

### 5.2. Request phase 1 an toàn

```json
{
  "showtimeId": "SHO_001",
  "showtimeSeatIds": [
    "STS_001",
    "STS_002"
  ],
  "foodItems": [
    {
      "fbItemId": "FB_001",
      "quantity": 2
    }
  ],
  "voucherCode": "SUMMER50"
}
```

Không nhận các giá trị do FE tự tính:

- `seatPrice`
- `unitPrice`
- `subtotal`
- `discountAmount`
- `totalAmount`
- `customerProfileId`
- `bookingStatus`
- `expiredAt`

### 5.3. Validation DTO

`CheckoutRequest`:

- `ShowtimeId`: required, max length 50.
- `ShowtimeSeatIds`: required, ít nhất một phần tử.
- Không cho ID rỗng hoặc trùng nhau.
- Giới hạn số ghế mỗi booking cần lấy từ cấu hình/business rule; nếu chưa chốt thì dùng hằng số có tên, không magic number.
- `FoodItems`: optional.
- Mỗi `FbItemId`: required, max length 50.
- `Quantity`: lớn hơn 0.
- Gộp item trùng hoặc trả validation error; khuyến nghị trả lỗi để request minh bạch.
- `VoucherCode`: optional, trim và normalize theo quy ước voucher.

### 5.4. Success response

HTTP `201 Created`:

```json
{
  "success": true,
  "message": "Checkout created successfully.",
  "data": {
    "bookingId": "BKG_001",
    "bookingStatus": "PENDING_PAYMENT",
    "showtimeId": "SHO_001",
    "seats": [
      {
        "showtimeSeatId": "STS_001",
        "seatCode": "A1",
        "seatType": "VIP",
        "price": 120000
      }
    ],
    "foodItems": [
      {
        "fbItemId": "FB_001",
        "itemName": "Popcorn",
        "quantity": 2,
        "unitPrice": 40000,
        "subtotal": 80000
      }
    ],
    "seatSubtotal": 240000,
    "foodSubtotal": 80000,
    "grossAmount": 320000,
    "voucherDiscount": 30000,
    "rewardDiscount": 0,
    "totalAmount": 290000,
    "expiredAt": "2026-06-07T10:10:00Z"
  },
  "errorCode": null,
  "errors": null
}
```

`rewardDiscount` nên để `0` trong phase 1 nếu reward reservation chưa được thiết kế.

## 6. HTTP status và error code

| HTTP | Error code | Trường hợp |
|---|---|---|
| 400 | `VALIDATION_ERROR` | DTO sai, ID trùng, quantity không hợp lệ. |
| 400 | `INVALID_SEAT_SELECTION` | Ghế không cùng showtime, sai room hoặc vi phạm orphan-seat rule. |
| 400 | `VOUCHER_MIN_ORDER_NOT_MET` | Không đủ giá trị đơn tối thiểu. |
| 400 | `INSUFFICIENT_REWARD_POINTS` | Không đủ điểm, khi feature được hỗ trợ. |
| 401 | `UNAUTHORIZED` | Không có/không hợp lệ JWT. |
| 403 | `BOOKING_NOT_ALLOWED` | User không thuộc policy Customer hoặc không active. |
| 404 | `CUSTOMER_PROFILE_NOT_FOUND` | User Customer không có profile. |
| 404 | `SHOWTIME_NOT_FOUND` | Showtime không tồn tại. |
| 404 | `SHOWTIME_SEAT_NOT_FOUND` | Có ID ghế không tồn tại. |
| 404 | `FB_ITEM_NOT_FOUND` | F&B item không tồn tại. |
| 404 | `VOUCHER_NOT_FOUND` | Voucher code không tồn tại. |
| 409 | `SHOWTIME_NOT_OPEN` | Showtime closed/cancelled/completed. |
| 409 | `ONLINE_SALE_CLOSED` | Đã qua cutoff time BR-07. |
| 409 | `SEAT_NOT_LOCKED_BY_USER` | Ghế không do current user khóa. |
| 409 | `SEAT_LOCK_EXPIRED` | Khóa đã hết hạn. |
| 409 | `SEAT_UNAVAILABLE` | Ghế booked/unavailable hoặc đã có booking seat. |
| 409 | `INSUFFICIENT_FB_STOCK` | Không đủ tồn tại cinema của showtime. |
| 409 | `VOUCHER_EXPIRED` | Ngoài thời gian hiệu lực. |
| 409 | `VOUCHER_USAGE_LIMIT_REACHED` | Hết quota toàn chiến dịch. |
| 409 | `VOUCHER_CUSTOMER_LIMIT_REACHED` | Customer đã đạt quota. |
| 409 | `CHECKOUT_CONCURRENCY_CONFLICT` | `rowVersion` thay đổi trong lúc checkout. |

Không dùng chung `BAD_REQUEST` cho mọi lỗi nghiệp vụ vì FE cần error code ổn định.

## 7. Thuật toán xử lý

### Bước 1: Lấy Customer hiện tại

1. Đọc `userId` từ JWT.
2. Tìm `CUSTOMER_PROFILE` theo `userId`.
3. Không tin customer/profile ID từ client.

### Bước 2: Load Showtime

Query cần lấy:

```text
SHOWTIME
  -> ROOM
  -> CINEMA
  -> MOVIE
```

Kiểm tra:

- Showtime tồn tại.
- `status == OPEN`.
- Room và cinema ở trạng thái cho phép bán.
- Chưa qua online sale cutoff.
- Phim không thuộc trạng thái cấm bán.

Cutoff không được hard-code trong service. Đặt trong cấu hình, ví dụ:

```text
BookingSettings:OnlineSaleCutoffMinutes
```

### Bước 3: Load toàn bộ ghế bằng một query

Load `SHOWTIME_SEAT` kèm:

```text
SEAT -> SEAT_TYPE
```

Sau đó kiểm tra:

- Số dòng DB bằng số ID distinct trong request.
- Tất cả cùng `request.showtimeId`.
- Tất cả ghế physical đang active.
- Tất cả `seatStatus == LOCKED`.
- Tất cả `lockedByUserId == currentUserId`.
- Tất cả `lockedUntil > utcNow`.
- Chưa có `BOOKING_SEAT`.

Không query từng ghế trong vòng lặp.

### Bước 4: Kiểm tra seat-selection rule

Nếu BR-16 được bật:

- Lấy toàn bộ seat map của room.
- Mô phỏng trạng thái sau khi chọn.
- Không để lại một ghế available đơn lẻ bị kẹp giữa unavailable/booked/selected hoặc mép theo rule đã chốt.

Rule này nên nằm trong service riêng hoặc pure function để unit test, không viết trực tiếp trong controller.

### Bước 5: Tính tiền ghế

Với schema hiện tại:

```text
seatPrice = SHOWTIME.basePrice + SEAT_TYPE.extraFee
seatSubtotal = Sum(seatPrice)
```

Nếu BR-25 yêu cầu giá theo ngày, giờ, cinema hoặc format nhưng schema chưa có price-rule table, phase 1 chỉ có thể dùng công thức trên. Phải ghi rõ đây là giới hạn hiện tại, không tự bịa thêm phụ phí.

### Bước 6: Validate và tính F&B

1. Load tất cả `FB_ITEM` bằng một query.
2. Load inventory theo `cinemaId + fbItemIds`.
3. Item phải `AVAILABLE`.
4. Inventory phải tồn tại và đủ quantity.
5. Tính bằng giá DB:

```text
subtotal = FB_ITEM.price * quantity
```

Lưu `unitPrice` và `subtotal` làm snapshot.

### Bước 7: Validate voucher

Nếu request có voucher:

1. Tìm theo normalized code.
2. `voucherStatus == ACTIVE`.
3. `startDate <= utcNow < endDate`.
4. `usedCount < usageLimit`.
5. Gross amount đạt `minOrderAmount`.
6. Đếm số `VOUCHER_USAGE` của customer ở trạng thái `CONFIRMED`; chưa vượt `perCustomerLimit`.
7. Tính discount:

```text
AMOUNT:
  discount = discountValue

PERCENT:
  discount = grossAmount * discountValue / 100

Nếu có maxDiscountAmount:
  discount = Min(discount, maxDiscountAmount)

discount = Min(discount, grossAmount)
```

Nếu `PERCENT`, application phải chặn `discountValue > 100` vì DB chưa có constraint này.

### Bước 8: Điểm thưởng

Chỉ triển khai sau khi chốt:

- Tỷ lệ quy đổi.
- Min/max point.
- Điểm áp dụng trên vé hay cả F&B.
- Cơ chế reserve.
- Cách webhook xác nhận.
- Cách timeout trả reservation.

Cho tới khi chốt, không thêm một field request “có vẻ chạy được” nhưng không thể persistence đúng.

### Bước 9: Tính tổng

```text
grossAmount      = seatSubtotal + foodSubtotal
voucherDiscount  = calculated voucher discount
rewardDiscount   = 0 trong phase 1
totalAmount      = Max(0, grossAmount - voucherDiscount - rewardDiscount)
```

Tất cả dùng `decimal`, không dùng `double`.

Quy tắc làm tròn phải chốt. Với VND, đề xuất làm tròn đến đơn vị đồng hoặc theo yêu cầu provider, nhưng DB hiện dùng 2 chữ số thập phân.

### Bước 10: Ghi dữ liệu trong transaction

Mở EF Core transaction và tạo:

#### BOOKING

```text
bookingId         = ID được sinh ở server
customerProfileId = current customer
showtimeId        = request showtime
bookingChannel    = ONLINE
bookingStatus     = PENDING_PAYMENT
totalAmount       = calculated total
createdAt         = utcNow
expiredAt         = Min(lockedUntil của các ghế)
```

Không tự kéo dài lock tại Checkout trừ khi business rule quy định rõ.

#### BOOKING_SEAT

Mỗi ghế một dòng:

```text
bookingId
showtimeSeatId
seatPrice = calculated snapshot
```

#### BOOKING_FB_ITEM

Mỗi item một dòng:

```text
bookingId
fbItemId
quantity
unitPrice
subtotal
```

#### VOUCHER_USAGE

Nếu có voucher:

```text
usageStatus   = APPLIED
discountAmount = calculated discount
usedAt        = null
```

Không tăng `VOUCHER.usedCount`.

### Bước 11: Xử lý concurrency

`SHOWTIME_SEAT.rowVersion` đã được EF map làm concurrency token.

Trước commit:

- Có thể cập nhật lại một trường lock hoặc attach original row version để bảo đảm trạng thái ghế không thay đổi.
- Bắt `DbUpdateConcurrencyException`.
- Rollback và trả `409 CHECKOUT_CONCURRENCY_CONFLICT`.

Unique violation của `UQ_BOOKING_SEAT_SHOWTIME_SEAT` cũng phải map thành `409 SEAT_UNAVAILABLE`, không trả 500.

### Bước 12: Commit và response

Chỉ trả success sau khi transaction commit.

Không tạo `PAYMENT` trong SCRUM-157 nếu SCRUM-158 được giao riêng. CreatePayment sẽ tạo payment attempt sau khi nhận `bookingId`.

## 8. Cấu trúc code đề xuất

Giữ Controller mỏng và theo pattern hiện tại:

```text
CinemaSystem.Contracts/
  Bookings/
    CheckoutRequest.cs
    CheckoutFoodItemRequest.cs
    CheckoutResponse.cs
    CheckoutSeatResponse.cs
    CheckoutFoodItemResponse.cs

CinemaSystem.Application/
  Interfaces/
    ICheckoutService.cs
  Common/
    BookingConstants.cs
  Bookings/
    CheckoutCalculation.cs

CinemaSystem.Infrastructure/
  Bookings/
    CheckoutService.cs
  Configuration/
    BookingSettings.cs

CinemaSystem/
  Controllers/
    BookingsController.cs

CinemaSystem.Tests/
  CheckoutServiceTests.cs
```

Interface:

```csharp
Task<ServiceResult<CheckoutResponse>> CheckoutAsync(
    string userId,
    CheckoutRequest request,
    CancellationToken cancellationToken);
```

Controller:

1. Lấy `userId` claim.
2. Gọi `ICheckoutService`.
3. Chuyển `ServiceResult` thành `ApiResponse`.
4. Không query `CinemaDbContext`.
5. Không tính tiền.

## 9. Constants cần có

Không rải chuỗi trạng thái trong service:

```text
BookingStatus.PendingPayment
BookingChannel.Online
ShowtimeStatus.Open
ShowtimeSeatStatus.Locked
VoucherStatus.Active
VoucherUsageStatus.Applied
FbItemStatus.Available
DiscountType.Amount
DiscountType.Percent
```

Các chuỗi phải khớp chính xác check constraint trong DB.

## 10. Logging

Nên log metadata:

- `bookingId`
- `userId`
- `showtimeId`
- số ghế
- tổng tiền
- voucher ID nếu có
- kết quả transaction/concurrency conflict

Không log:

- JWT
- thông tin thanh toán
- token/secret
- toàn bộ request nếu có PII

## 11. Test plan

### 11.1. Happy path

1. Một ghế locked đúng user, không F&B/voucher.
2. Nhiều ghế locked đúng user.
3. Có F&B hợp lệ.
4. Voucher `AMOUNT`.
5. Voucher `PERCENT` có max discount.
6. Booking được tạo `PENDING_PAYMENT`.
7. `expiredAt` bằng lock sớm nhất.
8. Giá response khớp snapshot DB.

### 11.2. Seat validation

1. Ghế không tồn tại.
2. Ghế thuộc showtime khác.
3. Ghế trùng ID trong request.
4. Ghế `AVAILABLE`, chưa lock.
5. Ghế locked bởi user khác.
6. Ghế hết hạn.
7. Ghế `BOOKED`.
8. Ghế physical inactive.
9. Một ghế đã có `BOOKING_SEAT`.
10. Concurrency: hai request checkout cùng tập ghế, chỉ một request thành công.

### 11.3. Showtime

1. Showtime không tồn tại.
2. Showtime `CLOSED`.
3. Showtime `CANCELLED`.
4. Qua online-sale cutoff.
5. Room/cinema không hoạt động.

### 11.4. F&B

1. Item không tồn tại.
2. Item unavailable.
3. Không có inventory tại cinema.
4. Quantity lớn hơn tồn.
5. Quantity bằng 0/âm.
6. Item trùng trong request.
7. Backend bỏ qua giá giả do FE không được gửi giá.

### 11.5. Voucher

1. Code không tồn tại.
2. Inactive.
3. Chưa tới ngày hiệu lực.
4. Hết hạn.
5. Không đạt min order.
6. Hết usage limit.
7. Customer đạt per-customer limit.
8. Percent discount có max cap.
9. Discount không làm total âm.
10. Voucher usage được tạo `APPLIED`, usedCount chưa tăng.

### 11.6. Transaction

1. Lỗi khi insert một `BOOKING_SEAT` phải rollback toàn bộ booking.
2. Lỗi khi insert F&B phải rollback booking và voucher usage.
3. Unique/concurrency conflict trả 409.
4. CancellationToken hủy operation không để dữ liệu dở dang.

## 12. Definition of Done

- Endpoint yêu cầu policy `CanBookTicket`.
- Không nhận giá hoặc customer ID từ FE.
- Controller không dùng DbContext và không chứa business logic.
- Toàn bộ query dùng async EF Core.
- Tất cả write chạy trong một transaction.
- Booking được tạo `PENDING_PAYMENT`, không phải `PAID`.
- Ghế vẫn `LOCKED`, chưa chuyển `BOOKED`.
- Voucher usage chỉ `APPLIED`, chưa tăng used count.
- Không tạo payment hoặc ticket.
- Error code ổn định và đúng HTTP status.
- Có unit/integration test cho happy path, expired lock, wrong owner, invalid voucher và concurrency.
- `dotnet build` và `dotnet test` thành công.

## 13. Vấn đề ngoài scope nhưng phải báo cho BE Payment

EF model hiện map:

```text
Booking 1:0..1 Payment
```

trong khi DDL/tài liệu yêu cầu:

```text
Booking 1:0..N Payment attempts
```

SCRUM-158/159 phải sửa navigation/mapping này trước khi hỗ trợ retry payment. SCRUM-157 không nên tạo `PAYMENT`, nên chưa cần sửa mapping trong task Checkout nếu muốn giữ phạm vi thay đổi nhỏ.

## 14. Thứ tự triển khai đề xuất

1. Chốt ba quyết định: reward reservation, F&B reservation, cleanup `BOOKING_SEAT`.
2. Chốt online-sale cutoff và giới hạn số ghế.
3. Tạo DTO và API contract.
4. Tạo constants và `BookingSettings`.
5. Implement checkout ghế cơ bản.
6. Thêm F&B.
7. Thêm voucher.
8. Chỉ thêm reward point sau khi schema/flow được chốt.
9. Thêm transaction và concurrency mapping.
10. Viết integration tests.
11. Test contract với SCRUM-158 bằng `bookingId`.
