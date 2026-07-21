# Chạy CinemaSystem Backend bằng Docker (Windows)

## Mục tiêu

Docker chỉ đóng gói **Backend** ở cổng `5070`. SQL Server hiện có trên máy được giữ nguyên để không mất dữ liệu test. Docker không tự tạo URL Internet; cần ngrok riêng cho BE khi VNPAY hoặc SePay gửi callback.

## Lần đầu cấu hình

1. Đảm bảo Docker Desktop đang chạy, rồi kiểm tra tại PowerShell:

   ```powershell
   docker --version
   docker compose version
   ```

2. Tại thư mục gốc `CinemaSystem_BE`, tạo file cấu hình cục bộ:

   ```powershell
   Copy-Item .env.example .env
   ```

3. Mở `.env` và thay ít nhất ba giá trị:

   - `ConnectionStrings__DefaultConnection`: connection string của SQL Server trên Windows. Giữ `host.docker.internal` vì từ container, `localhost` là chính container chứ không phải máy Windows.
   - `JwtSettings__Secret` và `SecuritySettings__ConfirmationTokenSecret`: hai chuỗi ngẫu nhiên khác nhau, mỗi chuỗi ít nhất 32 ký tự.
   - `CorsSettings__AllowedOrigins__2`: URL FE ngrok hiện tại, không có dấu `/` ở cuối.

   Nếu SQL Server không nghe tại TCP port `1433`, thay `1433` bằng port thực tế. Cần bật TCP/IP cho SQL Server để container truy cập được.

4. Dừng BE đang chạy trong Visual Studio hoặc terminal để giải phóng cổng `5070`.

## Chạy backend

```powershell
docker compose up --build
```

Sau khi API khởi động, mở Swagger tại:

```text
http://localhost:5070/swagger
```

Lần sau chỉ cần:

```powershell
docker compose up
```

Các lệnh hỗ trợ:

```powershell
docker compose logs -f api
docker compose down
docker compose down -v
```

`docker compose down -v` xóa volume upload (`cinema_uploads`), nên chỉ dùng khi thật sự muốn xóa các file ảnh đã upload qua container. Nó không xóa SQL Server trên máy Windows.

## Ngrok và callback thanh toán

Mở terminal thứ hai, khi Docker API đang chạy:

```powershell
& "D:\New folder\ngrok-v3-stable-windows-amd64\ngrok.exe" http 127.0.0.1:5070
```

Chỉ dùng URL tunnel **BE** vừa tạo:

| Dịch vụ | URL cần cấu hình |
| --- | --- |
| SePay webhook | `https://<BE_NGROK>/api/Payment/sepay-webhook` |
| VNPAY Return URL | `https://<BE_NGROK>/api/payment/vnpay-return` |
| VNPAY IPN URL | `https://<BE_NGROK>/api/payment/vnpay-ipn` |

Không dùng FE ngrok cổng `5173` cho ba URL trên.

## Khi checkout nhánh VNPAY

Sau khi chuyển sang nhánh có VNPAY, thêm các biến sau vào `.env` bằng TMN Code và Hash Secret do VNPAY cấp:

```env
VnPaySettings__Enabled=true
VnPaySettings__TmnCode=REPLACE_WITH_VNPAY_TMN_CODE
VnPaySettings__HashSecret=REPLACE_WITH_VNPAY_HASH_SECRET
VnPaySettings__PaymentUrl=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
VnPaySettings__ReturnUrl=https://<BE_NGROK>/api/payment/vnpay-return
VnPaySettings__PreferredBankCode=INTCARD
VnPaySettings__Locale=vn
VnPaySettings__OrderType=other
```

Không commit `.env` và không đưa TMN Code, Hash Secret, JWT secret, password SQL/SMTP vào source code hay ảnh chụp.
