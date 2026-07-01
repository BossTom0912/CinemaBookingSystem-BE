using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CinemaSystem.Domain.Constants;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Runtime implementation for the original booking create/detail/history
/// routes reached from <c>BookingsController</c>.
/// </summary>
/// <remarks>
/// Reads customer/showtime/seat/F&amp;B data, creates a PENDING_PAYMENT booking
/// and records temporary seat state through <c>CinemaDbContext</c>. The richer
/// transactional checkout path is implemented separately by
/// <c>CinemaSystem.Infrastructure.Bookings.CheckoutService</c>; payment is the
/// next use case handled by <c>PaymentService</c>.
/// </remarks>
public sealed class BookingService : IBookingService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly CinemaSystem.Application.Settings.SecuritySettings _securitySettings;

    public BookingService(CinemaDbContext dbContext, IClock clock, Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.SecuritySettings> securityOptions)
    {
        _dbContext = dbContext;
        _clock = clock;
        _securitySettings = securityOptions.Value;
    }

    public async Task<ServiceResult<BookingResponse>> CreateBookingAsync(
        CreateBookingRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        // Truy vấn thông tin hồ sơ khách hàng dựa trên ID người dùng
        var customerProfile = await _dbContext.CustomerProfiles
            // Thực thi truy vấn lấy bản ghi đầu tiên khớp với UserId
            .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);

        // Kiểm tra nếu không tìm thấy hồ sơ khách hàng
        if (customerProfile == null)
        {
            // Trả về lỗi 403 (Cấm truy cập) vì chỉ khách hàng mới được đặt vé
            return ServiceResult<BookingResponse>.Fail(403, "Only customers can book tickets.", "CUSTOMER_PROFILE_NOT_FOUND");
        }

        // Truy vấn thông tin suất chiếu dựa trên ID được yêu cầu
        var showtime = await _dbContext.Showtimes
            // Bao gồm thông tin Bộ phim
            .Include(s => s.Movie)
            // Bao gồm thông tin Phòng chiếu
            .Include(s => s.Room)
                // Từ Phòng chiếu lấy thông tin Rạp chiếu
                .ThenInclude(r => r.Cinema)
            // Thực thi truy vấn lấy bản ghi suất chiếu đầu tiên khớp với ID
            .FirstOrDefaultAsync(s => s.ShowtimeId == request.ShowtimeId, cancellationToken);

        // Kiểm tra nếu không tìm thấy suất chiếu
        if (showtime == null)
        {
            // Trả về lỗi 404 (Không tìm thấy)
            return ServiceResult<BookingResponse>.Fail(404, "Showtime not found.", "SHOWTIME_NOT_FOUND");
        }

        // Kiểm tra nếu trạng thái của suất chiếu đã bị HỦY hoặc đã ĐÓNG
        if (showtime.Status == DomainConstants.EntityStatus.Cancelled || showtime.Status == DomainConstants.EntityStatus.Closed)
        {
            // Trả về lỗi 400 (Yêu cầu không hợp lệ) vì suất chiếu không còn nhận đặt vé
            return ServiceResult<BookingResponse>.Fail(400, "This showtime is no longer accepting bookings.", "SHOWTIME_UNAVAILABLE");
        }

        // Truy vấn danh sách các ghế của suất chiếu dựa trên các ID ghế được gửi lên
        var showtimeSeats = await _dbContext.ShowtimeSeats
            // Bao gồm thông tin chi tiết của Ghế
            .Include(ss => ss.Seat)
            // Từ thông tin Ghế lấy loại ghế (SeatType) để biết phụ phí
            .ThenInclude(s => s.SeatType)
            // Lọc các ghế nằm trong danh sách ID được yêu cầu và thuộc về suất chiếu hiện tại
            .Where(ss => request.ShowtimeSeatIds.Contains(ss.ShowtimeSeatId) && ss.ShowtimeId == request.ShowtimeId)
            // Thực thi truy vấn và trả về dạng List bất đồng bộ
            .ToListAsync(cancellationToken);

        // Kiểm tra nếu số lượng ghế tìm thấy không khớp với số lượng ghế được yêu cầu
        if (showtimeSeats.Count != request.ShowtimeSeatIds.Count)
        {
            // Trả về lỗi 400 vì có ghế không hợp lệ
            return ServiceResult<BookingResponse>.Fail(400, "One or more selected seats are invalid.", "INVALID_SEATS");
        }

        // Lấy thời gian hiện tại từ hệ thống (chuẩn UTC)
        var now = _clock.UtcNow;

        // Kiểm tra nếu thời gian hiện tại đã vượt qua hoặc bằng thời gian bắt đầu chiếu
        if (now >= showtime.StartTime)
        {
            // Trả về lỗi 400 vì không thể đặt vé cho suất chiếu đã bắt đầu
            return ServiceResult<BookingResponse>.Fail(400, "Cannot book tickets for a showtime that has already started.", "SHOWTIME_STARTED");
        }

        // Duyệt qua từng ghế được chọn để kiểm tra tính hợp lệ
        foreach (var ss in showtimeSeats)
        {
            // Nếu ghế đã bị đặt (Booked)
            if (ss.SeatStatus == DomainConstants.EntityStatus.Booked)
            {
                // Trả về lỗi 409 (Xung đột) vì ghế đã có người đặt
                return ServiceResult<BookingResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is already booked.", "SEAT_ALREADY_BOOKED");
            }

            // Nếu ghế đang bị khóa (Locked) bởi người khác và thời gian khóa vẫn còn hiệu lực
            if (ss.SeatStatus == DomainConstants.EntityStatus.Locked && ss.LockedByUserId != userId && ss.LockedUntil > now)
            {
                // Trả về lỗi 409 vì ghế đang được giữ bởi người khác
                return ServiceResult<BookingResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is locked by another user.", "SEAT_LOCKED");
            }
        }

        // Khởi tạo biến lưu tổng số tiền cần thanh toán
        decimal totalAmount = 0;
        // Khởi tạo danh sách các ghế trong đơn đặt vé
        var bookingSeats = new List<BookingSeat>();
        // Duyệt qua từng ghế đã chọn để tính tiền và tạo bản ghi BookingSeat
        foreach (var ss in showtimeSeats)
        {
            // Tính giá của từng ghế: giá cơ bản của suất chiếu + phụ phí của loại ghế
            var seatPrice = showtime.BasePrice + ss.Seat.SeatType.ExtraFee;
            // Cộng dồn vào tổng tiền
            totalAmount += seatPrice;
            // Thêm thông tin ghế vào danh sách của đơn đặt vé
            bookingSeats.Add(new BookingSeat
            {
                // Tạo ID mới cho BookingSeat
                BookingSeatId = NewId("BKS"),
                // Gán ID ghế của suất chiếu
                ShowtimeSeatId = ss.ShowtimeSeatId,
                // Lưu lại giá của ghế tại thời điểm đặt
                SeatPrice = seatPrice
            });
        }

        // Xử lý thông tin Đồ ăn & Thức uống (F&B)
        var bookingFbItems = new List<BookingFbItem>();
        // Kiểm tra xem khách hàng có đặt kèm F&B không
        if (request.FoodAndBeverages != null && request.FoodAndBeverages.Any())
        {
            // Trích xuất danh sách ID của các món F&B được yêu cầu
            var fbItemIds = request.FoodAndBeverages.Select(f => f.FbItemId).ToList();
            // Truy vấn thông tin các món F&B từ cơ sở dữ liệu dựa trên ID
            var fbItems = await _dbContext.FbItems
                // Lọc theo danh sách ID
                .Where(f => fbItemIds.Contains(f.FbItemId))
                // Thực thi truy vấn trả về danh sách
                .ToListAsync(cancellationToken);

            // Duyệt qua từng yêu cầu món F&B
            foreach (var itemRequest in request.FoodAndBeverages)
            {
                // Tìm thông tin món F&B tương ứng trong danh sách đã lấy từ DB
                var fbItem = fbItems.FirstOrDefault(f => f.FbItemId == itemRequest.FbItemId);
                // Nếu không tìm thấy thì bỏ qua món này
                if (fbItem == null) continue;

                // Tính tổng tiền cho món này (giá món * số lượng)
                var subtotal = fbItem.Price * itemRequest.Quantity;
                // Cộng dồn vào tổng tiền thanh toán của đơn hàng
                totalAmount += subtotal;
                // Thêm thông tin món F&B vào danh sách chi tiết đơn đặt vé
                bookingFbItems.Add(new BookingFbItem
                {
                    // Tạo ID mới cho BookingFbItem
                    BookingFbitemId = NewId("BFI"),
                    // Gán ID của món F&B
                    FbItemId = fbItem.FbItemId,
                    // Lưu lại số lượng đặt
                    Quantity = itemRequest.Quantity,
                    // Lưu lại đơn giá tại thời điểm đặt
                    UnitPrice = fbItem.Price,
                    // Lưu lại thành tiền của món này
                    Subtotal = subtotal
                });
            }
        }

        // Tạo ID mới cho Đơn đặt vé (Booking)
        var bookingId = NewId("BOK");
        // Khởi tạo đối tượng Đơn đặt vé
        var booking = new Booking
        {
            // Gán ID đơn đặt vé
            BookingId = bookingId,
            // Gán ID hồ sơ khách hàng thực hiện đặt vé
            CustomerProfileId = customerProfile.CustomerProfileId,
            // Gán ID suất chiếu
            ShowtimeId = showtime.ShowtimeId,
            // Gán trạng thái khởi điểm của đơn là "Chờ thanh toán"
            BookingStatus = DomainConstants.EntityStatus.PendingPayment,
            // Gán tổng số tiền của đơn đặt vé
            TotalAmount = totalAmount,
            // Ghi nhận thời điểm tạo đơn
            CreatedAt = now,
            // Thiết lập thời gian hết hạn của đơn là 10 phút kể từ lúc tạo
            ExpiredAt = now.AddMinutes(10),
            // Ghi nhận kênh đặt vé là trực tuyến (ONLINE)
            BookingChannel = "ONLINE",
            // Gán danh sách các ghế đã đặt
            BookingSeats = bookingSeats,
            // Gán danh sách các món F&B đã đặt
            BookingFbItems = bookingFbItems
        };

        // Cập nhật trạng thái các ghế của suất chiếu thành ĐANG KHÓA (LOCKED)
        foreach (var ss in showtimeSeats)
        {
            // Cập nhật trạng thái ghế
            ss.SeatStatus = DomainConstants.EntityStatus.Locked;
            // Khóa ghế cho đến thời điểm hết hạn của đơn đặt vé
            ss.LockedUntil = booking.ExpiredAt;
            // Ghi nhận ID của người dùng đang khóa ghế
            ss.LockedByUserId = userId;
        }

        // Thêm đối tượng Booking vào context của Entity Framework
        _dbContext.Bookings.Add(booking);
        // Lưu toàn bộ thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả thành công chứa thông tin cơ bản của đơn đặt vé
        return ServiceResult<BookingResponse>.Ok(new BookingResponse
        {
            // Trả về ID đơn đặt vé
            BookingId = booking.BookingId,
            // Trả về ID suất chiếu
            ShowtimeId = booking.ShowtimeId,
            // Trả về tên bộ phim
            MovieTitle = showtime.Movie.Title,
            // Trả về tên rạp chiếu
            CinemaName = showtime.Room.Cinema.CinemaName,
            // Trả về tên phòng chiếu
            RoomName = showtime.Room.RoomName,
            // Trả về thời gian bắt đầu chiếu
            StartTime = showtime.StartTime,
            // Trả về tổng tiền đơn hàng
            TotalAmount = booking.TotalAmount,
            // Trả về trạng thái hiện tại
            Status = booking.BookingStatus,
            // Trả về thời điểm tạo
            CreatedAt = booking.CreatedAt,
            // Trả về thời điểm hết hạn thanh toán
            ExpiredAt = booking.ExpiredAt
        }, "Booking created successfully.");
    }

    public async Task<ServiceResult<BookingDetailsResponse>> GetBookingDetailsAsync(
        string bookingId,
        string userId,
        CancellationToken cancellationToken)
    {
        // Truy vấn thông tin chi tiết của Đơn đặt vé dựa trên ID
        var booking = await _dbContext.Bookings
            // Bao gồm thông tin Suất chiếu
            .Include(b => b.Showtime)
                // Từ Suất chiếu lấy thông tin Phim
                .ThenInclude(s => s.Movie)
            // Bao gồm thông tin Suất chiếu (nhánh 2)
            .Include(b => b.Showtime)
                // Từ Suất chiếu lấy thông tin Phòng chiếu
                .ThenInclude(s => s.Room)
                    // Từ Phòng chiếu lấy thông tin Rạp chiếu
                    .ThenInclude(r => r.Cinema)
            // Bao gồm danh sách các ghế trong đơn đặt vé
            .Include(b => b.BookingSeats)
                // Từ thông tin ghế đặt lấy thông tin ghế của suất chiếu
                .ThenInclude(bs => bs.ShowtimeSeat)
                    // Từ ghế suất chiếu lấy thông tin ghế vật lý
                    .ThenInclude(ss => ss.Seat)
                        // Từ ghế vật lý lấy thông tin loại ghế
                        .ThenInclude(s => s.SeatType)
            // Bao gồm danh sách các ghế trong đơn đặt vé (nhánh 2)
            .Include(b => b.BookingSeats)
                // Từ thông tin ghế đặt lấy thông tin Vé (Ticket) tương ứng
                .ThenInclude(bs => bs.Ticket)
            // Bao gồm danh sách các món F&B trong đơn đặt vé
            .Include(b => b.BookingFbItems)
                // Từ thông tin chi tiết F&B lấy dữ liệu của món F&B
                .ThenInclude(bfi => bfi.FbItem)
            // Bao gồm thông tin Hồ sơ khách hàng của người đặt vé
            .Include(b => b.CustomerProfile)
            // Thực thi truy vấn lấy bản ghi đầu tiên khớp với ID đơn đặt vé
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        // Kiểm tra nếu không tìm thấy đơn đặt vé
        if (booking == null)
        {
            // Trả về lỗi 404
            return ServiceResult<BookingDetailsResponse>.Fail(404, "Booking not found.", "BOOKING_NOT_FOUND");
        }

        // Kiểm tra xem đơn đặt vé này có thuộc về người dùng đang truy cập hay không
        if (booking.CustomerProfile?.UserId != userId)
        {
            // Trả về lỗi 403 (Cấm truy cập) nếu không phải chủ sở hữu của vé
            return ServiceResult<BookingDetailsResponse>.Fail(403, "You do not have permission to view this booking.", "FORBIDDEN");
        }

        // Trả về kết quả chi tiết của đơn đặt vé
        return ServiceResult<BookingDetailsResponse>.Ok(new BookingDetailsResponse
        {
            // Gán ID đơn đặt vé
            BookingId = booking.BookingId,
            // Gán ID suất chiếu
            ShowtimeId = booking.ShowtimeId,
            // Gán tên bộ phim
            MovieTitle = booking.Showtime.Movie.Title,
            // Gán tên rạp chiếu
            CinemaName = booking.Showtime.Room.Cinema.CinemaName,
            // Gán tên phòng chiếu
            RoomName = booking.Showtime.Room.RoomName,
            // Gán thời gian bắt đầu chiếu
            StartTime = booking.Showtime.StartTime,
            // Gán tổng tiền của đơn
            TotalAmount = booking.TotalAmount,
            // Gán trạng thái hiện tại của đơn
            Status = booking.BookingStatus,
            // Gán thời điểm tạo đơn
            CreatedAt = booking.CreatedAt,
            // Ánh xạ danh sách các ghế đã đặt sang DTO
            Seats = booking.BookingSeats.Select(bs => new BookedSeatDetailsResponse
            {
                // Gán ID của ghế trong suất chiếu
                SeatId = bs.ShowtimeSeat.SeatId,
                // Gán số ghế
                SeatNumber = bs.ShowtimeSeat.Seat.SeatNumber.ToString(),
                // Gán nhãn hàng ghế (VD: A, B, C...)
                RowLabel = bs.ShowtimeSeat.Seat.RowLabel,
                // Gán tên loại ghế (VD: Standard, VIP...)
                SeatType = bs.ShowtimeSeat.Seat.SeatType.TypeName,
                // Gán giá của ghế
                Price = bs.SeatPrice,
                // Gán ID của vé (nếu đã được phát hành)
                TicketId = bs.Ticket?.TicketId,
                // Gán mã QR của vé (nếu có)
                TicketQrCode = bs.Ticket?.QrCode,
                // Gán trạng thái hiện tại của vé
                TicketStatus = bs.Ticket?.TicketStatus
            }).ToList(),
            // Ánh xạ danh sách các món F&B đã đặt sang DTO
            FoodAndBeverages = booking.BookingFbItems.Select(bfi => new BookedFbItemResponse
            {
                // Gán tên món ăn/thức uống
                ItemName = bfi.FbItem.ItemName,
                // Gán số lượng đặt
                Quantity = bfi.Quantity,
                // Gán tổng tiền của món này
                Subtotal = bfi.Subtotal
            }).ToList()
        }, "Booking details retrieved successfully.");
    }

    public async Task<ServiceResult<IReadOnlyList<BookingResponse>>> GetMyBookingsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        // Truy vấn danh sách các đơn đặt vé của người dùng hiện tại
        var bookings = await _dbContext.Bookings
            // Bao gồm thông tin Hồ sơ khách hàng
            .Include(b => b.CustomerProfile)
            // Lọc các đơn đặt vé có Hồ sơ khách hàng khớp với ID người dùng
            .Where(b => b.CustomerProfile != null && b.CustomerProfile.UserId == userId)
            // Sắp xếp các đơn đặt vé mới nhất lên đầu
            .OrderByDescending(b => b.CreatedAt)
            // Ánh xạ dữ liệu sang đối tượng Response
            .Select(b => new BookingResponse
            {
                // Gán ID đơn đặt vé
                BookingId = b.BookingId,
                // Gán ID suất chiếu
                ShowtimeId = b.ShowtimeId,
                // Lấy tên phim từ Suất chiếu
                MovieTitle = b.Showtime.Movie.Title,
                // Lấy tên rạp chiếu từ Suất chiếu -> Phòng chiếu
                CinemaName = b.Showtime.Room.Cinema.CinemaName,
                // Lấy tên phòng chiếu
                RoomName = b.Showtime.Room.RoomName,
                // Lấy thời gian bắt đầu chiếu
                StartTime = b.Showtime.StartTime,
                // Lấy tổng tiền đơn hàng
                TotalAmount = b.TotalAmount,
                // Lấy trạng thái đơn hàng
                Status = b.BookingStatus,
                // Lấy thời điểm tạo đơn
                CreatedAt = b.CreatedAt,
                // Lấy thời điểm hết hạn thanh toán
                ExpiredAt = b.ExpiredAt
            })
            // Thực thi truy vấn trả về dạng List
            .ToListAsync(cancellationToken);

        // Trả về danh sách đơn đặt vé thành công
        return ServiceResult<IReadOnlyList<BookingResponse>>.Ok(bookings, "My bookings retrieved successfully.");
    }

    public async Task<ServiceResult<bool>> ConfirmTimeChangeAsync(
        string bookingId,
        bool accept,
        string token,
        CancellationToken cancellationToken)
    {
        // 1. Xác thực tính hợp lệ của token
        // Lấy khóa bí mật dùng để xác thực token từ cài đặt hệ thống
        var secret = _securitySettings.ConfirmationTokenSecret;
        // Khởi tạo thuật toán mã hóa HMACSHA256 với khóa bí mật
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        // Tính toán mã băm dựa trên ID của đơn đặt vé
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(bookingId));
        // Chuyển đổi mã băm thành chuỗi Base64 để làm token dự kiến
        var expectedToken = Convert.ToBase64String(hash);

        // Chuẩn hóa token gửi lên (để đảm bảo URL an toàn nếu cần) và so sánh với token dự kiến
        if (token.Replace(" ", "+") != expectedToken)
        {
            // Trả về lỗi 400 nếu token không hợp lệ hoặc đã hết hạn
            return ServiceResult<bool>.Fail(400, "Invalid or expired token.", "INVALID_TOKEN");
        }

        // Truy vấn thông tin đơn đặt vé kèm theo dữ liệu thanh toán
        var booking = await _dbContext.Bookings
            // Bao gồm danh sách các giao dịch thanh toán
            .Include(b => b.Payments)
            // Lấy bản ghi đầu tiên khớp với ID
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        // Kiểm tra nếu không tìm thấy đơn đặt vé
        if (booking == null) return ServiceResult<bool>.Fail(404, "Booking not found.", "NOT_FOUND");
        // Kiểm tra nếu trạng thái đơn không phải là đang chờ xác nhận thay đổi lịch chiếu
        if (booking.BookingStatus != DomainConstants.EntityStatus.ProcessingUnstable) 
            return ServiceResult<bool>.Fail(400, "Booking is not pending a time change confirmation.", "INVALID_STATUS");

        // Nếu người dùng chọn ĐỒNG Ý với lịch chiếu mới
        if (accept)
        {
            // Cập nhật trạng thái đơn vé quay lại thành Đã thanh toán (PAID)
            booking.BookingStatus = DomainConstants.EntityStatus.Paid;
            // Lưu thay đổi vào cơ sở dữ liệu
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Trả về kết quả xác nhận thành công
            return ServiceResult<bool>.Ok(true, "Time change accepted successfully.");
        }
        // Nếu người dùng chọn TỪ CHỐI lịch chiếu mới
        else
        {
            // Đổi trạng thái đơn vé thành Đang chờ hoàn tiền (PENDING_REFUND)
            booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;
            
            // Tìm giao dịch thanh toán thành công của đơn hàng này
            var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == DomainConstants.RefundStatus.Success) ?? booking.Payments.FirstOrDefault();
            
            // Nếu không tìm thấy thông tin thanh toán trong danh sách đã tải
            if (payment == null)
            {
                // Truy vấn trực tiếp từ database để lấy giao dịch thanh toán thành công
                payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId && p.PaymentStatus == DomainConstants.RefundStatus.Success, cancellationToken);
                    
                // Nếu vẫn không có giao dịch thành công, lấy bất kỳ giao dịch nào của đơn này
                if (payment == null)
                {
                    payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.BookingId, cancellationToken);
                }
            }
            else
            {
                // Kiểm tra xem bản ghi thanh toán này có thực sự tồn tại trong CSDL không
                bool exists = await _dbContext.Payments.AnyAsync(p => p.PaymentId == payment.PaymentId, cancellationToken);
                // Nếu không tồn tại thì gán thành null
                if (!exists) payment = null;
            }

            // Nếu không có thông tin thanh toán hợp lệ
            if (payment == null || string.IsNullOrEmpty(payment.PaymentId) || string.IsNullOrEmpty(payment.PaymentProviderId))
            {
                // Trả về lỗi vì không có căn cứ để thực hiện hoàn tiền
                return ServiceResult<bool>.Fail(400, "Cannot reject time change because no valid payment record exists to process the refund.", "INVALID_PAYMENT");
            }

            // Khởi tạo một đối tượng Hoàn tiền (Refund) mới
            var refund = new Refund
            {
                // Tạo ID hoàn tiền mới
                RefundId = NewId("REF"),
                // Gán ID đơn đặt vé
                BookingId = booking.BookingId,
                // Gán ID giao dịch thanh toán gốc
                PaymentId = payment.PaymentId,
                // Gán ID của nhà cung cấp dịch vụ thanh toán
                PaymentProviderId = payment.PaymentProviderId,
                // Gán số tiền cần hoàn (bằng tổng tiền đã thanh toán)
                RefundAmount = booking.TotalAmount,
                // Đặt trạng thái ban đầu là Đang chờ xử lý
                RefundStatus = DomainConstants.RefundStatus.Pending,
                // Ghi nhận lý do hoàn tiền
                RefundReason = "User rejected time change",
                // Ghi nhận thời điểm yêu cầu hoàn tiền
                RequestedAt = _clock.UtcNow
            };
            // Thêm đối tượng hoàn tiền vào DbContext
            _dbContext.Refunds.Add(refund);
            // Lưu mọi thay đổi vào CSDL
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Trả về kết quả từ chối thành công và bắt đầu tiến trình hoàn tiền
            return ServiceResult<bool>.Ok(true, "Time change rejected. Refund initiated.");
        }
    }

    public async Task<ServiceResult<bool>> CancelBookingAsync(
        string bookingId,
        string userId,
        CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.CustomerProfile)
            .Include(b => b.BookingSeats)
                .ThenInclude(bs => bs.ShowtimeSeat)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        if (booking == null)
        {
            return ServiceResult<bool>.Fail(404, "Booking not found.", "BOOKING_NOT_FOUND");
        }

        if (booking.CustomerProfile?.UserId != userId)
        {
            return ServiceResult<bool>.Fail(403, "You do not have permission to cancel this booking.", "FORBIDDEN");
        }

        if (booking.BookingStatus != DomainConstants.EntityStatus.PendingPayment)
        {
            return ServiceResult<bool>.Fail(400, "Only bookings in pending payment status can be cancelled.", "INVALID_STATUS");
        }

        booking.BookingStatus = DomainConstants.EntityStatus.Cancelled;

        foreach (var bs in booking.BookingSeats)
        {
            if (bs.ShowtimeSeat != null && bs.ShowtimeSeat.SeatStatus == DomainConstants.EntityStatus.Locked && bs.ShowtimeSeat.LockedByUserId == userId)
            {
                bs.ShowtimeSeat.SeatStatus = DomainConstants.EntityStatus.Available;
                bs.ShowtimeSeat.LockedUntil = null;
                bs.ShowtimeSeat.LockedByUserId = null;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Booking cancelled successfully.");
    }

    // Hàm tiện ích để tạo ID mới với tiền tố (prefix) cho các thực thể
    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
