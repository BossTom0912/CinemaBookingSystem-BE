óm tắt Luồng API của Customer
Chọn suất chiếu

GET /api/showtimes (Xem danh sách lịch chiếu)

GET /api/showtimes/{showtimeId} (Xem chi tiết suất chiếu đã chọn)

Chọn vị trí ghế

GET /api/seats/showtimes/{showtimeId}/map (Lấy sơ đồ ghế và trạng thái trống/đã đặt)

POST /api/seats/lock (Giữ ghế tạm thời để tránh người khác chọn trùng)

Tạo đơn hàng & Thanh toán

POST /api/bookings (Tạo đơn đặt vé với trạng thái Chờ thanh toán)

POST /api/Payment (Khởi tạo giao dịch, lấy thông tin/QR Code chuyển khoản)

POST /api/Payment/sepay-webhook (Hệ thống nhận thông báo thanh toán thành công từ SePay)

Kiểm tra và quản lý vé

GET /api/bookings/{bookingId} (Xem chi tiết vé vừa mua thành công)

GET /api/bookings/my-bookings (Xem lại lịch sử tất cả các vé của tôi)