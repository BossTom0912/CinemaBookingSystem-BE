# Báo cáo triển khai Resend và kiểm thử E2E production

Ngày xác minh: 2026-07-27

Frontend production: <https://cinema.beer>

Backend production: <https://cinemabookingsystem-be-production.up.railway.app>

## Kết luận

Luồng xác thực email production đã hoạt động với người dùng thật, không còn giới
hạn ở địa chỉ của nhóm phát triển:

1. Khách hàng đăng ký từ `https://cinema.beer`.
2. Backend Railway tạo bản ghi khách hàng ở trạng thái chờ xác thực.
3. Backend gửi OTP qua Resend HTTPS API bằng domain đã xác minh
   `mail.cinema.beer`.
4. Gmail nhận được email OTP.
5. Khách hàng nhập OTP và tài khoản chuyển sang trạng thái đã xác thực.
6. Khách hàng đăng xuất rồi đăng nhập lại thành công.

Kết quả cuối: **PASS**.

## Kiến trúc production đã nối

```text
Trình duyệt
  -> Vercel FE: https://cinema.beer
  -> Railway BE: https://cinemabookingsystem-be-production.up.railway.app
  -> Resend HTTPS API
  -> Gmail của khách hàng
```

Việc backend ghi nhận tài khoản ở trạng thái chờ xác thực trước khi OTP được xác
nhận là đúng thiết kế. Nếu gửi email thất bại, API không được báo gửi thành công
và trạng thái OTP tạm phải được dọn theo các bản sửa auth đã triển khai.

## Thay đổi backend

Commit production:

- `0b2bde3` — xác nhận kết quả gửi OTP trước khi trả thành công.
- `f6e9312` — giới hạn thời gian lỗi SMTP và dọn trạng thái OTP khi gửi thất bại.
- `ea078a4` — bổ sung provider Resend HTTPS API và kiểm thử.

Cấu hình production sử dụng:

- `EmailSettings__Provider=Resend`
- `EmailSettings__ResendApiBaseUrl=https://api.resend.com/`
- `EmailSettings__SenderEmail=noreply@mail.cinema.beer`
- API key chỉ được lưu trong biến môi trường Railway.

API key onboarding cũ đã bị thu hồi sau khi key production giới hạn quyền gửi
được cấu hình. Không có API key, mật khẩu hay OTP nào được ghi vào source hoặc
báo cáo này.

## Resend và DNS

Domain gửi: `mail.cinema.beer`

Resend domain ID: `920d979b-6b49-4f93-b7cd-b61a5efd0e8d`

Khu vực gửi: Tokyo (`ap-northeast-1`)

Các bản ghi đã được tạo trên Vercel DNS và kiểm tra trực tiếp qua nameserver có
thẩm quyền `ns1.vercel-dns.com`:

| Loại | Tên | Giá trị kiểm tra |
|---|---|---|
| TXT | `resend._domainkey.mail.cinema.beer` | DKIM public key hiện diện |
| TXT | `send.mail.cinema.beer` | SPF có `include:amazonses.com` |
| MX | `send.mail.cinema.beer` | `feedback-smtp.ap-northeast-1.amazonses.com`, priority 10 |

Trạng thái cuối trên Resend: `verified` — domain sẵn sàng gửi email.

## Railway backend

- Project ID: `6ae9df8b-f5ec-43cc-bc08-65a69a791447`
- Service ID: `afedf757-8258-42a1-9b3e-753601ffe06c`
- Deployment ID: `18057936-e739-4f5e-9e28-79d2b3aae83b`
- Trạng thái quan sát: `Active`, service `Online`
- Commit deploy: `ea078a4`
- `GET /api/health`: HTTP `200`, service trả `status=OK`
- Preflight `OPTIONS /api/auth/register` từ origin `https://cinema.beer`:
  HTTP `204`, có `Access-Control-Allow-Origin: https://cinema.beer`

Frontend origin cũ `https://cinemabooking-rho.vercel.app` vẫn được giữ trong
CORS để không làm gãy đường dẫn tương thích.

## Vercel frontend và domain

- Project: `dungshunnguyens-projects/cinemabooking`
- Deployment ID: `AtdmuuearHfjC8sezDACeDhtZPNB`
- Trạng thái: `Ready`, môi trường `Production`
- Source commit FE: `789ebec6f45bf30d8486c5b4a8b838a81bf3d0cf`
- Custom domain: `https://cinema.beer`
- HTTP production: `200`, trả HTML.
- Bundle production tham chiếu đúng Railway backend
  `https://cinemabookingsystem-be-production.up.railway.app`.

Hai thay đổi local có sẵn trong FE (`package.json`, `package-lock.json`) được giữ
nguyên và không tham gia deployment này.

## Build và test backend

Chạy tại solution `CinemaSystem.sln`:

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build CinemaSystem.sln --no-restore -m:1` | PASS — 0 warning, 0 error |
| `dotnet test CinemaSystem.sln --no-build -m:1` | PASS — 360 passed, 4 skipped, 0 failed |
| Test tập trung `ResendEmailSenderTests` | PASS — 4 passed |

Bốn test PostgreSQL integration bị skip theo cấu hình test hiện hành, không phải
lỗi của thay đổi Resend.

## Kiểm thử E2E trên production

Tài khoản test dùng Gmail plus alias đã được che:
`bosstom090305+resend…@gmail.com`.

| Bước | Bằng chứng quan sát | Kết quả |
|---|---|---|
| Mở FE custom domain | `https://cinema.beer` tải thành công | PASS |
| Đăng ký khách hàng mới | FE báo đăng ký thành công và yêu cầu nhập OTP | PASS |
| Nhận email | Gmail nhận email từ Cinema Booking System với tiêu đề OTP | PASS |
| Xác thực OTP | FE báo `Email verified successfully.` | PASS |
| Đăng nhập lần đầu | Hiện lời chào `Resend Production Test` và nút đăng xuất | PASS |
| Đăng xuất, đăng nhập mới | Phiên mới vào trang chủ thành công | PASS |
| Xoay mật khẩu test | FE báo đổi mật khẩu thành công | PASS |
| Đăng nhập bằng mật khẩu mới | Phiên mới vào trang chủ thành công | PASS |
| Kết thúc kiểm thử | Đã đăng xuất tài khoản test | PASS |

OTP và cả hai mật khẩu test không được lưu trong tài liệu. Mật khẩu dùng trong
luồng ban đầu đã được thay bằng giá trị ngẫu nhiên mới sau kiểm thử.

## Phạm vi đã xác minh

Báo cáo này xác nhận luồng đăng ký, giao OTP, xác thực email và đăng nhập trên
production. Nó không phải báo cáo kiểm thử toàn bộ nghiệp vụ đặt vé, thanh toán,
hoàn tiền hoặc vận hành dài hạn của nhà cung cấp email.
