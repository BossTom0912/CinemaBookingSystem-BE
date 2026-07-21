# Hướng dẫn FE chạy qua ngrok và gọi CinemaSystem BE

Tài liệu này dành cho thành viên Frontend khi cần demo FE/BE từ Internet hoặc
kiểm tra callback payment trên máy local.

## 1. Nguyên tắc quan trọng

FE và BE phải dùng **hai tunnel ngrok khác nhau**.

| Thành phần | Port local ví dụ | URL ngrok dùng cho |
|---|---:|---|
| React/Vite FE | `5173` | Người dùng mở giao diện web |
| CinemaSystem BE | `5070` | FE gọi API, SePay callback, VNPAY callback |

Không dùng URL ngrok FE cho webhook payment. FE không có route
`/api/Payment/sepay-webhook`, `/api/payment/vnpay-ipn` hay
`/api/payment/vnpay-return`.

## 2. Chạy FE

Tại thư mục gốc FE:

```powershell
npm.cmd install
npm.cmd run dev -- --host 0.0.0.0
```

Giữ terminal này mở. Vite thường hiển thị `http://localhost:5173`; nếu nó dùng
port khác thì dùng đúng port Vite thông báo ở các lệnh ngrok tiếp theo.

## 3. Tạo tunnel ngrok cho FE

Mở terminal khác:

```powershell
& "D:\New folder\ngrok-v3-stable-windows-amd64\ngrok.exe" http 127.0.0.1:5173
```

Ví dụ kết quả:

```text
https://fe-example.ngrok-free.app -> http://127.0.0.1:5173
```

Mở URL HTTPS này để truy cập FE từ Internet.

## 4. Cho phép hostname ngrok trong Vite

Vite chặn hostname lạ để chống DNS rebinding. Không hardcode hostname ngrok vì
URL ngrok Free có thể đổi sau mỗi lần chạy.

Trong `vite.config.ts`:

```ts
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const allowedHosts = (env.VITE_ALLOWED_HOSTS || '')
    .split(',')
    .map((host) => host.trim())
    .filter(Boolean)

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: { '@': path.resolve(__dirname, './src') },
    },
    server: {
      host: '0.0.0.0',
      allowedHosts,
    },
  }
})
```

Tạo `.env.local` ở cùng cấp `vite.config.ts` (không commit file này):

```env
VITE_ALLOWED_HOSTS=fe-example.ngrok-free.app
VITE_API_BASE_URL=https://be-example.ngrok-free.app
```

- `VITE_ALLOWED_HOSTS` chỉ là hostname, không có `https://` và không có dấu
  `/` ở cuối.
- `VITE_API_BASE_URL` là URL HTTPS đầy đủ của **BE ngrok**, không phải
  `localhost` và không phải URL FE ngrok.

Sau khi thay `.env.local` hoặc `vite.config.ts`, dừng Vite bằng `Ctrl+C` và
chạy lại lệnh ở bước 2.

## 5. Tạo tunnel ngrok cho BE

Chạy CinemaSystem API trước, sau đó mở một tunnel riêng:

```powershell
& "D:\New folder\ngrok-v3-stable-windows-amd64\ngrok.exe" http 127.0.0.1:5070
```

Ví dụ:

```text
https://be-example.ngrok-free.app -> http://127.0.0.1:5070
```

Kiểm tra nhanh:

```text
https://be-example.ngrok-free.app/swagger/index.html
```

## 6. Payment callbacks

### SePay

Cấu hình Webhook URL trong dashboard SePay:

```text
https://be-example.ngrok-free.app/api/Payment/sepay-webhook
```

Nếu SePay báo `ERR_NGROK_3200`, URL tunnel đã offline. Nếu báo `404` nhưng URL
là tunnel FE, SePay đang trỏ nhầm sang FE.

### VNPAY

VNPAY dùng URL BE public:

```text
Return URL: https://be-example.ngrok-free.app/api/payment/vnpay-return
IPN URL:    https://be-example.ngrok-free.app/api/payment/vnpay-ipn
```

Xem hướng dẫn backend đầy đủ tại [vnpay-setup.md](vnpay-setup.md).

## 7. CORS và lưu ý bảo mật

- BE phải cho phép origin FE ngrok hiện tại trong cấu hình CORS trước khi FE gọi
  API qua browser.
- URL ngrok Free thay đổi thì cập nhật lại `.env.local`, CORS, SePay Webhook và
  VNPAY Return/IPN URL theo nhu cầu.
- Không commit `.env.local`, API key upload ảnh, token OAuth secret hay payment
  secret.
- `VITE_` variables được bundle vào browser; chỉ đặt thông tin công khai ở đó.
