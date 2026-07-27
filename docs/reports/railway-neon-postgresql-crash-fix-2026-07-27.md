# Báo cáo xử lý Railway crash do cấu hình Neon PostgreSQL

Ngày kiểm tra: 2026-07-27

Repository: `CinemaSystem_BE`

Nhánh làm việc: `Tom/ticketscan2-postgres-integration`

Railway service: `afedf757-8258-42a1-9b3e-753601ffe06c`

Railway environment: `b0302c79-4fe5-4a09-af23-a5a84aa8fe5a`

## Kết luận

Railway không crash do quá tải hay do tiến trình bị nền tảng tự dừng. Ứng dụng chủ động
thoát trong giai đoạn khởi động vì không mở được kết nối PostgreSQL để chạy database
maintenance/migration.

Hai lỗi đã quan sát được theo thứ tự:

1. Deployment `390a39c4...` thất bại với PostgreSQL `SqlState 28000` và thông báo kết nối
   không an toàn, yêu cầu `sslmode=require`.
2. Sau commit `4eae2a6b0178bdca032d35132de428cdd6f78bd6` ép `SslMode=Require`, deployment
   `c2c27e63-1739-46af-9c1c-9413244aeca9` vẫn thất bại với
   `Parameter 'database' is missing in startup packet`.

Nguyên nhân gốc là biến Railway `ConnectionStrings__DefaultConnection` trước đó chỉ có
thông tin kết nối không đầy đủ và không khớp với database Neon đích. BE không thể tự suy
ra `Database`, `Username` hoặc `Password`; Railway phải nhận nguyên connection string
đầy đủ do Neon cung cấp.

Trong phiên xử lý này, biến Railway đã được thay bằng connection string Npgsql đầy đủ
lấy trực tiếp từ Neon, giữ `SSL Mode=Require` và `Channel Binding=Require`, sau đó áp
dụng thay đổi để tạo deployment mới.

## Bản sửa BE

### Kiểm tra cấu hình trước khi tạo `DbContext`

`CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs` hiện:

- từ chối connection string trống;
- parse bằng `NpgsqlConnectionStringBuilder`;
- từ chối cấu hình sai định dạng;
- yêu cầu đủ `Host`, `Database`, `Username` và `Password` hoặc `Passfile`;
- không đưa host, password hoặc toàn bộ connection string vào lỗi;
- luôn ép `SslMode=Require`;
- bỏ `Trust Server Certificate=true` vì tùy chọn này đã obsolete/không còn tác dụng với
  Npgsql 8 khi dùng `SslMode=Require`.

Mục tiêu của thay đổi là fail fast với lỗi cấu hình rõ ràng, thay vì để PostgreSQL trả về
lỗi khó chẩn đoán sau khi container đã khởi động.

### Test hồi quy

Đã thêm `CinemaSystem.Tests/PostgresConnectionConfigurationTests.cs` với bốn trường hợp:

- thiếu toàn bộ connection string;
- chỉ có host/port;
- chuỗi sai định dạng và không làm lộ secret trong exception;
- chuỗi đầy đủ được giữ nguyên các trường cần thiết và luôn dùng SSL.

Hai file cấu hình mẫu cũng đã được cập nhật để bỏ tùy chọn Npgsql obsolete:

- `.env.example`
- `CinemaSystem/appsettings.Development.example.json`

## Cấu hình Railway đã áp dụng

Trong Railway service và environment nêu trên, toàn bộ giá trị
`ConnectionStrings__DefaultConnection` đã được thay bằng connection string Npgsql đầy
đủ lấy từ hộp thoại **Connect** của Neon. Credential chỉ được chuyển từ Neon sang ô
biến bảo mật của Railway; không được in ra terminal, báo cáo hoặc source code.

Mẫu cấu trúc, không chứa credential thật:

```text
Host=<NEON_HOST>;Port=5432;Database=neondb;Username=neondb_owner;Password=<NEON_PASSWORD>;SSL Mode=Require;Channel Binding=Require;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=20;Timeout=15;Command Timeout=30;Keepalive=30
```

Lưu ý:

- không dùng giá trị chỉ có `Host` và `Port`;
- không commit connection string thật vào Git;
- không ghi password vào báo cáo, log hoặc ảnh chụp;
- nên sao chép đúng connection string `.NET/Npgsql` từ Neon thay vì tự đoán host/user;
- giữ `SSL Mode=Require`; Neon cũng cung cấp `Channel Binding=Require`;
- nếu password Neon đã từng bị lộ ở nơi công khai, rotate password trước khi redeploy.

`DOTNET_SYSTEM_NET_DISABLEIPV6=1` không giải quyết hai lỗi PostgreSQL trên. Có thể bỏ biến
này sau khi deployment ổn định, trừ khi có bằng chứng riêng về sự cố IPv6.

## Kết quả xác minh cục bộ

| Kiểm tra | Kết quả |
|---|---|
| Test tập trung cấu hình PostgreSQL | PASS, 4/4 |
| Build Release với output riêng | PASS, 90 warning hiện hữu, 0 error |
| Toàn bộ test solution | PASS, 352 passed, 4 skipped, 0 failed |
| `git diff --check` | PASS |
| Real SMTP | Không chạy; vẫn là test opt-in qua `RUN_REAL_EMAIL_TESTS=true` |
| PostgreSQL integration cần credential riêng | Skipped theo cấu hình test cục bộ |

Build/test dùng thư mục artifacts riêng trong `C:\tmp` để không can thiệp tiến trình
Visual Studio/API mà người dùng đang chạy.

Các nullable warning hiện có xuất hiện khi `dotnet test` tự build lại solution. Chúng nằm
ở các module sẵn có và không phát sinh từ bản sửa connection string này.

## Xác minh deployment Railway

- Deployment: `1d63455f-26f6-43fa-8684-0d3cde7b8071`.
- Railway hiển thị `Deployment successful`, deployment ở trạng thái `Active` và service
  ở trạng thái `Online`.
- `GET /api/health`: HTTP 200, `status=OK`.
- `GET /api/db-test/movies-count`: HTTP 200 và truy vấn thành công bảng `MOVIE`; đây là
  bằng chứng process không chỉ sống mà còn kết nối/truy vấn được Neon.
- Deploy log mới không còn hai lỗi `connection is insecure` và
  `Parameter 'database' is missing in startup packet`.
- Sau hơn 4 phút (vượt ngưỡng crash cũ 1-2 phút), Railway vẫn hiển thị `Online` và lần
  gọi lại `/api/health` tiếp tục trả HTTP 200.

Vì deployment đã vượt ngưỡng crash cũ và cả liveness lẫn truy vấn DB đều thành công, sự
cố cấu hình connection string được xác nhận đã xử lý.

## Ranh giới an toàn dữ liệu

Ứng dụng hiện chạy database maintenance/migration trước khi bắt đầu lắng nghe HTTP. Vì
vậy phải xác nhận connection string đang trỏ đúng database dự kiến trước khi redeploy.
Không chạy `docs/database/cinema-booking-schema.sql` trên Neon vì đây là script reset có
tính phá hủy. Nếu cần thử migration với dữ liệu đang giữ lại, hãy dùng một Neon
branch/database staging riêng trước khi áp dụng lên database chính.

## Trạng thái bàn giao

- Bản sửa và test đang ở working tree cục bộ, chưa commit/push.
- Biến Railway đã được sửa và deployment mới đã được kích hoạt.
- Deployment mới đang `Active`; health và truy vấn DB production đều trả HTTP 200.
- Không có credential thật trong source, test hoặc báo cáo.
