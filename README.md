# CinemaSystem Backend

ASP.NET Core 8 Web API for an online movie ticket booking and cinema
management system.

## Features

- Customer registration and email OTP verification
- JWT login, refresh-token rotation, logout, and role authorization
- Forgot-password and password-reset flows
- Staff account provisioning by administrators
- Cinema, room, seat, and showtime management
- Temporary seat locking with in-memory or Redis storage
- SePay payment creation and webhook confirmation

## Architecture

| Project | Responsibility |
| --- | --- |
| `CinemaSystem` | API controllers, middleware, authentication, Swagger, and DI entry point |
| `CinemaSystem.Application` | Use-case interfaces, application constants, and service contracts |
| `CinemaSystem.Contracts` | Request and response DTOs |
| `CinemaSystem.Domain` | Domain entities and business concepts |
| `CinemaSystem.Infrastructure` | EF Core, SQL Server, JWT, SMTP, Redis, and payment implementations |
| `CinemaSystem.Tests` | Automated tests |

Project documents are indexed in [`docs/README.md`](docs/README.md).

## Requirements

- .NET 8 SDK
- SQL Server
- Redis is optional; the application can use the in-memory seat-lock store
- Gmail SMTP credentials are optional when mock email mode is enabled

## Local Setup

1. Clone the repository.
2. Create the local settings file:

   ```powershell
   Copy-Item CinemaSystem/appsettings.Development.example.json CinemaSystem/appsettings.Development.json
   ```

3. Add local credentials to `CinemaSystem/appsettings.Development.json`.
4. Create the database using
   [`docs/database/cinema-booking-schema.sql`](docs/database/cinema-booking-schema.sql).
5. Restore, build, and test:

   ```powershell
   dotnet restore CinemaSystem.sln
   dotnet build CinemaSystem.sln
   dotnet test CinemaSystem.sln
   ```

6. Run the API:

   ```powershell
   dotnet run --project CinemaSystem
   ```

Swagger is available at `/swagger` in the development environment.

## Configuration

Keep credentials in `appsettings.Development.json`, .NET User Secrets, or
environment variables. Never commit database passwords, JWT secrets, SMTP app
passwords, webhook secrets, tokens, or production account details.

Important configuration sections:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings`
- `EmailSettings`
- `SepaySettings`
- `Redis:ConnectionString`

Optional seed accounts are created only when their password variables are set:

- `ADMIN_PASSWORD` and optional `ADMIN_EMAIL`
- `DEV_STAFF_PASSWORD`
- `DEV_CUSTOMER_PASSWORD`

## Git Workflow

Create feature branches from `main` and merge through a pull request or merge
request:

```powershell
git switch main
git pull
git switch -c feature/short-description
```

Before opening a PR/MR, run:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.sln
# CinemaSystem Backend

ASP.NET Core 8 Web API for an online movie ticket booking and cinema
management system.

## Features

- Customer registration and email OTP verification
- JWT login, refresh-token rotation, logout, and role authorization
- Forgot-password and password-reset flows
- Staff account provisioning by administrators
- Cinema, room, seat, and showtime management
- Temporary seat locking with in-memory or Redis storage
- SePay payment creation and webhook confirmation

## Architecture

| Project | Responsibility |
| --- | --- |
| `CinemaSystem` | API controllers, middleware, authentication, Swagger, and DI entry point |
| `CinemaSystem.Application` | Use-case interfaces, application constants, and service contracts |
| `CinemaSystem.Contracts` | Request and response DTOs |
| `CinemaSystem.Domain` | Domain entities and business concepts |
| `CinemaSystem.Infrastructure` | EF Core, SQL Server, JWT, SMTP, Redis, and payment implementations |
| `CinemaSystem.Tests` | Automated tests |

Project documents are indexed in [`docs/README.md`](docs/README.md).

## Requirements

- .NET 8 SDK
- SQL Server
- Redis is optional; the application can use the in-memory seat-lock store
- Gmail SMTP credentials are optional when mock email mode is enabled

## Local Setup

1. Clone the repository.
2. Create the local settings file:

   ```powershell
   Copy-Item CinemaSystem/appsettings.Development.example.json CinemaSystem/appsettings.Development.json
   ```

3. Add local credentials to `CinemaSystem/appsettings.Development.json`.
4. Create the database using
   [`docs/database/cinema-booking-schema.sql`](docs/database/cinema-booking-schema.sql).
5. Restore, build, and test:

   ```powershell
   dotnet restore CinemaSystem.sln
   dotnet build CinemaSystem.sln
   dotnet test CinemaSystem.sln
   ```

6. Run the API:

   ```powershell
   dotnet run --project CinemaSystem
   ```

Swagger is available at `/swagger` in the development environment.

## Configuration

Keep credentials in `appsettings.Development.json`, .NET User Secrets, or
environment variables. Never commit database passwords, JWT secrets, SMTP app
passwords, webhook secrets, tokens, or production account details.

Important configuration sections:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings`
- `EmailSettings`
- `SepaySettings`
- `Redis:ConnectionString`

Optional seed accounts are created only when their password variables are set:

- `ADMIN_PASSWORD` and optional `ADMIN_EMAIL`
- `DEV_STAFF_PASSWORD`
- `DEV_CUSTOMER_PASSWORD`

## Git Workflow

Create feature branches from `main` and merge through a pull request or merge
request:

```powershell
git switch main
git pull
git switch -c feature/short-description
```

Before opening a PR/MR, run:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.sln
```


### 7. Nghiệp vụ và Quy tắc quản lý Ghế (Seat & Seat Map Business Rules)

#### 7.1 Phân loại Ghế (Seat Types)
Hệ thống hỗ trợ 3 loại ghế chính với cấu hình giá và sơ đồ khác nhau:
*   **Standard (Ghế thường):** Ghế tiêu chuẩn, áp dụng mức giá cơ bản của suất chiếu.
*   **VIP (Ghế VIP):** Áp dụng mức phụ thu hoặc nhân hệ số giá.
*   **Sweetbox (Ghế đôi/Couple):** Ghế đôi nằm ở hàng cuối, phụ thu giá cao hơn và bắt buộc chọn theo cặp.

#### 7.2 Trạng thái của Ghế trong Suất chiếu (Showtime Seat Status)
Trong mỗi suất chiếu (`Showtime`), một vị trí ghế (`ShowtimeSeat`) sẽ trải qua các trạng thái:
*   **AVAILABLE (Ghế trống):** Khách hàng có thể nhìn thấy, chọn và tiến hành đặt vé.
*   **LOCKED (Đang giữ ghế):** Trạng thái tạm khóa khi khách hàng chọn ghế để thanh toán. 
    *   *Thời gian giữ ghế tối đa:* 10 - 15 phút (cấu hình qua `BookingSettings`).
    *   *Quản lý khóa:* Lưu trữ tập trung tại Redis (`ConnectionString`) hoặc bộ nhớ đệm ứng dụng (`InMemorySeatLockStore`). Hết thời gian giữ ghế mà không thanh toán, ghế tự động mở khóa.
*   **BOOKED / PAID (Đã đặt / Đã bán):** Ghế đã được thanh toán thành công hoặc được liên kết với đơn đặt vé (`BookingSeat`). Vị trí này sẽ bị khóa vĩnh viễn đối với suất chiếu đó.
*   **MAINTENANCE (Bảo trì):** Ghế bị hỏng hoặc được Admin đóng lại để sửa chữa, không cho phép chọn đặt vé.

#### 7.3 Nghiệp vụ Cập nhật Khung ghế của Phòng chiếu (Seat Template)
*   **Quy mô ghế:** Mỗi phòng chiếu (`Room`) sẽ có số lượng ghế thực tế tương ứng với số lượng mà Admin tạo ra khi thiết lập phòng chiếu (thay vì áp đặt cố định 100 ghế). Điều này giúp hệ thống linh hoạt hỗ trợ mọi loại phòng chiếu lớn nhỏ với sơ đồ ma trận khác nhau.
*   **Lưu lịch sử thay đổi (Audit History):**
    *   Khi Admin cập nhật khung ghế mới (`UpdateRoomSeatsWithHistoryAsync`), hệ thống sẽ tự động serialize danh sách ghế cũ (chỉ lấy các trường định vị: `RowLabel`, `SeatNumber`, `SeatTypeId`) thành một chuỗi JSON lưu vào bảng lịch sử.
    *   Sử dụng **Database Transaction** để đảm bảo dữ liệu đồng bộ; nếu có lỗi trong quá trình lưu hoặc mapping, toàn bộ thay đổi sẽ bị Rollback.

### 3. Nghiệp vụ Đổi Giờ Chiếu & Token Xác Nhận (Time Change Approval)
*   **Lệch giờ $\ge$ 15 phút:** Khi Admin cập nhật giờ chiếu lệch quá 15 phút so với giờ cũ, vé của khách sẽ bị đưa vào trạng thái chờ `ProcessingUnstable`.
*   **Mã hóa Token không cần DB:** Backend tạo bảo mật `HMACSHA256` dựa trên `BookingId` để sinh ra link xác nhận mà không cần tạo thêm bảng trong Database.
*   **Quyền quyết định của User:** Hệ thống tự gửi mail song ngữ (Anh-Việt) kèm 2 lựa chọn (Links):
    1.  *Chấp nhận:* Gọi API `GET /api/bookings/{id}/confirm-time-change?accept=true` $\rightarrow$ Vé về lại `PAID`.
    2.  *Không chấp nhận & Hủy vé:* Truyền `accept=false` $\rightarrow$ Vé chuyển thành `PendingRefund` và sinh ra bản ghi `Refund`.
*   **Lệch giờ < 15 phút:** Chỉ gửi Email thông báo song ngữ cho khách hàng mà không làm gián đoạn trạng thái vé.

#### 7.4 Nghiệp vụ Xử lý khi Đổi phòng chiếu & Xung đột loại ghế (Room Change Rules)
*   Khi Admin thay đổi phòng chiếu của một suất chiếu đang mở bán:
    *   Hệ thống chạy thuật toán **tự động ánh xạ ghế (Auto-Mapping)** dựa trên mã ghế (`SeatCode`, ví dụ `A1` phòng cũ sang `A1` phòng mới).
    *   **Kiểm tra tương thích (Seat Mismatch Validation):** Nếu loại ghế mới ở phòng mới khác biệt so với phòng cũ (ví dụ: khách bị hạ cấp từ ghế VIP/Double sang ghế Standard), hệ thống sẽ:
        1.  Chuyển đơn đặt vé của khách hàng sang trạng thái chờ xử lý bất ổn định **(`ProcessingUnstable`)**.
        2.  Chuyển trạng thái suất chiếu về `ProcessingUnstable`.
        3.  Sinh mã token xác nhận (HMACSHA256 băm từ `BookingId` và `ConfirmationTokenSecret`).
        4.  Gửi email xin lỗi song ngữ (Anh - Việt) được viết tự động bởi **Gemini AI** đề xuất đổi suất chiếu (có giảm giá/nâng hạng) hoặc yêu cầu hoàn tiền.
*   **Giới hạn thời hạn Token xác nhận:** Token xác nhận đổi suất chiếu/ghế chỉ có hiệu lực đến trước giờ chiếu **2 tiếng**. Nếu quá thời hạn này mà khách chưa phản hồi, job chạy ngầm của hệ thống sẽ tự động hủy vé, mở khóa ghế và tạo yêu cầu hoàn tiền (`Refund`).

#### 7.5 Nghiệp vụ Khóa / Bảo trì Ghế (Seat Maintenance Isolation)
*   **Ngăn chặn lỗi nghiệp vụ (Seat Lockout Avoidance):** Admin **không được phép** đưa một ghế vào trạng thái bảo trì (`IsActive = false`) nếu ghế đó đang có khách đặt vé ở bất kỳ suất chiếu nào trong tương lai. Hệ thống sẽ báo lỗi `SEAT_HAS_FUTURE_BOOKINGS`.
*   **API Dịch chuyển ghế thủ công:** Để bảo trì ghế đó, Admin (chỉ có quyền Manager hoặc Admin, không bao gồm Staff) bắt buộc phải chuyển ghế của khách hàng sang vị trí tương đương khác trước bằng cách gọi endpoint `POST /api/admin/bookings/reassign-seat`.
    *   Hệ thống giải phóng ghế cũ về trạng thái `AVAILABLE`.
    *   Gán ghế trống mới sang trạng thái `BOOKED` cho khách.
    *   Tự động gửi email thông báo thay đổi ghế ngồi chuyên nghiệp cho khách hàng qua Hangfire.
    *   Sau khi chuyển ghế của khách xong, ghế cũ không còn lịch đặt tương lai, Admin có thể thực hiện tắt kích hoạt ghế để tiến hành bảo trì bình thường.

### 8. Nghiệp vụ và Quy tắc quản lý Phòng chiếu (Room Business Rules)

#### 8.1 Trạng thái Phòng chiếu (Room Status)
Hệ thống quản lý 3 trạng thái hoạt động chính của một phòng chiếu:
*   **ACTIVE (Hoạt động):** Cho phép lên lịch các suất chiếu mới và bán vé bình thường.
*   **INACTIVE (Ngưng hoạt động):** Phòng chiếu bị ẩn đi, không hiển thị cho người dùng và không cho lên lịch suất chiếu mới.
*   **MAINTENANCE (Bảo trì):** Phòng chiếu tạm đóng cửa để sửa chữa thiết bị.

#### 8.2 Nghiệp vụ Tạo Phòng chiếu (Create Room)
*   **Ràng buộc:** Phòng chiếu bắt buộc phải liên kết với một Rạp chiếu phim (`CinemaId`) đang tồn tại trong hệ thống.
*   **Trạng thái ban đầu:** Phải thuộc danh sách trạng thái hợp lệ (`ACTIVE`, `INACTIVE`, `MAINTENANCE`).

#### 8.3 Nghiệp vụ Cập nhật Phòng chiếu (Update Room)
*   **Quy tắc đặt tên:** Tên phòng chiếu sau khi chuẩn hóa (xóa khoảng trắng thừa) không được trùng lặp với bất kỳ phòng chiếu nào khác **trong cùng một rạp** (so sánh không phân biệt hoa thường).
*   **Ràng buộc Sức chứa (Capacity):**
    *   Sức chứa thiết lập phải lớn hơn `0` và không được vượt quá giới hạn hệ thống cấu hình (`MaxRoomCapacity`).
    *   Nếu phòng chiếu **đã được tạo sơ đồ ghế**, sức chứa mới thiết lập không được phép nhỏ hơn số lượng ghế thực tế đang có trong phòng chiếu đó.
*   **Cơ chế Đình chỉ Suất chiếu liên đới (Cascading Suspension):**
    *   Khi phòng chiếu bị cập nhật trạng thái từ hoạt động (`ACTIVE`) sang ngưng hoạt động (`INACTIVE`) hoặc bảo trì (`MAINTENANCE`).
    *   Hệ thống sẽ tự động quét tất cả các Suất chiếu (`Showtime`) của phòng chiếu này đang mở bán (`OPEN`) và tự động chuyển chúng sang trạng thái **`SUSPENDED` (Đình chỉ)** để tạm khóa bán vé, bảo vệ quyền lợi của khách hàng đã mua vé.

#### 8.4 Nghiệp vụ Xóa Phòng chiếu (Delete Room - Soft Delete)
*   Hệ thống áp dụng cơ chế **xóa mềm (Soft Delete)** đối với thực thể phòng chiếu.
*   Khi Admin yêu cầu xóa phòng:
    *   Trạng thái phòng chiếu được chuyển sang **`INACTIVE`**.
    *   Tất cả các Suất chiếu đang mở bán (`OPEN`) thuộc phòng chiếu này sẽ được tự động đổi sang trạng thái **`SUSPENDED`** (Đình chỉ). Nhờ đó, Admin có cơ hội di dời lịch chiếu sang phòng khác (Re-seat / Change Room) trước khi xử lý hủy vé/hoàn tiền.

#### 8.5 Nghiệp vụ Tự động Sinh Ghế Hàng loạt (Generate Seats)
*   **Điều kiện thực hiện:** Admin/Manager chỉ được phép sinh ghế tự động khi phòng chiếu **chưa có bất kỳ ghế nào** được tạo trước đó.
*   **Quy tắc ma trận:**
    *   Sinh ghế dựa trên cấu hình số hàng (`Rows`) và số cột (`Columns`) và loại ghế mặc định (`SeatTypeId`).
    *   Tổng số ghế tạo ra (`Rows * Columns`) không được vượt quá giới hạn sức chứa tối đa của hệ thống.
*   **Quy tắc đặt mã ghế tự động:**
    *   Nhãn hàng ghế tự động tăng theo bảng chữ cái từ `A`, `B`... `Z`, rồi đến `AA`, `AB`...
    *   Số thứ tự ghế trong hàng đánh số từ `1` đến hết số lượng cột.
    *   Mã ghế hiển thị (`SeatCode`) được tự động ghép từ nhãn hàng và số cột (ví dụ: `A1`, `A2`, `B1`...).
*   **Đồng bộ sức chứa:** Sau khi tạo thành công danh sách ghế, hệ thống sẽ tự động đồng bộ giá trị trường sức chứa (`Capacity`) của Phòng chiếu bằng đúng số lượng ghế thực tế vừa được sinh ra.

### 9. Nghiệp vụ và Quy tắc quản lý Suất chiếu (Showtime Business Rules)

#### 9.1 Trạng thái Suất chiếu (Showtime Status)
Mỗi suất chiếu (`Showtime`) có các trạng thái hoạt động chính:
*   **OPEN (Mở bán):** Trạng thái hoạt động bình thường, khách hàng có thể đặt và giữ ghế.
*   **SUSPENDED (Đình chỉ):** Tạm khóa do phòng chiếu gặp sự cố, bảo trì hoặc bị xóa mềm. Suất chiếu ở trạng thái này sẽ ẩn khỏi danh sách đặt vé công khai.
*   **CANCELLED (Bị hủy):** Suất chiếu đã bị Admin hủy bỏ (do sự cố của rạp). Toàn bộ ghế được nhả và vé của khách được xử lý hoàn tiền.
*   **ProcessingUnstable (Đang xử lý không ổn định):** Suất chiếu đang bị thay đổi thông tin cốt lõi (giờ chiếu lệch $\ge$ 15 phút hoặc đổi phòng gây xung đột loại ghế) và đang chờ phản hồi xác nhận từ những khách hàng đã mua vé.

#### 9.2 Nghiệp vụ Tạo Suất chiếu (Create Showtime)
*   **Kiểm tra tính khả dụng của Phim và Phòng chiếu:**
    *   Bộ phim (`Movie`) được chọn phải tồn tại và đang hoạt động (không ở trạng thái `Archived` hoặc `Inactive`).
    *   Phòng chiếu (`Room`) và Rạp chiếu phim (`Cinema`) chứa phòng đó phải ở trạng thái hoạt động (`ACTIVE`).
*   **Thời gian mở bán sớm:** Thời gian bắt đầu chiếu (`StartTime`) phải nằm trong tương lai và cách thời điểm hiện tại tối thiểu một khoảng thời gian khóa cấu hình (`PreShowtimeBlockingMinutes` - nhằm ngăn việc tạo suất chiếu sát giờ diễn).
*   **Tính toán Thời gian kết thúc (EndTime):**
    *   Thời gian kết thúc tự động tính theo công thức: 
        $$\text{EndTime} = \text{StartTime} + \text{Thời lượng phim (phút)} + \text{Thời gian dọn dẹp phòng chiếu (Cleaning Buffer)}$$
*   **Quy tắc Chống trùng giờ (Overlap Check):**
    *   Trong cùng một phòng chiếu, không được có bất kỳ hai suất chiếu nào bị chồng chéo thời gian lên nhau.
    *   Công thức kiểm tra chồng chéo: Suất chiếu mới đụng độ suất cũ nếu:
        $$\text{StartTime}_{\text{mới}} < \text{EndTime}_{\text{cũ}} \quad \text{và} \quad \text{EndTime}_{\text{mới}} > \text{StartTime}_{\text{cũ}}$$
*   **Tự động nhân bản ghế (Showtime Seat Cloning):**
    *   Sau khi lưu suất chiếu mới thành công, hệ thống tự động tìm toàn bộ các ghế đang hoạt động của phòng chiếu đó (`Room.Seats.Where(s => s.IsActive)`) và nhân bản thành danh sách ghế tương ứng cho suất chiếu (`ShowtimeSeat`) với trạng thái ban đầu là `AVAILABLE` (Trống).

#### 9.3 Nghiệp vụ Cập nhật Suất chiếu (Update Showtime)
*   **Khi chưa có vé nào bán ra:** Admin được phép thay đổi mọi thông tin cốt lõi (giờ chiếu, phòng chiếu, giá vé) mà không gặp ràng buộc.
*   **Khi đã có vé bán ra (Đã thanh toán):**
    *   *Trường hợp thay đổi nhỏ (Lệch giờ dưới 15 phút):* Hệ thống chỉ gửi email thông báo nhẹ nhàng cho khách đã mua vé. Trạng thái suất chiếu và vé vẫn giữ nguyên là hoạt động bình thường.
    *   *Trường hợp thay đổi lớn (Lệch giờ từ 15 phút trở lên HOẶC thay đổi phòng chiếu gây xung đột/hạ cấp loại ghế):*
        1.  Chuyển trạng thái của suất chiếu và toàn bộ đơn đặt vé của khách hàng đã mua về trạng thái **`ProcessingUnstable`**.
        2.  Sinh mã token xác nhận an toàn (bằng thuật toán `HMACSHA256` dựa trên `BookingId`).
        3.  Gọi dịch vụ **AI Gemini** tự động viết email xin lỗi song ngữ (Anh - Việt) chuyên nghiệp, cung cấp link xác nhận đính kèm 2 sự lựa chọn: **Đồng ý đổi** (nâng hạng ghế/tặng voucher) hoặc **Hủy vé và nhận hoàn tiền**.
        4.  *Thời hạn xác nhận:* Token xác nhận chỉ có giá trị đến **trước giờ chiếu 2 tiếng**. Nếu quá thời hạn này khách chưa chọn, hệ thống tự động hủy vé, hoàn tiền và nhả ghế về trống.

#### 9.4 Nghiệp vụ Hủy suất chiếu (Delete / Cancel Showtime)
*   **Nếu suất chiếu chưa bán vé:** Thực hiện xóa vĩnh viễn (Hard Delete) suất chiếu và toàn bộ bản ghi ghế của suất chiếu đó khỏi cơ sở dữ liệu.
*   **Nếu suất chiếu đã bán vé:**
    *   Admin chuyển trạng thái suất chiếu về **`CANCELLED`** (nếu không vi phạm quy tắc khóa hủy trong vòng 30 phút trước giờ diễn).
    *   Nhả toàn bộ ghế của suất chiếu về trống (`AVAILABLE`).
    *   Chuyển toàn bộ vé của khách hàng sang trạng thái hủy và tiến hành hoàn tiền tự động (`PendingRefund`), đồng thời sinh ra các bản ghi hoàn tiền (`Refund`).
    *   Hệ thống gọi dịch vụ **AI Gemini** gửi thư xin lỗi song ngữ (Anh - Việt) thông báo suất chiếu bị hủy do lỗi kỹ thuật và đề xuất đền bù (chuyển suất/nâng hạng hoặc hoàn tiền).
    *   *Ràng buộc EF Core:* Đảm bảo quan hệ 1-1 của biên bản hủy suất chiếu (`ShowtimeCancellation`) để tránh lỗi khóa ngoại khi Admin nhấn hủy nhiều lần.

### 10. Nghiệp vụ và Quy tắc quản lý Phim (Movie Business Rules)

#### 10.1 Trạng thái Bộ phim (Movie Status)
Hệ thống quản lý 4 trạng thái hoạt động chính của một bộ phim:
*   **NowShowing (Đang chiếu):** Phim đang được mở bán vé. Trạng thái mặc định khi phim được phát hành.
*   **ComingSoon (Sắp chiếu):** Phim chuẩn bị ra rạp. Trạng thái mặc định nếu ngày phát hành (`ReleaseDate`) nằm trong tương lai.
*   **Inactive (Không hoạt động / Xóa mềm):** Phim đã ngừng chiếu hoặc bị xóa mềm khỏi hệ thống.
*   **Archived (Lưu trữ):** Phim cũ được lưu trữ lại.

#### 10.2 Quy định hiển thị công khai (Public Visibility)
*   Đối với khách hàng thông thường, hệ thống tự động lọc bỏ (ẩn đi) các bộ phim có trạng thái **`Inactive`** và các phim có nhãn giới hạn độ tuổi cấm chiếu **`C`** (chỉ hiển thị cho tài khoản quản trị Admin/Manager kiểm tra).

#### 10.3 Nghiệp vụ Tạo Phim (Create Movie)
*   **Ngôn ngữ bản xứ:** Mã ngôn ngữ (`LanguageId`) bắt buộc phải tồn tại trong CSDL (bảng `Languages`).
*   **Tự động tạo Thể loại:** Nếu Admin nhập một thể loại phim chưa có sẵn, hệ thống sẽ tự động thêm thể loại đó vào bảng danh mục thể loại trước khi thiết lập liên kết.
*   **Quản lý Poster an toàn:** Poster được tải lên thông qua luồng stream và lưu trữ tập trung qua `IFileStorageService`. Nếu việc ghi nhận phim vào CSDL thất bại, hệ thống tự động xóa file poster đã lưu trước đó để tránh dư thừa tài nguyên.

#### 10.4 Nghiệp vụ Cập nhật Phim (Update Movie)
*   **Ràng buộc thời lượng phim (Duration):** Admin chỉ bị chặn sửa thời lượng của phim khi phim đã có suất chiếu chứa đơn đặt vé hoạt động (không bị hủy). Trường hợp phim đã có lịch chiếu (showtime) nhưng chưa có khách nào đặt vé (booking), Admin vẫn được phép chỉnh sửa thời lượng bình thường. Nếu vi phạm, hệ thống chặn thay đổi và trả về mã lỗi `DURATION_CANNOT_BE_CHANGED_HAS_SHOWTIMES`.
*   **Quản lý Poster thay thế:** Khi Admin cập nhật ảnh poster mới, hệ thống tự động phát hiện và xóa tệp poster cũ trên ổ đĩa lưu trữ trước khi ghi đè tệp mới.

#### 10.5 Nghiệp vụ Xóa Phim (Delete Movie - Soft Delete)
*   Xóa phim thực chất là thực hiện xóa mềm (Soft Delete) chuyển trạng thái sang **`Inactive`**.
*   **Đình chỉ & Hoàn tiền Suất chiếu liên đới (Cascading Refund):**
    *   Khi phim bị xóa, hệ thống quét tất cả các suất chiếu đang mở bán (`OPEN`) của phim đó.
    *   Tự động gọi dịch vụ hoàn tiền `IAdminRefundService.CancelShowtimesAndRefundAsync` để **hủy toàn bộ suất chiếu** đó, giải phóng tất cả ghế ngồi, tự động hoàn trả tiền đầy đủ cho mọi khách hàng đã mua vé và xếp lịch gửi email xin lỗi tự động từ **Gemini AI**.

#### 10.6 Nghiệp vụ Đồng bộ Điểm đánh giá (Rating Update)
*   Khi có khách hàng viết đánh giá, chỉnh sửa đánh giá hoặc xóa đánh giá (`Review`), hệ thống tự động tính toán lại và đồng bộ các chỉ số của Phim:
    *   Tính lại tổng số lượt đánh giá (`TotalReviews`).
    *   Tính lại điểm số đánh giá trung bình (`AverageRating` - được làm tròn tới 2 chữ số thập phân).

### 11. Nghiệp vụ và Quy tắc quản lý Email & OTP (Email & OTP System Business Rules)

#### 11.1 Cơ chế gửi Mail SMTP (Gmail)
Hệ thống tích hợp cổng gửi mail SMTP (như Gmail hoặc các SMTP Server chuyên dụng) thông qua các lớp adapter:
*   **IEmailService & IEmailSender:** Định nghĩa các phương thức gửi email thông thường hoặc gửi thư mời (`SendInvitationAsync`) cho Staff mới lập mật khẩu.
*   **Cấu hình SMTP:**
    *   Sử dụng cổng kết nối bảo mật **SSL/TLS (`EnableSsl = true`)**.
    *   Các tham số cấu hình linh hoạt qua `EmailSettings` (Host, Port, SenderEmail, SenderName, Password/AppPassword).
    *   **Tự động nhận diện định dạng HTML:** Nếu nội dung thư chứa các nhãn `<html>` hoặc `<body>`, hệ thống tự động thiết lập gửi dưới định dạng Rich Text/HTML để email hiển thị chuyên nghiệp.
*   **Mock Mode (Chế độ phát triển):** Khi cấu hình chạy thử nghiệm local (`EmailSettings:UseMock = true`), hệ thống chuyển sang sử dụng `MockEmailService`. Các thư gửi đi sẽ được ghi nhận trực tiếp vào Logger thay vì gửi SMTP thực tế để tiết kiệm chi phí và hỗ trợ kiểm thử dễ dàng.

#### 11.2 Nghiệp vụ Chống Spam OTP & Tràn tài nguyên Email (Rate Limiting & Cooldown)
Để ngăn chặn kẻ tấn công lợi dụng các API gửi OTP liên tục làm cạn kiệt tài nguyên (Spam OTP) hoặc gây block/hỏng tên miền gửi thư của rạp, hệ thống áp dụng các quy tắc bảo vệ:
*   **Thời gian chờ (Resend Cooldown):** Áp dụng thời gian nghỉ giữa các lần yêu cầu gửi lại mã OTP là **60 giây** (`OtpResendCooldownSeconds = 60`). Nếu gửi yêu cầu liên tục trong 60 giây, hệ thống từ chối và trả về lỗi `429 Too Many Requests` kèm mã lỗi `OTP_RESEND_COOLDOWN`.
*   **Giới hạn số lần gửi tối đa (Send Limits):** Mỗi phiên giao dịch xác thực/đăng ký chỉ cho phép yêu cầu gửi mã tối đa **5 lần** (`OtpMaxSendAttempts = 5`).
*   **Khóa phạt (Lock Penalty):** Nếu khách hàng yêu cầu gửi OTP quá 5 lần quy định, hệ thống sẽ tự động khóa chức năng gửi OTP của tài khoản/đối tượng đó trong vòng **2 tiếng** (`OtpSendLockHours = 2`), trả về mã lỗi `429` kèm mã lỗi `OTP_SEND_LIMIT_REACHED`.
*   **Độ tin cậy của OTP:**
    *   Thời gian sống của OTP ngắn (mặc định 10 phút).
    *   Tất cả các OTP đều được lưu trữ dưới dạng băm bảo mật trong CSDL (`EmailVerificationTokens`).
    *   Tuyệt đối không bao giờ trả về giá trị OTP trong API Response.

#### 11.3 Nghiệp vụ Xác thực kép khi Thay đổi Email (Double Verification)
Để ngăn ngừa lỗ hổng chiếm đoạt tài khoản (Email Takeover) khi tài khoản bị lộ session đăng nhập và kẻ gian cố tình thay đổi địa chỉ email liên kết:
*   Hệ thống **không chỉ gửi OTP về email mới** mà bắt buộc phải thực hiện xác thực kép (Double Verification).
*   Mã OTP sẽ được gửi đồng thời về cả **Email cũ** (mục đích xác nhận chủ sở hữu hiện tại đồng ý chuyển) và **Email mới** (xác nhận hòm thư mới hoạt động chính xác).
*   Khách hàng phải nhập đúng cả 2 mã OTP (`OldEmailOtp` và `NewEmailOtp`) thì yêu cầu thay đổi email mới được xử lý thành công.

#### 11.4 Nghiệp vụ Email xin lỗi tự động song ngữ bằng AI (AI Apology Mail)
Khi xảy ra sự cố kỹ thuật từ phía rạp chiếu phim (hủy suất chiếu, đổi phòng chiếu dẫn đến hạ cấp loại ghế):
*   Hệ thống tích hợp **Gemini AI** (`gemini-3.1-flash-lite`) thông qua `IAiEmailService` để tự động soạn thảo email.
*   **Ngôn ngữ:** Nội dung email xin lỗi được AI soạn song ngữ **Anh - Việt** cực kỳ chuyên nghiệp và lịch thiệp, dựa trên ngữ cảnh sự cố cụ thể.
*   **Đề xuất đền bù:** Thư gửi đi tự động đính kèm các lựa chọn xử lý cho khách hàng tự chọn (nâng hạng ghế miễn phí / đổi suất chiếu kèm mã giảm giá voucher, hoặc hoàn trả tiền vé 100% tự động).
*   **Chạy ngầm (Background Job):** Toàn bộ tiến trình gọi AI viết thư và gửi mail đều được xếp hàng qua **Hangfire** chạy ngầm, không gây block hay tăng thời gian phản hồi (latency) của API.

#### 11.5 Các trường hợp hệ thống tự động gửi Email
Hệ thống Cinema Booking tự động kích hoạt gửi email cho khách hàng hoặc nhân viên trong các tình huống nghiệp vụ cụ thể sau:
*   **Xác thực đăng ký tài khoản mới (Register Verification OTP):** Khi khách hàng đăng ký tài khoản thành công, hệ thống gửi email chứa mã OTP để kích hoạt tài khoản trước khi đăng nhập.
*   **Yêu cầu gửi lại OTP (Resend Verification OTP):** Khách hàng yêu cầu gửi lại mã kích hoạt khi OTP cũ bị hết hạn hoặc không nhận được.
*   **Yêu cầu Quên mật khẩu (Forgot Password OTP):** Khách hàng yêu cầu đặt lại mật khẩu, hệ thống gửi mã OTP xác nhận thay đổi mật khẩu an toàn.
*   **Yêu cầu Thay đổi Email cá nhân (Email Update Double Verification):** Khi khách hàng muốn đổi email liên kết của tài khoản, hệ thống gửi đồng thời **2 email chứa OTP** về cả **Email cũ** và **Email mới** để tiến hành xác thực kép.
*   **Mời nhân viên mới (Staff Invitation):** Khi Admin tạo tài khoản nhân viên (`Staff`) mới trên hệ thống, một email mời sẽ tự động gửi tới hòm thư của nhân viên đó kèm theo mã token mời (`invitationToken`) để kích hoạt tài khoản và tự đặt mật khẩu lần đầu.
*   **Lệch giờ chiếu nhẹ (Dưới 15 phút):** Gửi email thông báo cập nhật giờ chiếu mới cho tất cả các khách hàng đã đặt vé của suất chiếu đó để họ chủ động thời gian đến rạp.
*   **Đổi phòng chiếu nhưng loại ghế tương thích:** Khi suất chiếu chuyển sang phòng mới nhưng loại ghế của khách không bị ảnh hưởng (ví dụ: Standard sang Standard), hệ thống gửi email thông báo đổi phòng chiếu tự động.
*   **Đổi phòng chiếu gây xung đột/hạ cấp loại ghế (Seat Type Mismatch):** Khi phòng chiếu mới không có loại ghế tương đương (ví dụ khách bị hạ cấp từ VIP/Double xuống Standard), hệ thống gửi **thư xin lỗi song ngữ Anh - Việt tự động do Gemini AI soạn thảo** đính kèm liên kết xác nhận (Token hạn dùng đến trước giờ chiếu 2 tiếng) để khách hàng chọn lựa (chấp nhận đền bù nâng hạng/voucher hoặc yêu cầu hoàn tiền).
*   **Hủy suất chiếu hàng loạt:** Khi Admin quyết định hủy một suất chiếu (do phòng chiếu hỏng thiết bị, cúp điện...), hệ thống gửi **thư xin lỗi song ngữ Anh - Việt tự động do Gemini AI soạn thảo** thông báo suất chiếu bị hủy, xác nhận vé của họ đã được tự động hoàn tiền 100% và tặng voucher đền bù cho lần xem phim kế tiếp.
*   **Hủy vé tự động do quá hạn xác nhận (Timeout Cancellation):** Khi khách hàng có vé bị ảnh hưởng đổi suất chiếu (`ProcessingUnstable`) nhưng không thực hiện xác nhận trước giờ chiếu 2 tiếng, hệ thống tự động hủy vé, hoàn tiền và gửi email thông báo đơn đặt vé đã bị hủy tự động.
*   **Di dời ghế ngồi thủ công (Manual Seat Reassignment):** Khi Admin thực hiện dịch chuyển ghế của khách hàng sang vị trí trống khác (để đóng ghế cũ bảo trì sửa chữa), hệ thống sẽ gửi email thông báo chi tiết vị trí ghế ngồi mới cho khách hàng để tránh bỡ ngỡ khi vào phòng chiếu.

### 12. Nghiệp vụ Voucher, Bán vé tại quầy & Báo cáo ca làm việc (Vouchers, Counter Booking & Staff Shift Report)

#### 12.1 Nghiệp vụ Ví Voucher & Xác thực Voucher (Voucher Wallet & Validation Rules)
Hệ thống quản lý voucher được thiết kế chặt chẽ và tập trung logic để chống gian lận và tối ưu hóa trải nghiệm khách hàng:
*   **Ví Voucher (Customer Voucher Wallet):** Khách hàng có thể lưu/thu thập các voucher vào ví cá nhân (`CUSTOMER_VOUCHER`). Khi đặt vé, nếu voucher trong ví được sử dụng, hệ thống tự động đánh dấu đã dùng (`IsUsed = true`) và ghi lại thời gian dùng.
*   **Xác thực tập trung (Unified Validation):** Mọi thao tác kiểm tra voucher (qua API validation, đặt vé online, hoặc đặt vé tại quầy) đều đi qua phương thức tập trung `ValidateAndGetVoucherAsync` để đảm bảo tính nhất quán (DRY):
    *   **Trạng thái & Thời gian:** Voucher phải ở trạng thái `ACTIVE` và nằm trong khoảng thời gian hiệu lực (`StartDate <= UtcNow <= EndDate`).
    *   **Giá trị đơn hàng tối thiểu:** Kiểm tra số tiền đơn hàng phải lớn hơn hoặc bằng mức tối thiểu (`MinOrderAmount`).
    *   **Giới hạn số lượng (Usage Limit):** Tổng số lượt áp dụng thực tế (trạng thái khác `Cancelled`) không vượt quá giới hạn tổng của voucher (`UsageLimit`).
    *   **Giới hạn mỗi khách hàng (Per-Customer Limit):** Số lần khách hàng đã sử dụng voucher (trạng thái khác `Cancelled` trong `VoucherUsages`) không được vượt quá hạn mức cá nhân (`PerCustomerLimit`).
*   **Tính toán chiết khấu (Discount Calculation):** Hỗ trợ giảm giá theo số tiền cố định (`AMOUNT`) hoặc phần trăm (`PERCENT`) kèm theo hạn mức trần tối đa (`MaxDiscountAmount`). Nếu số tiền thanh toán sau giảm giá dưới 1,000 VND, hệ thống tự động làm tròn về 0 VND (miễn phí hoàn toàn).
*   **Thu hồi quota khi hết hạn/hủy đơn:** Nếu đặt vé bị hết hạn thanh toán (timeout) hoặc bị hủy ở trạng thái không ổn định (`ProcessingUnstable`), hệ thống tự động hủy lượt dùng voucher, hoàn trả lượt dùng (`UsedCount`) và hoàn voucher lại ví cá nhân (`IsUsed = false`).

#### 12.2 Nghiệp vụ Bán vé tại quầy (Staff Counter Booking)
Cho phép nhân viên (`Staff`, `Manager`, `Admin`) bán vé và đồ ăn nước uống (F&B) trực tiếp cho khách tại quầy bán vé:
*   **Giới hạn khu vực (Cinema Scoping):** Nhân viên rạp chỉ được phép bán vé cho các suất chiếu thuộc rạp mình đang được phân công làm việc (`currentStaffCinemaId` khớp với `Room.CinemaId`).
*   **Trừ kho F&B tự động và nguyên tử (Atomic Inventory Deduction):** Đồ ăn nước uống được bán trực tiếp tại quầy sẽ tự động thực hiện trừ số lượng trong kho của rạp (`CINEMA_FB_INVENTORY`) thông qua câu lệnh SQL Update nguyên tử nhằm chống tranh chấp tài nguyên kho (Concurrency/Race Condition).
*   **Tự động bàn giao:** F&B được mua tại quầy mặc định có trạng thái hoàn tất bàn giao (`FbFulfillmentStatus = Fulfilled`) ngay lập tức mà không cần qua quy trình quét mã nhận đồ ăn.

#### 12.3 Nghiệp vụ Quét mã & Bàn giao đồ ăn nước uống (F&B Order Fulfillment)
Hỗ trợ quy trình nhận đồ ăn nước uống đã đặt trước trực tuyến thông qua mã QR:
*   **Kiểm tra chéo chi nhánh (Cross-Branch Scan Protection):** Hệ thống chặn nhân viên rạp này quét mã bàn giao đồ ăn của đơn hàng được đặt ở rạp khác.
*   **Chống gian lận nhận nhiều lần (Anti-Fraud Control):** Kiểm tra trạng thái đơn hàng. Nếu đơn F&B đã được bàn giao (`Fulfilled`), hệ thống sẽ chặn và đưa ra cảnh báo thời gian đã nhận đồ ăn trước đó để chống gian lận.
*   **Bảo vệ dữ liệu phân quyền:** Trong trường hợp thông tin rạp của nhân viên bị thiếu trong token (claims), hệ thống sẽ truy vấn ngược CSDL để lấy thông tin rạp từ `StaffProfile` hoạt động, đảm bảo an toàn tuyệt đối.

#### 12.4 Nghiệp vụ Báo cáo ca làm việc của Nhân viên (Staff Shift Report)
Cung cấp báo cáo minh bạch cho nhân viên và quản lý rạp về doanh thu và hoạt động vận hành trong ca:
*   **Phân quyền truy cập (Role-Based Scoping):**
    *   **Staff:** Chỉ được phép xem báo cáo ca làm việc của chính mình tại rạp được chỉ định.
    *   **Manager:** Xem được báo cáo của bất kỳ nhân viên nào hoặc toàn bộ nhân viên thuộc rạp mình quản lý.
    *   **Admin:** Có toàn quyền xem báo cáo của mọi nhân viên ở tất cả các rạp trong hệ thống.
*   **Thống kê chi tiết hoạt động:** Báo cáo tổng hợp số lượng vé đã quét kiểm tra (`CheckedInTicketCount`), số lượng đơn F&B tại quầy và trực tuyến đã xử lý, tổng doanh thu bán đồ ăn tại quầy, chi tiết doanh thu theo phương thức thanh toán (Tiền mặt - Cash, Chuyển khoản - Transfer), và bảng nhật ký chi tiết tất cả giao dịch được sắp xếp theo thời gian thực.

#### 12.5 Nghiệp vụ Cập nhật Sơ đồ Ghế kèm Lưu Lịch sử (Room Seats Template Update with History)
Cho phép quản trị viên thay đổi thiết kế sơ đồ ghế ngồi của phòng chiếu:
*   **Lưu vết lịch sử thay đổi (Audit Log History):** Trước khi cập nhật sơ đồ ghế mới, hệ thống tự động tuần tự hóa (serialize) sơ đồ ghế cũ và sơ đồ ghế mới dưới dạng JSON để lưu vào bảng nhật ký thay đổi (`AuditLog`), giúp dễ dàng theo dõi vết hoạt động nâng cấp phòng chiếu.
*   **Bảo toàn dữ liệu Suất chiếu:** Hệ thống kiểm tra xem phòng chiếu đó đã có suất chiếu nào được lên lịch (`ShowtimeSeat`) hay chưa. Nếu đã có suất chiếu được tạo, hệ thống chặn hành động cập nhật và yêu cầu xử lý suất chiếu trước nhằm tránh crash API do ràng buộc khóa ngoại (Foreign Key Violation) hoặc phá hỏng dữ liệu đặt vé lịch sử.
