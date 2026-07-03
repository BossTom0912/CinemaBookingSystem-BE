# Báo cáo tích hợp `main` và `MangerAndAdmin_1`

## 1. Kết quả tổng quát

Ngày thực hiện: 2026-06-28

Nhánh test: `Tom/integration-main-manager-admin-test`

Remote sử dụng: `gitlab`

Kết quả:

- Đã push `main` lên `gitlab/main`.
- Đã push `MangerAndAdmin_1` lên `gitlab/MangerAndAdmin_1`.
- Đã tạo nhánh test mới từ `main`.
- Đã merge toàn bộ `MangerAndAdmin_1` vào nhánh test.
- Có 9 file conflict và đã resolve hết.
- Logic conflict được chọn theo `MangerAndAdmin_1`, là nhánh đã chứa logic
  Admin và database mới.
- Build thành công, không có error.
- Toàn bộ 225 test trong test assembly tích hợp đều pass.
- Schema local `CinemaBookingDB` có đầy đủ các bảng/cột mới được kiểm tra.

Merge commit:

```text
dcc5e95c5d3fb22303ea182b8b2ac96ac3c22784
```

Hai parent:

```text
main:              b8bfd9f77bdfcce23f0fbb5d5297274922b39167
MangerAndAdmin_1:  a9834a54c9ff581859c940c1774bd90f3f541629
```

Phạm vi thay đổi so với `main`:

```text
179 files changed
16,174 insertions
1,638 deletions
```

## 2. Các lệnh Git chính đã thực hiện

Kiểm tra và cập nhật remote:

```text
git fetch --all --prune
git rev-list --left-right --count main...gitlab/main
git ls-remote --heads gitlab main MangerAndAdmin_1
```

Push bằng refspec rõ ràng:

```text
git push gitlab main:main
git push gitlab MangerAndAdmin_1:MangerAndAdmin_1
```

Kết quả push:

```text
gitlab/main:
  a9fa818 -> b8bfd9f

gitlab/MangerAndAdmin_1:
  e616642 -> a9834a5
```

Tạo nhánh test:

```text
git switch -c Tom/integration-main-manager-admin-test main
```

Merge có kiểm soát:

```text
git merge --no-ff --no-commit MangerAndAdmin_1
```

Merge được để ở trạng thái chưa commit cho đến khi conflict đã resolve và build,
test đều pass.

## 3. Danh sách conflict và cách resolve

### 3.1 `CinemaSystem.Infrastructure/Auth/AdminService.cs`

Loại conflict: content conflict.

Khác biệt:

- `main` gọi `IEmailService.SendInvitationAsync` trực tiếp.
- `MangerAndAdmin_1` enqueue email bằng Hangfire
  `IBackgroundJobClient.Enqueue<IEmailService>`.

Quyết định:

- Chọn logic `MangerAndAdmin_1`.

Lý do:

- Nhánh Admin đã chuyển email invitation thành background job.
- Giữ direct call của `main` sẽ làm sai kiến trúc xử lý bất đồng bộ mới.
- Runtime dependency Hangfire đã được nhánh Admin bổ sung.

### 3.2 `CinemaSystem.Infrastructure/Bookings/CheckoutService.cs`

Loại conflict: modify/delete.

Khác biệt:

- `main` bổ sung comment vào `CheckoutService`.
- `MangerAndAdmin_1` xóa `CheckoutService`.

Quyết định:

- Chọn delete theo `MangerAndAdmin_1`.

Lý do:

- `ICheckoutService` đã bị xóa.
- `CheckoutRequest`, `CheckoutResponse`, `CheckoutFoodItemRequest` đã bị xóa.
- Route checkout cũ đã được thay đổi trong `BookingsController`.
- Hai file test checkout được chuyển thành `.cs.txt` và không còn compile.
- Giữ riêng service sẽ tạo dependency graph nửa cũ, nửa mới và gây build lỗi.

### 3.3 `CinemaSystem.Infrastructure/Extensions/DependencyInjection.cs`

Loại conflict: content conflict.

Khác biệt:

- `main` còn đăng ký `ICheckoutService`.
- `MangerAndAdmin_1` thêm cinema scope, refund, review, moderation, file storage,
  event publisher và highlight background job.

Quyết định:

- Chọn registration của `MangerAndAdmin_1`.

Các runtime mapping mới quan trọng:

- `ICinemaScopeAuthorizationService -> CinemaScopeAuthorizationService`
- `IShowtimeCancellationService -> ShowtimeCancellationService`
- `IRefundService -> RefundService`
- `IRefundClaimService -> RefundClaimService`
- `IManualRefundService -> ManualRefundService`
- `IRefundProcessor -> RefundProcessor`
- `IReviewService -> ReviewService`
- `IAiModerationService -> GeminiModerationService`
- `IAdminRefundService -> AdminRefundService`
- `IFileStorageService -> LocalFileStorageService`
- `IPaymentRefundGateway -> UnsupportedPaymentRefundGateway`
- `IEventPublisher -> NoOpEventPublisher`
- `MovieHighlightClassificationJob`

### 3.4 `CinemaSystem.Infrastructure/Services/GeminiChatbotService.cs`

Loại conflict: content conflict.

Khác biệt:

- `main` gọi signature cũ của `IMovieService.GetMoviesAsync`.
- `MangerAndAdmin_1` dùng signature mới có paging/filter.

Quyết định:

- Chọn lời gọi mới:

```text
GetMoviesAsync(null, 1, 100, null, false, cancellationToken)
```

Lý do:

- `IMovieService` và `MovieService` đã đổi contract.
- Giữ signature cũ sẽ không compile.

### 3.5 `CinemaSystem/Controllers/BookingsController.cs`

Loại conflict: content conflict.

Khác biệt:

- `main` còn route `POST /api/bookings/checkout`.
- `MangerAndAdmin_1` thay phần này bằng xác nhận thay đổi giờ chiếu:
  `GET /api/bookings/{bookingId}/confirm-time-change`.

Quyết định:

- Chọn controller của `MangerAndAdmin_1`.

Lý do:

- Đồng bộ với `IBookingService.ConfirmTimeChangeAsync`.
- Đồng bộ với việc xóa checkout interface/DTO/service.
- Hỗ trợ luồng thay đổi phòng/giờ chiếu và xác nhận từ khách hàng.

### 3.6 `CinemaSystem/Controllers/MoviesController.cs`

Loại conflict: content conflict.

Quyết định:

- Chọn logic Admin.

Chức năng được giữ:

- paging;
- filter genre/status;
- Admin/Manager có thể xem dữ liệu đã soft delete;
- ghi nhận lượt xem movie;
- create movie kèm poster;
- update movie kèm poster;
- delete movie.

Lý do:

- Contract `IMovieService` của nhánh Admin đã mở rộng.
- Đây là phần Movie CRUD và statistics của Admin use case.

### 3.7 `CinemaSystem/Controllers/RoomsController.cs`

Loại conflict: content conflict.

Quyết định:

- Chọn logic `MangerAndAdmin_1`.

Logic mới được giữ:

- gọi `ICinemaScopeAuthorizationService`;
- Manager/Staff chỉ truy cập room thuộc cinema được phân công;
- Admin có system scope;
- hỗ trợ `includeInactive`;
- update/delete truyền actor user id để audit/event processing.

Lý do:

- Phù hợp yêu cầu SRS: Manager không được quản lý toàn bộ hệ thống.

### 3.8 `CinemaSystem/Controllers/SeatsController.cs`

Loại conflict: content conflict.

Quyết định:

- Chọn logic `MangerAndAdmin_1`.

Logic mới được giữ:

- authorize theo room trước khi tạo/list seat;
- authorize theo seat trước khi update/delete;
- bổ sung filter/list/detail seat cho Manager/Admin;
- giữ lock/unlock/seat map cho booking flow.

### 3.9 `CinemaSystem/Controllers/ShowtimesController.cs`

Loại conflict: content conflict.

Quyết định:

- Chọn logic `MangerAndAdmin_1`.

Logic mới được giữ:

- authorize cinema scope theo room/showtime;
- update có cờ `force`;
- kiểm tra cả current showtime scope và target room scope;
- bổ sung route đổi phòng:
  `POST /api/showtimes/{showtimeId}/change-room`.

## 4. Chức năng được tích hợp vào nhánh test

### 4.1 Manager cinema scope

Thành phần chính:

- `ICinemaScopeAuthorizationService`
- `CinemaScopeAuthorizationService`
- room/seat/showtime controllers có scope check
- integration test `ManagerCinemaScopeApiIntegrationTests`

Mục tiêu:

- Admin có quyền toàn hệ thống.
- Manager/Staff chỉ thao tác trong cinema thuộc `STAFF_PROFILE`.
- Chặn truy cập chéo cinema.

### 4.2 Movie Admin CRUD và thống kê

Thành phần chính:

- movie paging/filter;
- create/update/delete movie;
- upload/lưu poster qua `IFileStorageService`;
- increment movie view;
- movie daily view;
- highlight classification background job;
- genre/language/movie-genre entities.

Database liên quan:

- `MOVIE`
- `MOVIE_VIEW_LOG`
- `MOVIE_DAILY_VIEW`
- `GENRE`
- `LANGUAGE`
- `MOVIE_GENRE`

### 4.3 Review và moderation

Thành phần chính:

- `ReviewsController`
- `IReviewService`
- `ReviewService`
- `IAiModerationService`
- `GeminiModerationService`
- review edit/moderation history
- user spam/block fields

API chính:

- tạo review;
- xem review theo movie;
- xem review của user;
- sửa review;
- Admin approve review.

### 4.4 Hủy showtime và refund

Thành phần chính:

- `ShowtimeCancellationsController`
- `ShowtimeCancellationService`
- `RefundService`
- `RefundProcessor`
- `UnsupportedPaymentRefundGateway`
- event publisher abstraction

Luồng:

1. Manager/Admin yêu cầu hủy showtime.
2. Kiểm cinema scope.
3. Cập nhật showtime/cancellation.
4. Tìm booking/payment liên quan.
5. Tạo refund.
6. Release seat.
7. Xử lý refund tự động hoặc chuyển manual.
8. Gửi thông báo/email theo cấu hình.

Lưu ý:

- `UnsupportedPaymentRefundGateway` cho biết nhánh hiện chưa tích hợp refund
  thật với provider bên ngoài.
- Trường hợp không hỗ trợ được chuyển sang quy trình manual.

### 4.5 Customer-assisted refund

Thành phần chính:

- `CustomerRefundClaimsController`
- `RefundClaimService`
- `RefundClaimIssuer`
- `SensitiveDataProtector`

API hỗ trợ:

- lấy danh sách ngân hàng;
- resolve refund link/token;
- lưu tài khoản ngân hàng;
- submit refund claim;
- tạo customer refund request.

### 4.6 Manager/Admin refund

Manager:

- xem danh sách refund theo phạm vi cinema.

Admin:

- xem refund toàn hệ thống;
- confirm refund;
- xem manual refund;
- assign manual refund;
- xác nhận đã chuyển tiền thủ công.

### 4.7 Auth

Nhánh tích hợp giữ login JWT hiện tại và bổ sung contract Google login.

Admin tạo Staff sử dụng Hangfire để enqueue invitation email thay vì chờ SMTP
trong request.

### 4.8 Chatbot

Chatbot được cập nhật để gọi contract MovieService có paging/filter.

Database đã có `CHAT_HISTORY`, nhưng cần kiểm tra riêng việc persistence history
trong use case chatbot trước khi coi chức năng lịch sử chat là hoàn chỉnh.

## 5. Thay đổi database

### 5.1 Bảng mới đã được kiểm tra trên local DB

Truy vấn read-only được chạy trên:

```text
Server: localhost
Database: CinemaBookingDB
```

Kết quả:

| Bảng | Trạng thái |
|---|---|
| `BANK_DIRECTORY` | PRESENT |
| `CHAT_HISTORY` | PRESENT |
| `CUSTOMER_REFUND_REQUEST` | PRESENT |
| `GENRE` | PRESENT |
| `LANGUAGE` | PRESENT |
| `MANUAL_REFUND_PROCESS` | PRESENT |
| `MOVIE_DAILY_VIEW` | PRESENT |
| `MOVIE_GENRE` | PRESENT |
| `MOVIE_VIEW_LOG` | PRESENT |
| `REFUND_CLAIM` | PRESENT |
| `REFUND_CLAIM_TOKEN` | PRESENT |
| `REVIEW_EDIT_HISTORY` | PRESENT |
| `REVIEW_MODERATION_HISTORY` | PRESENT |

Kết quả: 13/13 bảng được kiểm tra đều tồn tại.

### 5.2 Cột Movie đã được kiểm tra

Các cột tồn tại:

- `MOVIE.averageRating`
- `MOVIE.dailyViews`
- `MOVIE.highlight`
- `MOVIE.totalReviews`
- `MOVIE.totalViews`
- `MOVIE.viewCount`

### 5.3 Cột User đã được kiểm tra

Các cột tồn tại:

- `USER.blockedUntil`
- `USER.isBlocked`
- `USER.spamViolationCount`

### 5.4 Script database được đưa vào nhánh

- `docs/database/Genre_Languge.sql`
- `docs/database/customer-assisted-refund-patch.txt`
- `docs/database/sprint-2-full-architecture.sql`
- `docs/database/sprint-2-review-and-views.sql`
- `docs/database/sprint-2-update-constraints.sql`
- schema tổng hợp `docs/database/cinema-booking-schema.sql`

Trong lần test tích hợp này không chạy script ghi dữ liệu/schema vì local DB đã
có đầy đủ bảng/cột cần kiểm tra. Chỉ chạy truy vấn read-only để tránh apply lặp
hoặc làm thay đổi dữ liệu test hiện có.

## 6. Kết quả build

Lệnh:

```text
dotnet build CinemaSystem.sln --no-restore
```

Kết quả:

```text
Build succeeded.
0 Error(s)
3 Warning(s)
```

Ba warning nullable:

- `CinemaSystem.Tests/CinemaApiIntegrationTests.cs`
- `CinemaSystem.Tests/ReviewServiceTests.cs`
- `CinemaSystem.Tests/RoomShowtimeServiceTests.cs`

Không có warning mới làm build thất bại.

## 7. Kết quả test

Lệnh:

```text
dotnet test CinemaSystem.sln --no-build --no-restore
```

Kết quả:

```text
Failed:  0
Passed:  225
Skipped: 0
Total:   225
Duration: 19 s
```

Kết luận:

- 100% test đang được compile trong solution tích hợp đều pass.
- Không có test failed hoặc skipped.

So với `main` trước merge:

- `main` đã chạy 239 test.
- nhánh tích hợp chạy 225 test;
- hai file `BookingsControllerTests.cs` và `CheckoutServiceTests.cs` được nhánh
  Admin đổi thành `.cs.txt`, nên không còn được test runner compile;
- nhánh Admin đồng thời thêm test mới cho cinema scope, refund, review và
  showtime cancellation;
- vì test set đã thay đổi, không dùng chênh lệch `239 - 225` để kết luận số test
  chức năng bị mất một cách trực tiếp.

## 8. Test mới đáng chú ý trong nhánh tích hợp

- `ManagerCinemaScopeApiIntegrationTests`
- `RefundProcessorTests`
- `RequestedFlowApiIntegrationTests`
- `ReviewServiceTests`
- `ReviewsControllerTests`
- `RoomSeatUpdateApiIntegrationTests`
- `ShowtimeCancellationApiIntegrationTests`

Các nhóm này kiểm tra:

- giới hạn Manager theo cinema;
- xử lý refund và fallback;
- movie/review requested flows;
- update room/seat;
- cancel showtime và trạng thái booking/refund liên quan.

## 9. Điểm cần lưu ý trước khi merge về nhánh phát triển chính

1. Checkout cũ đã bị loại bỏ. Frontend/API consumer đang gọi
   `POST /api/bookings/checkout` cần được cập nhật hoặc team phải quyết định khôi
   phục use case này theo contract mới.
2. `UnsupportedPaymentRefundGateway` chưa chuyển tiền refund thật.
3. Hai file test checkout `.cs.txt` không được chạy. Nếu checkout được giữ lại,
   phải đưa test trở lại `.cs`.
4. Snapshot EF đã có nhưng cần quản lý migration/schema patch nhất quán; không
   nên vừa chạy schema tổng hợp vừa chạy tất cả patch không idempotent.
5. `SyncDb.cs` và `SyncDbProj` là utility đồng bộ DB; không nằm trong
   `CinemaSystem.sln` chính, cần xác nhận có giữ lâu dài hay không.
6. Chat history có entity/table nhưng cần xác nhận service thực sự persist.
7. Google login có contract/interface thay đổi; cần kiểm tra cấu hình provider và
   integration test riêng trước production.
8. Các warning nullable trong test nên được xử lý nhưng không chặn build hiện tại.

## 10. Trạng thái cuối

Tại thời điểm tạo báo cáo:

- merge conflict: đã resolve hết;
- merge commit: đã tạo;
- build: pass;
- test: 225/225 pass;
- database schema check: pass;
- report: đã tạo;
- bước cuối còn lại: commit report và push nhánh test lên GitLab.
