# Hướng dẫn thiết lập VNPAY Sandbox

## Phạm vi

Tích hợp ở backend thực hiện các chức năng sau:

- Tạo URL chuyển hướng sang VNPAY và ký URL bằng chữ ký bảo mật.
- Chỉ xác nhận thanh toán từ callback IPN có chữ ký hợp lệ.
- Cung cấp Return URL để VNPAY chuyển trình duyệt của khách hàng về hệ thống.

Không bắt buộc phải có frontend để kiểm thử tích hợp VNPAY Sandbox ở backend.

## Các URL đăng ký với Merchant VNPAY

VNPAY cần truy cập được backend qua một địa chỉ HTTPS công khai. Khi phát triển
ở máy local, có thể dùng tunnel HTTPS như ngrok và chuyển tiếp tunnel đó tới
cổng của backend.

- Website URL: địa chỉ HTTPS gốc của backend, ví dụ:
  `https://your-subdomain.ngrok-free.app`
- Return URL:
  `https://your-subdomain.ngrok-free.app/api/payment/vnpay-return`
- IPN URL:
  `https://your-subdomain.ngrok-free.app/api/payment/vnpay-ipn`

Không thêm dấu `/` ở cuối Return URL hoặc IPN URL. Nếu URL tunnel thay đổi, phải
cập nhật URL mới ở cả trang đăng ký Merchant VNPAY và cấu hình backend.

### Ý nghĩa của từng URL

- Website URL: địa chỉ đại diện cho website hoặc backend của hệ thống.
- Return URL: VNPAY chuyển trình duyệt của khách hàng về URL này sau khi kết
  thúc quá trình thanh toán. Return URL chỉ dùng để hiển thị kết quả, không
  được dùng làm nguồn xác nhận thanh toán thành công.
- IPN URL: VNPAY gọi trực tiếp từ máy chủ VNPAY đến backend. Đây là nguồn đáng
  tin cậy để backend xác nhận và cập nhật trạng thái thanh toán.

## Cấu hình backend

Không lưu thông tin Merchant thật trong các file `appsettings` được commit lên
Git. Hãy cấu hình bằng .NET User Secrets, biến môi trường hoặc hệ thống quản lý
secret của môi trường deploy.

Chạy các lệnh sau tại thư mục gốc của repository:

```powershell
dotnet user-secrets set "VnPaySettings:Enabled" "true" --project CinemaSystem
dotnet user-secrets set "VnPaySettings:TmnCode" "YOUR_TMN_CODE" --project CinemaSystem
dotnet user-secrets set "VnPaySettings:HashSecret" "YOUR_HASH_SECRET" --project CinemaSystem
dotnet user-secrets set "VnPaySettings:PaymentUrl" "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html" --project CinemaSystem
dotnet user-secrets set "VnPaySettings:ReturnUrl" "https://YOUR_PUBLIC_BACKEND_HOST/api/payment/vnpay-return" --project CinemaSystem
dotnet user-secrets set "VnPaySettings:PreferredBankCode" "INTCARD" --project CinemaSystem
```

Thay các giá trị sau bằng thông tin thật được VNPAY cấp:

- `YOUR_TMN_CODE`: mã website hoặc mã terminal `TmnCode`.
- `YOUR_HASH_SECRET`: chuỗi bí mật dùng để ký và kiểm tra chữ ký.
- `YOUR_PUBLIC_BACKEND_HOST`: domain HTTPS công khai của backend, không gồm
  dấu `/` ở cuối.

Ví dụ khi URL ngrok là `https://example.ngrok-free.app`:

```powershell
dotnet user-secrets set "VnPaySettings:ReturnUrl" "https://example.ngrok-free.app/api/payment/vnpay-return" --project CinemaSystem
```

`INTCARD` yêu cầu VNPAY mở luồng thanh toán bằng thẻ quốc tế, bao gồm thẻ Visa.
Nếu muốn khách hàng tự chọn kênh thanh toán trên trang VNPAY, hãy xóa cấu hình
`PreferredBankCode` hoặc đặt giá trị này thành chuỗi rỗng.

Các giá trị sau là mặc định của giao thức nhưng vẫn được phép cấu hình:

```json
{
  "VnPaySettings": {
    "Locale": "vn",
    "OrderType": "other"
  }
}
```

## Dữ liệu nhà cung cấp thanh toán trong database

Schema chuẩn, script nâng cấp database và quá trình seed khi backend khởi động
đều thêm dữ liệu sau theo cách idempotent:

```text
paymentProviderId: PP_VNPAY
providerName: VNPAY
providerStatus: ACTIVE
```

Idempotent nghĩa là có thể chạy lại nhiều lần mà không tạo bản ghi trùng.

Bản ghi provider cố ý để `apiEndpoint` bằng `NULL`. URL VNPAY Sandbox hoặc
Production phụ thuộc môi trường chạy nên phải nằm trong cấu hình, không được
hardcode cố định trong database.

## Các bước kiểm thử API

1. Khởi động backend.
2. Khởi động tunnel HTTPS công khai trỏ tới backend.
3. Đăng nhập tài khoản có role Customer trong Swagger.
4. Nhấn `Authorize` và nhập access token của Customer.
5. Tạo hoặc chọn một booking thuộc Customer đó, có trạng thái
   `PENDING_PAYMENT`.
6. Gọi `POST /api/payment` với request:

```json
{
  "bookingId": "YOUR_PENDING_BOOKING_ID",
  "paymentProviderId": "PP_VNPAY"
}
```

7. Nếu thành công, API trả response tương tự:

```json
{
  "success": true,
  "message": "Payment created.",
  "data": {
    "paymentId": "PAY_...",
    "amount": 120000,
    "transactionCode": "TXXXXXXXXXX",
    "paymentProviderName": "VNPAY",
    "bankName": "",
    "bankAccount": "",
    "checkoutUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?...",
    "expiresAt": "2026-07-17T10:30:00Z"
  },
  "errorCode": null,
  "errors": null
}
```

8. Sao chép hoặc mở giá trị `data.checkoutUrl`.
9. Trong lần smoke test đầu tiên, chỉ cần xác nhận trang thanh toán VNPAY
   Sandbox mở được. Không cần nhập thông tin thẻ hoặc thực hiện thanh toán.

## Luồng xử lý

```text
Customer gọi POST /api/payment
        |
        v
Backend kiểm tra booking, quyền sở hữu và PP_VNPAY
        |
        v
Backend tạo PAYMENT trạng thái PENDING
        |
        v
Backend tạo và ký checkoutUrl bằng HashSecret
        |
        v
Customer mở checkoutUrl trên VNPAY
        |
        +------> Return URL: trả kết quả hiển thị cho trình duyệt
        |
        +------> IPN URL: xác nhận kết quả thanh toán với backend
```

## Quyết định bảo mật và giao thức

- `TmnCode`, `HashSecret`, Payment URL, Return URL và kênh ngân hàng ưu tiên là
  cấu hình môi trường.
- Backend không trả `HashSecret` trong response và không ghi secret vào log.
- Các giá trị cố định trong code gồm phiên bản VNPAY `2.1.0`, command `pay`,
  tiền tệ `VND`, hệ số số tiền `100`, múi giờ GMT+7 và thuật toán HMAC-SHA512.
  Đây là quy định cố định của giao thức VNPAY, không phải dữ liệu môi trường.
- VNPAY nhận số tiền bằng đơn vị nhỏ hơn 100 lần. Ví dụ `120.000 VND` được gửi
  dưới dạng `12000000`.
- Return URL không được phép tự đánh dấu payment thành công.
- Chỉ IPN có chữ ký hợp lệ mới được gọi luồng xác nhận payment trong database.
- IPN kiểm tra chữ ký, mã giao dịch, provider, số tiền và khả năng xử lý callback
  lặp lại.
- Nếu giao dịch thất bại hoặc bị hủy, hệ thống đánh dấu payment thất bại, hủy
  booking, giải phóng ghế và trả voucher đang được giữ.

## Nguồn tham khảo

- Tài liệu thanh toán VNPAY Sandbox chính thức:
  <https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html>
