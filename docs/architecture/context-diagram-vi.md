1. Guest - Khách Vãng Lai

Guest → System:

Yêu cầu tra cứu phim, rạp, lịch chiếu
Thông tin đăng ký tài khoản
Thông tin đăng nhập
OTP xác minh email
System → Guest:

Danh sách phim, rạp, lịch chiếu công khai
Kết quả đăng ký tài khoản
Kết quả đăng nhập
Kết quả xác minh email
Thông báo lỗi đăng ký/đăng nhập/xác minh
Giải thích: Guest chưa được đặt vé. Guest chỉ xem thông tin công khai, đăng ký, đăng nhập, verify email.

2. Customer - Khách Hàng Thành Viên

Customer → System:

Yêu cầu cập nhật hồ sơ cá nhân
Yêu cầu xem phim, lịch chiếu, sơ đồ ghế
Yêu cầu khóa ghế tạm thời
Thông tin đặt vé
Thông tin chọn F&B
Mã voucher / yêu cầu dùng điểm thưởng
Yêu cầu thanh toán booking
Yêu cầu xem lịch sử booking, vé, điểm thưởng
Nội dung đánh giá phim
System → Customer:

Thông tin hồ sơ cá nhân
Danh sách phim, lịch chiếu, sơ đồ ghế realtime
Trạng thái khóa ghế
Thông tin tổng tiền booking
Kết quả áp dụng voucher / điểm thưởng
Trạng thái booking
Trạng thái thanh toán
Vé điện tử / mã QR
Lịch sử booking, vé, điểm thưởng
Thông báo hủy suất chiếu / hoàn tiền
Kết quả gửi đánh giá phim
Giải thích: Customer là actor chính của nghiệp vụ đặt vé online. Các bước nhỏ như chọn ghế, dùng voucher, mua F&B, thanh toán nên gom thành nhóm booking/checkout để diagram không bị rối.

3. Staff - Nhân Viên Rạp

Staff → System:

Yêu cầu quét mã QR vé
Mã vé nhập thủ công để kiểm tra
Yêu cầu check-in vé
Yêu cầu bán vé tại quầy
Xác nhận xử lý đơn F&B tại rạp, nếu có
System → Staff:

Kết quả kiểm tra vé: hợp lệ, đã dùng, đã hủy, không hợp lệ
Thông tin vé, booking, suất chiếu, phòng, ghế
Kết quả check-in
Kết quả bán vé tại quầy
Thông tin đơn F&B cần xử lý, nếu có
Giải thích: Staff không nên quản lý phim, phòng, ghế, lịch chiếu. Staff chủ yếu scan vé, hỗ trợ vận hành và có thể bán vé tại quầy.

4. Manager - Quản Lý Rạp

Manager → System:

Yêu cầu xem dashboard chi nhánh/rạp
Yêu cầu xem báo cáo doanh thu
Yêu cầu xem thống kê booking
Yêu cầu xem danh sách payment/refund
Lệnh hủy suất chiếu do sự cố
Yêu cầu quản lý lịch chiếu trong phạm vi rạp
System → Manager:

Dashboard vận hành rạp
Báo cáo doanh thu
Thống kê booking, payment, refund
Kết quả hủy suất chiếu
Trạng thái hoàn tiền tự động/thủ công
Dữ liệu vận hành theo rạp/chi nhánh
Giải thích: Manager quản lý vận hành và báo cáo theo phạm vi rạp/cụm rạp, không phải toàn hệ thống.

5. Admin - Quản Trị Viên Hệ Thống

Admin → System:

Yêu cầu quản lý user và role
Yêu cầu khóa/mở khóa tài khoản
Yêu cầu quản lý phim
Yêu cầu quản lý rạp, phòng, ghế
Yêu cầu quản lý lịch chiếu
Yêu cầu quản lý voucher/promotion
Yêu cầu quản lý menu F&B
Yêu cầu cấu hình payment provider
Yêu cầu xem audit log và dashboard toàn hệ thống
System → Admin:

Danh sách user, role, trạng thái tài khoản
Kết quả cập nhật user/role
Danh sách và kết quả quản lý phim, rạp, phòng, ghế, lịch chiếu
Danh sách và kết quả quản lý voucher/F&B
Báo cáo toàn hệ thống
Nhật ký audit log
Kết quả cấu hình hệ thống
Giải thích: Admin là quyền cao nhất, quản lý dữ liệu nền và phân quyền toàn hệ thống.

6. Payment Gateway - VNPAY/MoMo

System → Payment Gateway:

Yêu cầu tạo giao dịch thanh toán
Thông tin booking cần thanh toán
Số tiền thanh toán
Mã giao dịch hệ thống
Yêu cầu hoàn tiền
Thông tin payment/refund cần đối soát
Payment Gateway → System:

Callback kết quả thanh toán
Trạng thái giao dịch: success, failed, cancelled, expired
Mã giao dịch từ nhà cung cấp
Callback kết quả hoàn tiền
Mã refund từ nhà cung cấp
Lỗi thanh toán/hoàn tiền nếu có
Giải thích: Hệ thống không tự xử lý tiền, mà gửi yêu cầu sang gateway và nhận callback.

7. Email / Notification Service

System → Email/Notification Service:

Email OTP xác minh tài khoản
Email OTP/link đặt lại mật khẩu
Email xác nhận booking
Email vé điện tử / mã QR
Thông báo hủy suất chiếu
Thông báo hoàn tiền
Thông báo trạng thái tài khoản/booking
Email/Notification Service → System:

Kết quả gửi email/thông báo
Trạng thái gửi thành công/thất bại
Lỗi gửi email/thông báo
Giải thích: Không vẽ Guest/Customer gửi trực tiếp đến Email Service. Hệ thống mới là bên yêu cầu gửi email.

8. Database - Tùy Chọn