# INTEGRATION TEST & QA REPORT

## 1. Executive Summary

* Overall merge status: Đã merge trước đó trên nhánh `Tom/integration-main-manager-admin-test`; worktree còn các thay đổi và báo cáo chưa commit.
* Overall E2E status:
  * Customer E2E: Pass ở Task 2. Booking `BOK_d71e512806c64880974a57ed93fd7427` đã được xác minh `PAID`, payment `SUCCESS`.
  * Kiểm tra âm tính quyền Customer: Pass. Customer truy cập `/admin/dashboard` bị chuyển về `/`.
  * Admin/Manager E2E: Blocked do chưa có mật khẩu test hợp lệ.
  * Manager FE: Blocked do FE hiện không có route, layout, page hoặc service cho Manager.
* Main risks:
  * Không thể chạy luồng Manager qua FE như yêu cầu với mã nguồn FE hiện tại.
  * Không thể xác minh Admin dashboard và API Manager ở trạng thái đăng nhập nếu chưa có credential.
  * Chưa được phép hủy một showtime thật khi chưa xác định được tài khoản Manager và phạm vi rạp.
  * Customer E2E còn lỗi hiển thị timezone giữa các màn hình.

## 2. Git & Code Review Log

* Target branch: `Tom/integration-main-manager-admin-test`
* Admin reference branch: `Admin`
* Feature branch merged: `integration-manager-features`
* Conflicts resolved: Xem `MERGE_ADMIN_ALIGNMENT_REPORT.md`.
* Admin alignment result: Xem `MERGE_ADMIN_ALIGNMENT_REPORT.md` và `MERGE_REVIEW.md`.
* Hardcoded values removed: Xem `MERGE_REVIEW.md`.
* Hardcoded values retained with reason: Xem `MERGE_REVIEW.md`.
* Safety:
  * Đã xác nhận đúng nhánh.
  * Không switch branch, commit, push hoặc force push.
  * Không chạy `INSERT`, `UPDATE`, `DELETE` trong Task 3.
* Build:
  * `dotnet build CinemaSystem.sln --no-restore`: Pass, 0 warning, 0 error.
  * `dotnet test CinemaSystem.sln --no-build --no-restore`: Pass, 231/231 test.
  * `npm.cmd run build`: Pass; Vite cảnh báo bundle JS lớn hơn 500 kB, ngoài phạm vi merge hiện tại.

## 3. Database Audit Results

* Customer test data status:
  * Email: `tommy090305@gmail.com`
  * User: `ACTIVE`, `emailVerified = 1`
* Booking/payment/ticket status verification:
  * Booking ID: `BOK_d71e512806c64880974a57ed93fd7427`
  * Booking: `PAID`
  * Payment ID: `PAY_f7bbea4569c34a6189288a9eec38458d`
  * Payment: `SUCCESS`
  * Showtime: `SHW004`
  * Seat: `B1`, trạng thái `BOOKED`
  * Ticket: 1 bản ghi, trạng thái `UNUSED`
* Refund generation verification:
  * Chưa thực hiện cancel showtime nên chưa phát sinh refund để xác minh.
  * Không chạy cập nhật DB liên quan refund trong Task 3.
* Missing tables/columns/relationships if any:
  * Không phát hiện thiếu bảng/cột trong các truy vấn SELECT đã thực hiện.
  * Schema thực tế dùng `BOOKING.totalAmount` và `SEAT.seatCode`.
* Admin/Manager account audit bằng SELECT:
  * Admin: `admin@test.com`, `ACTIVE`, đã xác minh email.
  * Manager:
    * `manager@test.com` — `CIN_BH_DN`
    * `manager.bh_dn@test.com` — `CIN_BH_DN`
    * `manager.dev_seed@test.com` — `CIN_DEV_SEED`
    * `manager.nd_q1@test.com` — `CIN_ND_Q1`
* Không đọc hoặc ghi password hash, OTP, JWT, refresh token hay connection string vào báo cáo.

## 4. Customer E2E Test Summary

* Register: Pass với `tommy090305@gmail.com`.
* Login: Pass.
* Movie selection: Pass, `Doraemon Movie 2026`.
* Seat selection: Pass, ghế `B1`.
* Combo/food selection: Pass, tổng F&B `75.000đ`.
* Booking/order: Pass, booking `BOK_d71e512806c64880974a57ed93fd7427`.
* DB completion update: Đã thực hiện trong Task 2, chỉ nhắm đúng booking/payment của tài khoản test do môi trường không có payment callback.
* Screenshots:
  * `test-artifacts/customer-e2e/11-payment-success-ticket-viewport.png`
  * `test-artifacts/customer-e2e/12-my-bookings-paid-viewport.png`
* Báo cáo chi tiết: `CUSTOMER_E2E_TEST_REPORT.md`.

## 5. Manager/Admin Test Summary

* Environment:
  * BE: `http://localhost:5070`, Swagger trả HTTP 200.
  * FE: `http://127.0.0.1:5173`, trang login trả HTTP 200.
* Backend automated verification:
  * Pass 231/231 test.
  * Có integration test cho Manager cinema scope, Admin bypass scope, Customer bị 403, Manager dashboard/revenue/ticket/occupancy, cancel showtime, refund generation, idempotency và manual refund.
* Admin login:
  * Status: Blocked.
  * Account tồn tại: `admin@test.com`.
  * `ADMIN_PASSWORD` không có trong environment; seed/config/docs không cung cấp mật khẩu dùng được.
* Manager login:
  * Status: Blocked.
  * Các account Manager tồn tại nhưng seed/config/docs không cung cấp mật khẩu dùng được.
* Permission negative test:
  * Status: Pass.
  * Phiên Customer truy cập trực tiếp `/admin/dashboard` bị FE chuyển về `/`.
  * Không có console error/warning liên quan trong lần kiểm tra này.
* Manager limit:
  * Backend integration tests: Pass cho giới hạn theo `cinemaId`, Manager thao tác ngoài rạp bị `403`, Admin được bypass scope.
  * FE E2E status: Blocked.
  * FE không có `managerRoles`, route Manager, page Manager hoặc API service cho Manager limit.
* Cancel performance/showtime:
  * Backend integration tests: Pass cho cancel hợp lệ, khác rạp bị `403`, gọi lần hai bị `409`, showtime đã bắt đầu bị `409`.
  * FE E2E status: Blocked.
  * FE chỉ có màn `ManageShowtime` thuộc cụm route Admin.
  * Chưa hủy `SHW004` hoặc showtime nào khác để tránh ảnh hưởng dữ liệu ngoài phạm vi rạp của Manager.
* Refund generation:
  * Backend integration tests: Pass cho tạo refund, refund muộn, xử lý provider không hỗ trợ và manual refund.
  * FE E2E status: Blocked vì chưa thể chạy cancel showtime bằng đúng role và scope.
* Revenue overview:
  * Backend integration tests: Pass cho net revenue theo rạp và Admin bypass scope.
  * FE E2E status: Blocked. FE không có page/route Manager revenue.
* Ticket overview:
  * Backend integration tests: Pass cho ticket count/occupancy và trường hợp partial refund.
  * FE E2E status: Blocked. FE không có page/route Manager ticket overview.
* Screenshots:
  * `test-artifacts/manager-admin/01-login-blocked-credentials-needed.png`
  * `test-artifacts/manager-admin/02-customer-denied-admin.png`

## 6. Bug & Error Log

### Bug ID: TASK3-BLOCKER-001

* Component: FE / BE / DB
* Description: Không có mật khẩu test hợp lệ cho các account Admin/Manager hiện có.
* Steps to reproduce:
  1. Kiểm tra seed/config/docs/environment.
  2. Xác nhận account tồn tại bằng SELECT.
  3. Không tìm thấy credential test hợp lệ; `ADMIN_PASSWORD` và `DEV_STAFF_PASSWORD` không được set.
* Expected result: Có credential test được quản lý qua environment/user secrets để chạy E2E.
* Actual result: Có user/role/profile nhưng không có mật khẩu test.
* Severity: Critical
* Status: Not fixed
* Fix files if fixed: N/A

### Bug ID: MGR-E2E-001

* Component: FE
* Description: FE chưa tích hợp giao diện và định tuyến cho role Manager.
* Steps to reproduce:
  1. Kiểm tra `src/App.tsx`: chỉ khai báo `customerRoles = ['customer']` và `adminRoles = ['admin']`.
  2. Kiểm tra `src/pages`, `src/layouts`, `src/services`: không có module Manager cho limit, cancel/refund, revenue hoặc ticket overview.
  3. `useLoginController` chỉ chuyển Admin tới `/admin/dashboard`; mọi role khác về `/`.
* Expected result: Manager đăng nhập được chuyển tới khu vực Manager và chỉ thấy chức năng đúng cinema scope.
* Actual result: Không có khu vực Manager để chạy E2E.
* Severity: Major
* Status: Not fixed
* Fix files if fixed: N/A — việc dựng toàn bộ module Manager FE cần task/thiết kế riêng, không phải sửa lỗi nhỏ trong backend merge.

### Bug ID: MGR-E2E-002

* Component: FE
* Description: `src/App.tsx` có route `dashboard` khai báo lặp hai lần và có text node `..` trước fallback route.
* Steps to reproduce:
  1. Mở `src/App.tsx`.
  2. Quan sát hai `<Route path="dashboard" ... />`.
  3. Quan sát `..` trước `<Route path="*" ... />`.
* Expected result: Route table sạch, không lặp và không có text node ngoài ý muốn.
* Actual result: FE vẫn build được nhưng route config chứa mã thừa/dễ gây lỗi bảo trì.
* Severity: Minor
* Status: Not fixed
* Fix files if fixed: N/A — FE nằm ngoài workspace backend hiện tại và lỗi không chặn build.

### Bug ID: CUST-E2E-001

* Component: FE
* Description: Lệch thời gian suất chiếu giữa modal và checkout/success/my-bookings.
* Expected result: Hiển thị nhất quán theo timezone hệ thống.
* Actual result: Modal `01/07/2026 19:00`; các màn sau `02:00 2/7/26`; DB `2026-07-01 19:00:00`.
* Severity: Major
* Status: Not fixed
* Fix files if fixed: N/A

### Bug ID: CUST-E2E-002

* Component: FE
* Description: Checkout hiển thị phone `Đang cập nhật` dù DB user có phone.
* Severity: Minor
* Status: Not fixed
* Fix files if fixed: N/A

### Bug ID: CUST-E2E-003

* Component: FE
* Description: Google Identity Services bị initialize nhiều lần.
* Actual result: Console cảnh báo `google.accounts.id.initialize() is called multiple times`.
* Severity: Minor
* Status: Not fixed
* Fix files if fixed: N/A

## 7. Final Recommendation

* Ready to commit: No.
* Required fixes before commit:
  1. Cung cấp mật khẩu test cho `admin@test.com` và một Manager, ưu tiên `manager@test.com` hoặc `manager.bh_dn@test.com`.
  2. Xác định hoặc tích hợp FE Manager module cho manager limit, cancel/refund, revenue và ticket overview.
  3. Sau đó chạy lại Admin login/dashboard, Manager login/scope, manager limit, cancel/refund và đối chiếu revenue/ticket với DB.
* Suggested next testing:
  * Dùng credential qua environment/user secrets; không hardcode vào source hoặc báo cáo.
  * Chọn showtime test chỉ có booking của tài khoản test hoặc tạo dữ liệu qua FE để tránh tác động người dùng khác.
  * Chỉ chạy targeted SELECT để đối chiếu revenue/ticket/refund; không cập nhật DB ngoài hành động nghiệp vụ qua FE/API.
