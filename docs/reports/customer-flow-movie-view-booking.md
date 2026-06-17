# Customer Flow: Movie View And Booking

## 1. Xem phim

- `GET /api/movies?status=NOW_SHOWING`: xem danh sách phim đang chiếu.
- `GET /api/movies/{movieId}`: xem chi tiết phim đã chọn.

## 2. Chọn suất chiếu

- `GET /api/showtimes`: xem danh sách lịch chiếu.
- `GET /api/showtimes/{showtimeId}`: xem chi tiết suất chiếu đã chọn.

## 3. Chọn vị trí ghế

- `GET /api/seats/showtimes/{showtimeId}/map`: lấy sơ đồ ghế và trạng thái.
- `POST /api/seats/lock`: giữ ghế tạm thời để tránh chọn trùng.

## 4. Tạo booking và thanh toán

- `POST /api/bookings`: tạo đơn đặt vé ở trạng thái chờ thanh toán.
- `POST /api/payment`: khởi tạo giao dịch và lấy thông tin chuyển khoản.
- `POST /api/payment/sepay-webhook`: nhận thông báo thanh toán từ SePay.

## 5. Kiểm tra và quản lý booking

- `GET /api/bookings/{bookingId}`: xem chi tiết booking.
- `GET /api/bookings/my-bookings`: xem lịch sử booking của Customer.
