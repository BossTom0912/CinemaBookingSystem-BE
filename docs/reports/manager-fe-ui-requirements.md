# Manager FE UI Requirements

## Nguon phan tich

Anh trong task board dang co 4 BE task o trang thai In Review:

- SCRUM-190: Cai dat gioi han cho Manager theo rap.
- SCRUM-192: API Huy suat va sinh data hoan tien.
- SCRUM-195: API Tong quan doanh thu va ve trong rap rieng.
- SCRUM-198: API Soat ve, scan ticket.

Tu 4 task nay, FE nen xay mot khu vuc rieng cho Manager/Staff theo huong van hanh rap. Manager khong duoc chon tuy y `cinemaId`; backend se scope theo thong tin nhan vien/quan ly gan voi rap.

## Tong quan UI can co

Manager sidebar nen gom cac muc:

- Dashboard
- Showtimes
- Refunds
- Ticket Scanner
- Rooms / Seats neu FE da co module nay
- My Cinema / Thong tin rap cua toi

Tat ca man hinh Manager can co:

- Loading state.
- Empty state.
- Error state.
- Xu ly `401 Unauthorized`.
- Xu ly `403 Forbidden` ro rang, dac biet khi thao tac ngoai pham vi rap.
- Khong hien dropdown chon rap cho Manager neu khong co yeu cau Admin.

## SCRUM-190 - Gioi han Manager theo rap

### Muc tieu FE

Manager chi xem va thao tac du lieu thuoc rap minh quan ly. FE khong dung request body/query `cinemaId` nhu co che phan quyen chinh.

### UI can co

1. Manager layout
   - Sidebar rieng cho Manager.
   - Header hien role, ten user, rap dang quan ly neu API tra ve.
   - Badge hoac text nho: "Rap cua toi".

2. Man hinh My Cinema
   - Ten rap.
   - Dia chi.
   - Thanh pho.
   - So dien thoai.
   - Trang thai rap.
   - Danh sach phong thuoc rap.

3. Scope-aware screens
   - Dashboard chi hien du lieu cua rap backend tra ve.
   - Showtimes chi hien suat chieu trong rap.
   - Refunds chi hien refund lien quan rap.
   - Ticket scanner chi chap nhan ve thuoc rap.

### Xu ly loi can co

- `403`: hien thong bao "Ban khong co quyen thao tac voi du lieu cua rap nay".
- `404`: hien "Khong tim thay du lieu hoac du lieu khong thuoc rap cua ban".
- Neu profile Manager chua gan rap: hien empty/error state "Tai khoan Manager chua duoc gan rap".

## SCRUM-192 - Huy suat va sinh data hoan tien

### Muc tieu FE

Manager co the huy mot suat chieu hop le. Sau khi huy, backend tao du lieu refund cho cac booking/ve bi anh huong.

### UI can co

1. Showtime list
   - Bang/danh sach suat chieu.
   - Filter theo ngay.
   - Filter theo phim.
   - Filter theo phong.
   - Filter theo trang thai.
   - Badge trang thai: `OPEN`, `CANCELLED`, `COMPLETED`, `PROCESSING_UNSTABLE`, `SUSPENDED`.
   - Action "Huy suat" chi hien khi trang thai va quyen cho phep.

2. Showtime detail
   - Ten phim.
   - Phong.
   - Gio bat dau.
   - Gio ket thuc.
   - Gia co ban.
   - Trang thai.
   - So ghe da dat / so ve da ban neu API co.

3. Cancel showtime modal
   - Hien thong tin suat chieu sap huy.
   - Textarea nhap ly do huy.
   - Canh bao day la thao tac anh huong booking/refund.
   - Nut confirm.
   - Nut cancel.
   - Disable confirm khi ly do trong.

4. Cancel result / refund summary
   - Hien message thanh cong.
   - Trang thai showtime moi: `CANCELLED`.
   - So booking bi anh huong neu API co.
   - So refund duoc tao neu API co.
   - Link sang trang Manager Refunds.

5. Manager Refunds
   - Danh sach refund phat sinh tu showtime bi huy.
   - Filter theo refund status.
   - Filter theo ngay yeu cau.
   - Search theo booking id, customer, payment/refund code neu API ho tro.
   - Detail drawer/modal cho refund.

### Fields nen hien trong Refunds

- Refund id.
- Booking id.
- Customer name/email neu API tra ve.
- Showtime/movie.
- Refund amount.
- Refund status.
- Requested at.
- Refunded at neu co.
- Failure reason neu co.
- Workflow status neu API tra ve field derived.

### Trang thai refund FE nen support

- `PENDING`
- `REQUESTED`
- `PROCESSING`
- `MANUAL_REQUIRED`
- `SUCCESS`
- `FAILED`

## SCRUM-195 - Manager Dashboard doanh thu va ve

### Muc tieu FE

Manager xem tong quan doanh thu va ve trong rap rieng, khong phai toan he thong.

### UI can co

1. Dashboard summary cards
   - Tong doanh thu.
   - Tong ve ban.
   - Tong booking.
   - Tong refund neu API co.
   - Doanh thu sau refund neu API co.
   - Ty le lap day neu API co.

2. Filter bar
   - Date range.
   - Movie.
   - Room.
   - Showtime status.
   - Quick range: Hom nay, 7 ngay, 30 ngay, thang nay.

3. Charts
   - Revenue by day.
   - Tickets sold by day.
   - Top movies by revenue/tickets.
   - Room occupancy.

4. Tables
   - Top showtimes.
   - Movie performance.
   - Room performance.
   - Refund impact neu API co.

### UX can luu y

- Dashboard phai co empty state khi khong co du lieu trong khoang ngay.
- Khi API tra ve so 0, khong coi la loi.
- Neu Manager chua gan rap, dashboard khong nen hien du lieu global.

## SCRUM-198 - Soat ve / Scan Ticket

### Muc tieu FE

Staff/Manager scan QR ve va backend xac nhan ve co hop le trong dung rap hay khong.

### UI can co

1. Ticket scanner page
   - Camera QR scanner.
   - Input nhap QR/manual code.
   - Nut scan/submit.
   - Nut scan lai.
   - Trang thai camera permission.

2. Scan result panel
   - Success state mau xanh.
   - Failed state mau do/vang.
   - Message tu backend.
   - Ticket id.
   - Movie.
   - Showtime.
   - Room.
   - Seat.
   - Customer/guest info neu API tra ve.
   - Scan time.

3. Recent scans
   - Danh sach scan gan day trong session.
   - Result: `SUCCESS` hoac `FAILED`.
   - Failure reason.
   - Raw QR code rut gon.

4. Manual fallback
   - Dung khi camera khong duoc cap quyen.
   - Dung khi thiet bi khong co camera.
   - Cho phep paste QR code.

### Loi can hien ro

- Ve khong ton tai.
- Ve da check-in.
- Ve bi huy.
- Ve da refund.
- Suat chieu bi huy.
- Ve khong thuoc rap cua Manager/Staff.
- QR khong hop le.
- Server loi.

## Route de xuat

- `/manager/dashboard`
- `/manager/showtimes`
- `/manager/showtimes/:id`
- `/manager/showtimes/:id/cancel`
- `/manager/refunds`
- `/manager/refunds/:id`
- `/manager/ticket-scanner`
- `/manager/my-cinema`

Neu FE da co route convention khac thi giu convention hien tai, nhung nen tach khu vuc Manager ro rang.

## Component de xuat

- `ManagerLayout`
- `ManagerSidebar`
- `ManagerScopeBanner`
- `ManagerDashboardPage`
- `DashboardSummaryCards`
- `RevenueChart`
- `TicketSalesChart`
- `ShowtimeListPage`
- `ShowtimeFilters`
- `ShowtimeStatusBadge`
- `CancelShowtimeModal`
- `ManagerRefundListPage`
- `RefundStatusBadge`
- `RefundDetailDrawer`
- `TicketScannerPage`
- `QrScanner`
- `ManualQrInput`
- `ScanResultPanel`
- `RecentScanList`
- `MyCinemaPage`

## Thu tu uu tien trien khai

1. Manager layout va route guard.
2. Dashboard Manager.
3. Showtime list va cancel modal.
4. Manager refunds list/detail.
5. Ticket scanner.
6. My Cinema page.

## Luu y tich hop BE

- Khong hardcode role name phia FE neu token/API da co role convention.
- Khong tin `cinemaId` do user chon lam quyen.
- Neu API tra `403`, FE phai hien dung y nghia scope, khong hien loi chung chung.
- Refund flow co the la manual required; FE khong nen hien nhu da auto-refund thanh cong neu backend chua tra `SUCCESS`.
- Ticket scanner can goi backend de xac nhan, khong chi decode QR tren FE.
- Date/time hien thi nen format theo timezone nguoi dung, nhung du lieu gui len backend nen theo format API quy dinh.

## Ket luan

4 task nay tao thanh mot bo UI Manager hoan chinh: gioi han theo rap, dashboard rieng cua rap, huy suat sinh refund, va soat ve. FE nen uu tien cac man hinh van hanh lap lai hang ngay, khong lam nhu landing page. Trong UI, diem quan trong nhat la scope theo rap va xu ly refund/ticket status ro rang.
