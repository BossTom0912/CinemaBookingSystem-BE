using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using MediatR;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Use case quản lý ghế và khóa ghế theo suất chiếu.
/// </summary>
/// <remarks>
/// Nhận lệnh từ <c>CinemaSystem/Controllers/SeatsController.cs</c>; CRUD đi tới
/// ROOM/SEAT, còn lock/unlock/map đi tới SHOWTIME_SEAT và BOOKING_SEAT.
/// <see cref="ISeatLockStore"/> chuyển tiếp sang RedisSeatLockStore hoặc
/// InMemorySeatLockStore; kết quả quay về SeatsController.
/// </remarks>
public sealed class SeatService : ISeatService
{
    // Định nghĩa hằng số cho hành động Tạo mới
    private const string ActionCreate = DomainConstants.Action.Create;
    // Định nghĩa hằng số cho hành động Cập nhật
    private const string ActionUpdate = DomainConstants.Action.Update;
    // Định nghĩa hằng số cho hành động Xóa
    private const string ActionDelete = DomainConstants.Action.Delete;
    // Định nghĩa hằng số cho trạng thái Chờ duyệt
    private const string StatusPending = DomainConstants.ApprovalStatus.Pending;
    // Định nghĩa hằng số cho trạng thái Đã duyệt
    private const string StatusApproved = DomainConstants.ApprovalStatus.Approved;
    // Định nghĩa hằng số cho trạng thái Từ chối
    private const string StatusRejected = DomainConstants.ApprovalStatus.Rejected;
    // Định nghĩa hằng số cho trạng thái ghế Trống
    private const string SeatAvailable = DomainConstants.EntityStatus.Available;
    // Định nghĩa hằng số cho trạng thái ghế Đang bị khóa
    private const string SeatLocked = DomainConstants.EntityStatus.Locked;
    // Định nghĩa hằng số cho trạng thái ghế Đã bán
    private const string SeatBooked = DomainConstants.EntityStatus.Booked;

    // Biến lưu trữ DbContext để thao tác với cơ sở dữ liệu
    private readonly CinemaDbContext _dbContext;
    // Biến lưu trữ dịch vụ khóa ghế
    private readonly ISeatLockStore _seatLockStore;
    // Biến lưu trữ dịch vụ xử lý công việc chạy nền (Hangfire)
    private readonly IBackgroundJobClient _backgroundJobClient;
    // Biến lưu trữ dịch vụ hoàn tiền của Admin
    private readonly IAdminRefundService _refundService;
    // Biến lưu trữ cấu hình bảo mật
    private readonly CinemaSystem.Application.Settings.SecuritySettings _securitySettings;
    // Biến lưu trữ cấu hình các mẫu email
    private readonly CinemaSystem.Application.Settings.EmailTemplatesSettings _emailTemplates;
    // Biến lưu trữ cấu hình liên quan đến đặt vé
    private readonly CinemaSystem.Infrastructure.Configuration.BookingSettings _bookingSettings;

    // Constructor khởi tạo SeatService cùng các dependencies được tiêm vào (Dependency Injection)
    public SeatService(
        CinemaDbContext dbContext,
        IBackgroundJobClient backgroundJobClient,
        IAdminRefundService refundService,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.SecuritySettings> securityOptions,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.EmailTemplatesSettings> emailTemplatesOptions,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Infrastructure.Configuration.BookingSettings> bookingOptions,
        ISeatLockStore? seatLockStore = null)
    {
        // Gán dbContext, ném ngoại lệ nếu null
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        // Gán dịch vụ chạy nền Hangfire
        _backgroundJobClient = backgroundJobClient;
        // Gán dịch vụ hoàn tiền
        _refundService = refundService;
        // Gán các cài đặt bảo mật lấy từ Options
        _securitySettings = securityOptions.Value;
        // Gán các cài đặt mẫu email lấy từ Options
        _emailTemplates = emailTemplatesOptions.Value;
        // Gán các cài đặt đặt vé lấy từ Options
        _bookingSettings = bookingOptions.Value;
        // Khởi tạo dịch vụ khóa ghế, nếu không được tiêm vào thì dùng mặc định là InMemorySeatLockStore
        _seatLockStore = seatLockStore ?? new InMemorySeatLockStore();
    }

    // Phương thức lấy danh sách ghế theo phòng chiếu
    public async Task<ServiceResult<IEnumerable<SeatResponse>>> GetSeatsByRoomAsync(
        string roomId,
        CancellationToken cancellationToken)
    {
        // Kiểm tra nếu mã phòng chiếu bị trống
        if (string.IsNullOrWhiteSpace(roomId))
        {
            // Trả về lỗi yêu cầu phải có mã phòng chiếu
            return ServiceResult<IEnumerable<SeatResponse>>.Fail(
                400,
                "Room ID is required.",
                "INVALID_ROOM_ID");
        }

        // Truy vấn danh sách ghế từ cơ sở dữ liệu
        var seats = await _dbContext.Seats
            // Không theo dõi các thay đổi (để tối ưu hiệu suất đọc)
            .AsNoTracking()
            // Lọc ra các ghế thuộc phòng chiếu tương ứng
            .Where(seat => seat.RoomId == roomId)
            // Sắp xếp theo hàng (RowLabel)
            .OrderBy(seat => seat.RowLabel)
            // Sau đó sắp xếp theo số ghế (SeatNumber)
            .ThenBy(seat => seat.SeatNumber)
            // Chuyển đổi dữ liệu ghế sang dạng phản hồi SeatResponse
            .Select(seat => ToSeatResponse(seat))
            // Thực thi truy vấn và chuyển thành danh sách bất đồng bộ
            .ToListAsync(cancellationToken);

        // Trả về kết quả thành công chứa danh sách ghế
        return ServiceResult<IEnumerable<SeatResponse>>.Ok(
            seats,
            $"Retrieved {seats.Count} seat(s) for room {roomId}.");
    }

    // Phương thức lấy danh sách ghế có phân trang và lọc
    public async Task<ServiceResult<PagedList<SeatResponse>>> GetSeatsAsync(
        string? roomId,
        bool? isActive,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Bắt đầu truy vấn ghế, không theo dõi các thay đổi
        var query = _dbContext.Seats.AsNoTracking();

        // Nếu mã phòng chiếu không trống
        if (!string.IsNullOrWhiteSpace(roomId))
        {
            // Thêm điều kiện lọc theo mã phòng chiếu
            query = query.Where(s => s.RoomId == roomId);
        }

        // Nếu cờ trạng thái hoạt động có giá trị
        if (isActive.HasValue)
        {
            // Thêm điều kiện lọc theo trạng thái hoạt động
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        // Đếm tổng số ghế thỏa mãn điều kiện lọc
        var totalCount = await query.CountAsync(cancellationToken);

        // Truy vấn danh sách ghế
        var seats = await query
            // Sắp xếp theo mã phòng chiếu
            .OrderBy(s => s.RoomId)
            // Tiếp tục sắp xếp theo hàng ghế
            .ThenBy(s => s.RowLabel)
            // Cuối cùng sắp xếp theo số ghế
            .ThenBy(s => s.SeatNumber)
            // Bỏ qua số lượng ghế của các trang trước đó
            .Skip((pageIndex - 1) * pageSize)
            // Lấy số lượng ghế của trang hiện tại
            .Take(pageSize)
            // Chuyển đổi mỗi ghế thành đối tượng phản hồi
            .Select(seat => ToSeatResponse(seat))
            // Thực thi và trả về danh sách bất đồng bộ
            .ToListAsync(cancellationToken);

        // Tạo đối tượng danh sách phân trang
        var pagedList = new PagedList<SeatResponse>(seats, totalCount, pageIndex, pageSize);

        // Trả về kết quả thành công chứa danh sách ghế đã phân trang
        return ServiceResult<PagedList<SeatResponse>>.Ok(
            pagedList,
            "Seats retrieved successfully.");
    }

    // Phương thức lấy thông tin một ghế theo mã ID
    public async Task<ServiceResult<SeatResponse>> GetSeatByIdAsync(
        string seatId,
        CancellationToken cancellationToken)
    {
        // Truy vấn ghế từ cơ sở dữ liệu
        var seat = await _dbContext.Seats
            // Không theo dõi thay đổi
            .AsNoTracking()
            // Tìm ghế có mã ID khớp với đầu vào
            .FirstOrDefaultAsync(s => s.SeatId == seatId, cancellationToken);

        // Nếu không tìm thấy ghế
        if (seat == null)
        {
            // Trả về lỗi không tìm thấy ghế
            return ServiceResult<SeatResponse>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        // Trả về thông tin ghế thành công
        return ServiceResult<SeatResponse>.Ok(ToSeatResponse(seat), "Seat retrieved successfully.");
    }

    // Phương thức tạo ghế mới
    public async Task<ServiceResult<bool>> CreateSeatAsync(
        CreateSeatRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        // Kiểm tra xem ID người dùng có hợp lệ không
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Trả về lỗi thiếu thông tin người dùng
            return ServiceResult<bool>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        // Tìm kiếm phòng chiếu theo mã phòng
        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomId == request.RoomId, cancellationToken);
        // Nếu không tìm thấy phòng chiếu
        if (room == null)
        {
            // Trả về lỗi không tìm thấy phòng chiếu
            return ServiceResult<bool>.Fail(404, "Room not found.", "ROOM_NOT_FOUND");
        }

        // Kiểm tra xem loại ghế có tồn tại hay không
        var seatTypeExists = await _dbContext.SeatTypes
            .AnyAsync(st => st.SeatTypeId == request.SeatTypeId, cancellationToken);
        // Nếu loại ghế không tồn tại
        if (!seatTypeExists)
        {
            // Trả về lỗi không tìm thấy loại ghế
            return ServiceResult<bool>.Fail(404, "Seat type not found.", "SEAT_TYPE_NOT_FOUND");
        }

        // Tạo mã hiển thị của ghế (VD: A1)
        var seatCode = BuildSeatCode(request.RowLabel, request.SeatNumber);
        // Kiểm tra xem mã ghế đã tồn tại trong phòng chiếu này chưa
        var seatExists = await _dbContext.Seats
            .AnyAsync(s => s.RoomId == request.RoomId && s.SeatCode == seatCode, cancellationToken);
        // Nếu ghế đã tồn tại
        if (seatExists)
        {
            // Trả về lỗi trùng mã ghế
            return ServiceResult<bool>.Fail(409, $"Seat {seatCode} already exists.", "SEAT_ALREADY_EXISTS");
        }

        // Khởi tạo đối tượng ghế mới
        var seat = new Seat
        {
            // Tạo mã ID tự động ngẫu nhiên
            SeatId = Guid.NewGuid().ToString("N"),
            // Gán mã phòng chiếu
            RoomId = request.RoomId,
            // Gán ký hiệu hàng ghế
            RowLabel = request.RowLabel,
            // Gán số thứ tự ghế
            SeatNumber = request.SeatNumber,
            // Gán mã hiển thị ghế
            SeatCode = seatCode,
            // Gán mã loại ghế
            SeatTypeId = request.SeatTypeId,
            // Kích hoạt ghế
            IsActive = true
        };

        // Thêm ghế mới vào db context
        await _dbContext.Seats.AddAsync(seat, cancellationToken);
        // Lưu thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả tạo ghế thành công
        return ServiceResult<bool>.Ok(true, "Seat created successfully.", 201);
    }

    // Phương thức cập nhật thông tin ghế
    public async Task<ServiceResult<bool>> UpdateSeatAsync(
        UpdateSeatRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        // Kiểm tra người dùng có hợp lệ không
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Trả về lỗi thiếu thông tin người dùng
            return ServiceResult<bool>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        // Tìm ghế cần cập nhật trong cơ sở dữ liệu
        var seat = await _dbContext.Seats.FirstOrDefaultAsync(s => s.SeatId == request.SeatId, cancellationToken);
        // Nếu không tìm thấy ghế
        if (seat == null)
        {
            // Trả về lỗi không tìm thấy ghế
            return ServiceResult<bool>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        // Tạo mã ghế mới dựa trên nhãn hàng và số ghế từ request
        var newSeatCode = BuildSeatCode(request.RowLabel, request.SeatNumber);
        // Kiểm tra xem có ghế nào khác trong cùng phòng đang dùng mã này không
        var duplicatedSeat = await _dbContext.Seats
            .AnyAsync(item => item.RoomId == seat.RoomId && item.SeatCode == newSeatCode && item.SeatId != seat.SeatId, cancellationToken);
        // Nếu phát hiện trùng lặp
        if (duplicatedSeat)
        {
            // Trả về lỗi mã ghế đã tồn tại
            return ServiceResult<bool>.Fail(409, "Seat code already exists in this room.", "DUPLICATE_SEAT");
        }

        // Cập nhật nhãn hàng ghế
        seat.RowLabel = request.RowLabel;
        // Cập nhật số ghế
        seat.SeatNumber = request.SeatNumber;
        // Cập nhật mã hiển thị ghế
        seat.SeatCode = newSeatCode;
        // Cập nhật loại ghế
        seat.SeatTypeId = request.SeatTypeId;
        
        // Khởi tạo biến theo dõi việc ghế chuyển sang trạng thái bảo trì
        bool isMaintenanceTriggered = false;
        // Nếu request yêu cầu vô hiệu hóa ghế và ghế hiện đang kích hoạt
        if (!request.IsActive && seat.IsActive)
        {
            // Đánh dấu cần xử lý bảo trì ghế
            isMaintenanceTriggered = true;
        }
        // Cập nhật trạng thái hoạt động của ghế
        seat.IsActive = request.IsActive;

        // Nếu trạng thái bảo trì được kích hoạt
        if (isMaintenanceTriggered)
        {
            // Tìm các ghế trong các suất chiếu tương lai
            var futureShowtimeSeats = await _dbContext.ShowtimeSeats
                // Nạp thông tin suất chiếu
                .Include(item => item.Showtime)
                // Nạp thông tin vé đặt kèm ghế
                .Include(item => item.BookingSeat)
                    // Nạp chi tiết Booking
                    .ThenInclude(bs => bs!.Booking)
                        // Nạp hồ sơ khách hàng của Booking
                        .ThenInclude(b => b!.CustomerProfile)
                            // Nạp User từ hồ sơ khách hàng
                            .ThenInclude(cp => cp!.User)
                // Lọc theo mã ghế, trạng thái suất chiếu là Open và thời gian bắt đầu ở tương lai
                .Where(item => item.SeatId == seat.SeatId 
                            && item.Showtime.Status == DomainConstants.EntityStatus.Open 
                            && item.Showtime.StartTime > DateTime.UtcNow)
                // Chuyển kết quả thành danh sách bất đồng bộ
                .ToListAsync(cancellationToken);

            // Duyệt qua từng ghế của các suất chiếu tìm được
            foreach (var sts in futureShowtimeSeats)
            {
                // Cập nhật trạng thái ghế thành đang bảo trì
                sts.SeatStatus = DomainConstants.EntityStatus.Maintenance;
                
                // Nếu ghế này đã được đặt trong một Booking
                if (sts.BookingSeat != null && sts.BookingSeat.Booking != null)
                {
                    // Lấy đối tượng Booking
                    var booking = sts.BookingSeat.Booking;
                    // Lấy email khách hàng (ưu tiên người dùng hệ thống hoặc khách vãng lai)
                    var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
                    // Nếu tìm thấy email hợp lệ
                    if (!string.IsNullOrEmpty(customerEmail))
                    {
                        // Lấy khóa bí mật để tạo token xác nhận từ cấu hình
                        var secret = _securitySettings.ConfirmationTokenSecret;
                        // Khởi tạo thuật toán mã hóa HMACSHA256 với khóa bí mật
                        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
                        // Băm ID của Booking
                        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(booking.BookingId));
                        // Chuyển mã băm thành chuỗi Base64
                        var token = Convert.ToBase64String(hash);
                        // Mã hóa token để an toàn khi truyền qua URL
                        var encodedToken = System.Uri.EscapeDataString(token);
                        
                        // Lấy tiêu đề email thông báo bảo trì ghế từ cấu hình
                        string subject = _emailTemplates.SeatMaintenanceSubject;
                        // Xây dựng nội dung email từ mẫu cấu hình và thay thế các thông số
                        string message = string.Format(_emailTemplates.SeatMaintenanceBody,
                            seat.SeatCode,
                            sts.Showtime.StartTime.ToString("dd/MM/yyyy HH:mm"),
                            booking.BookingId,
                            encodedToken);
                        // Đẩy công việc gửi email vào hàng đợi Hangfire để chạy nền
                        _backgroundJobClient.Enqueue<IEmailService>(email => email.SendEmailAsync(customerEmail, subject, message, CancellationToken.None));
                    }
                }
            }
        }

        // Lưu toàn bộ thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả thành công cho việc cập nhật ghế
        return ServiceResult<bool>.Ok(true, "Seat updated successfully.");
    }

    // Phương thức xóa ghế (thực chất là vô hiệu hóa ghế)
    public async Task<ServiceResult<bool>> DeleteSeatAsync(
        string seatId,
        string userId,
        CancellationToken cancellationToken)
    {
        // Kiểm tra xem ID người dùng có hợp lệ không
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Trả về lỗi thiếu thông tin người dùng
            return ServiceResult<bool>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        // Tìm ghế cần xóa theo mã ID
        var seat = await _dbContext.Seats.FirstOrDefaultAsync(s => s.SeatId == seatId, cancellationToken);
        // Nếu không tìm thấy ghế
        if (seat == null)
        {
            // Trả về lỗi không tìm thấy ghế
            return ServiceResult<bool>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        // Tìm danh sách các suất chiếu trong tương lai có liên quan đến ghế này
        var futureShowtimeSeats = await _dbContext.ShowtimeSeats
            // Nạp thông tin suất chiếu
            .Include(item => item.Showtime)
            // Điều kiện: đúng mã ghế, suất chiếu Open và thời gian ở tương lai
            .Where(item => item.SeatId == seatId && item.Showtime.Status == DomainConstants.EntityStatus.Open && item.Showtime.StartTime > DateTime.UtcNow)
            // Lấy ra danh sách
            .ToListAsync(cancellationToken);
            
        // Duyệt qua từng ghế trong suất chiếu tương lai
        foreach (var sts in futureShowtimeSeats)
        {
            // Nếu trạng thái của ghế đang là Trống
            if (sts.SeatStatus == DomainConstants.EntityStatus.Available)
            {
                // Cập nhật trạng thái ghế thành Bảo trì
                sts.SeatStatus = DomainConstants.EntityStatus.Maintenance;
            }
        }

        // Tìm thông tin phòng chiếu mà ghế này trực thuộc
        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomId == seat.RoomId, cancellationToken);
        // Nếu tìm thấy phòng chiếu
        if (room != null)
        {
            // Đặt trạng thái phòng chiếu thành đang Bảo trì
            room.RoomStatus = DomainConstants.RoomStatus.Maintenance;
            // Tìm các suất chiếu đang mở trong phòng chiếu này
            var openShowtimes = await _dbContext.Showtimes
                .Where(s => s.RoomId == room.RoomId && s.Status == DomainConstants.EntityStatus.Open)
                .ToListAsync(cancellationToken);
            
            // Duyệt qua từng suất chiếu đang mở đó
            foreach (var st in openShowtimes)
            {
                // Cập nhật trạng thái suất chiếu thành Tạm ngưng
                st.Status = DomainConstants.ShowtimeStatus.Suspended;
            }
        }

        // Vô hiệu hóa ghế (đánh dấu IsActive = false) thay vì xóa vật lý
        seat.IsActive = false;
        // Lưu thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả xóa ghế thành công
        return ServiceResult<bool>.Ok(true, "Seat deleted successfully.");
    }

    // Approval workflow removed - direct CRUD methods are used instead

    // pending request APIs removed

    // Phương thức khóa ghế (Dành cho chức năng đặt vé)
    public async Task<ServiceResult<LockSeatResponse>> LockSeatAsync(
        LockSeatRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        // Kiểm tra xem ID người dùng có hợp lệ không
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Trả về lỗi thiếu thông tin người dùng
            return ServiceResult<LockSeatResponse>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        // Lấy thông tin chi tiết ghế trong một suất chiếu cụ thể
        var showtimeSeat = await _dbContext.ShowtimeSeats
            // Nạp thông tin đặt vé của ghế đó
            .Include(item => item.BookingSeat)
            // Tìm ghế theo mã suất chiếu và mã ghế
            .FirstOrDefaultAsync(
                item =>
                    item.ShowtimeId == request.ShowtimeId
                    && item.SeatId == request.SeatId,
                cancellationToken);
        // Nếu không tìm thấy thông tin ghế trong suất chiếu này
        if (showtimeSeat == null)
        {
            // Trả về lỗi không tìm thấy ghế trong suất chiếu
            return ServiceResult<LockSeatResponse>.Fail(
                404,
                "Showtime seat not found.",
                "SHOWTIME_SEAT_NOT_FOUND");
        }

        // Lấy thời điểm hiện tại theo chuẩn UTC
        var now = DateTime.UtcNow;
        // Nếu ghế này đã được liên kết với một đơn đặt vé hoặc đã ở trạng thái Đã bán
        if (showtimeSeat.BookingSeat != null || showtimeSeat.SeatStatus == SeatBooked)
        {
            // Trả về lỗi ghế đã bị bán
            return ServiceResult<LockSeatResponse>.Fail(
                409,
                "Seat has already been sold.",
                "SEAT_SOLD");
        }

        // Nếu ghế đang bị khóa và thời hạn khóa vẫn còn hiệu lực
        if (showtimeSeat.SeatStatus == SeatLocked
            && showtimeSeat.LockedUntil.HasValue
            && showtimeSeat.LockedUntil.Value > now)
        {
            // Trả về lỗi ghế đang tạm thời bị khóa
            return ServiceResult<LockSeatResponse>.Fail(
                409,
                "Seat is temporarily locked.",
                "SEAT_LOCKED");
        }

        // Tính thời gian hết hạn khóa ghế (đọc từ cấu hình)
        var seatLockTtl = TimeSpan.FromMinutes(_bookingSettings.PendingPaymentExpiryMinutes);
        // Tính toán thời điểm kết thúc khóa
        var lockedUntil = now.Add(seatLockTtl);
        // Tạo khóa định danh ghế để lưu trữ khóa trên cache/store
        var lockKey = BuildSeatLockKey(request.ShowtimeId, request.SeatId);
        // Thử thực hiện khóa ghế trên bộ đệm bộ nhớ (Memory/Redis)
        var locked = await _seatLockStore.TryLockAsync(
            lockKey,
            userId,
            seatLockTtl,
            cancellationToken);
        // Nếu việc đặt khóa trên cache thất bại
        if (!locked)
        {
            // Trả về lỗi ghế đang tạm thời bị khóa
            return ServiceResult<LockSeatResponse>.Fail(
                409,
                "Seat is temporarily locked.",
                "SEAT_LOCKED");
        }

        try
        {
            // Cập nhật trạng thái ghế trong cơ sở dữ liệu thành Đang khóa
            showtimeSeat.SeatStatus = SeatLocked;
            // Cập nhật thời điểm kết thúc khóa
            showtimeSeat.LockedUntil = lockedUntil;
            // Cập nhật mã người dùng đã khóa ghế
            showtimeSeat.LockedByUserId = userId;

            // Lưu thay đổi vào cơ sở dữ liệu
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Nếu có ngoại lệ trong quá trình lưu, giải phóng khóa khỏi cache
            await _seatLockStore.ReleaseAsync(lockKey, cancellationToken);
            // Ném lại ngoại lệ để hệ thống xử lý chung
            throw;
        }

        // Trả về kết quả khóa ghế thành công với thông tin chi tiết
        return ServiceResult<LockSeatResponse>.Ok(
            new LockSeatResponse
            {
                // Gán mã ghế trong suất chiếu
                ShowtimeSeatId = showtimeSeat.ShowtimeSeatId,
                // Gán mã suất chiếu
                ShowtimeId = showtimeSeat.ShowtimeId,
                // Gán mã ghế
                SeatId = showtimeSeat.SeatId,
                // Gán trạng thái hiện tại của ghế
                SeatStatus = showtimeSeat.SeatStatus,
                // Gán thời điểm hết hạn khóa
                LockedUntil = lockedUntil
            },
            $"Seat locked for {_bookingSettings.PendingPaymentExpiryMinutes} minutes.");
    }

    // Phương thức mở khóa ghế
    public async Task<ServiceResult<UnlockSeatResponse>> UnlockSeatAsync(
        UnlockSeatRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        // Kiểm tra xem ID người dùng có hợp lệ không
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Trả về lỗi thiếu thông tin người dùng
            return ServiceResult<UnlockSeatResponse>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        // Lấy thông tin ghế trong suất chiếu
        var showtimeSeat = await _dbContext.ShowtimeSeats
            // Nạp thông tin vé đặt kèm ghế
            .Include(item => item.BookingSeat)
            // Tìm theo mã suất chiếu và mã ghế
            .FirstOrDefaultAsync(
                item =>
                    item.ShowtimeId == request.ShowtimeId
                    && item.SeatId == request.SeatId,
                cancellationToken);
        // Nếu không tìm thấy
        if (showtimeSeat == null)
        {
            // Trả về lỗi không tìm thấy ghế trong suất chiếu
            return ServiceResult<UnlockSeatResponse>.Fail(
                404,
                "Showtime seat not found.",
                "SHOWTIME_SEAT_NOT_FOUND");
        }

        // Nếu ghế đã liên kết với đơn đặt vé hoặc đã ở trạng thái Đã bán
        if (showtimeSeat.BookingSeat != null || showtimeSeat.SeatStatus == SeatBooked)
        {
            // Trả về lỗi ghế đã bán, không thể mở khóa
            return ServiceResult<UnlockSeatResponse>.Fail(
                409,
                "Seat has already been sold.",
                "SEAT_SOLD");
        }

        // Lấy thời điểm hiện tại theo chuẩn UTC
        var now = DateTime.UtcNow;
        // Nếu ghế không bị khóa, hoặc không có thời hạn khóa, hoặc thời hạn khóa đã hết
        if (showtimeSeat.SeatStatus != SeatLocked
            || !showtimeSeat.LockedUntil.HasValue
            || showtimeSeat.LockedUntil.Value <= now)
        {
            // Cập nhật trạng thái ghế về lại Trống
            showtimeSeat.SeatStatus = SeatAvailable;
            // Xóa thời gian khóa
            showtimeSeat.LockedUntil = null;
            // Xóa mã người dùng đang giữ khóa
            showtimeSeat.LockedByUserId = null;

            // Xóa khóa lưu trên hệ thống cache
            await _seatLockStore.ReleaseAsync(
                BuildSeatLockKey(showtimeSeat.ShowtimeId, showtimeSeat.SeatId),
                cancellationToken);
            // Lưu các thay đổi vào cơ sở dữ liệu
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Trả về thông báo khóa ghế đã hết hạn và được xử lý
            return ServiceResult<UnlockSeatResponse>.Ok(
                ToUnlockSeatResponse(showtimeSeat),
                "Seat lock has already expired.");
        }

        // Nếu người đang cố mở khóa không phải là người đã khóa ghế
        if (!string.Equals(showtimeSeat.LockedByUserId, userId, StringComparison.Ordinal))
        {
            // Trả về lỗi không có quyền do ghế bị khóa bởi người khác
            return ServiceResult<UnlockSeatResponse>.Fail(
                403,
                "Seat is locked by another user.",
                "SEAT_LOCKED_BY_ANOTHER_USER");
        }

        // Cập nhật trạng thái ghế về lại Trống
        showtimeSeat.SeatStatus = SeatAvailable;
        // Hủy bỏ thời gian khóa
        showtimeSeat.LockedUntil = null;
        // Hủy bỏ thông hiện tại người giữ khóa
        showtimeSeat.LockedByUserId = null;

        // Xóa khóa trên hệ thống cache
        await _seatLockStore.ReleaseAsync(
            BuildSeatLockKey(showtimeSeat.ShowtimeId, showtimeSeat.SeatId),
            cancellationToken);
        // Lưu thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả mở khóa ghế thành công
        return ServiceResult<UnlockSeatResponse>.Ok(
            ToUnlockSeatResponse(showtimeSeat),
            "Seat lock released successfully.");
    }

    // Phương thức lấy sơ đồ ghế cho một suất chiếu
    public async Task<ServiceResult<SeatMapResponse>> GetSeatMapAsync(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        // Truy vấn thông tin suất chiếu từ cơ sở dữ liệu
        var showtime = await _dbContext.Showtimes
            // Không theo dõi thay đổi
            .AsNoTracking()
            // Tìm theo mã suất chiếu
            .FirstOrDefaultAsync(item => item.ShowtimeId == showtimeId, cancellationToken);
        // Nếu không tìm thấy suất chiếu
        if (showtime == null)
        {
            // Trả về lỗi suất chiếu không tồn tại
            return ServiceResult<SeatMapResponse>.Fail(
                404,
                "Showtime not found.",
                "SHOWTIME_NOT_FOUND");
        }

        // Chạy hàm giải phóng các ghế có khóa đã hết hạn
        await ReleaseExpiredLocksAsync(showtimeId, cancellationToken);

        // Lấy danh sách toàn bộ ghế thuộc suất chiếu
        var seats = await _dbContext.ShowtimeSeats
            // Không theo dõi thay đổi
            .AsNoTracking()
            // Nạp thông tin vé của từng ghế
            .Include(item => item.BookingSeat)
            // Nạp thông tin chi tiết về ghế
            .Include(item => item.Seat)
                // Nạp chi tiết về loại ghế
                .ThenInclude(item => item.SeatType)
            // Lọc các ghế thuộc đúng mã suất chiếu
            .Where(item => item.ShowtimeId == showtimeId)
            // Sắp xếp theo hàng (RowLabel)
            .OrderBy(item => item.Seat.RowLabel)
            // Tiếp tục sắp xếp theo số ghế (SeatNumber)
            .ThenBy(item => item.Seat.SeatNumber)
            // Đưa kết quả vào danh sách bất đồng bộ
            .ToListAsync(cancellationToken);

        // Khởi tạo thời điểm hiện tại theo chuẩn UTC
        var now = DateTime.UtcNow;
        // Khởi tạo danh sách chứa các ghế đang trống
        var availableSeats = new List<SeatMapItemResponse>();
        // Khởi tạo danh sách chứa các ghế đang bị khóa (có người đang đặt)
        var lockedSeats = new List<SeatMapItemResponse>();
        // Khởi tạo danh sách chứa các ghế đã được bán
        var soldSeats = new List<SeatMapItemResponse>();

        // Phân loại từng ghế trong danh sách
        foreach (var showtimeSeat in seats)
        {
            // Nếu ghế đã có thông tin đặt vé hoặc trạng thái là Đã bán
            if (showtimeSeat.BookingSeat != null || showtimeSeat.SeatStatus == SeatBooked)
            {
                // Thêm vào danh sách ghế đã bán
                soldSeats.Add(ToSeatMapItem(showtimeSeat, SeatBooked, showtime.BasePrice));
                // Bỏ qua các bước sau, chuyển sang ghế tiếp theo
                continue;
            }

            // Nếu ghế có trạng thái Đang khóa và hạn khóa vẫn trong tương lai
            if (showtimeSeat.SeatStatus == SeatLocked
                && showtimeSeat.LockedUntil.HasValue
                && showtimeSeat.LockedUntil.Value > now)
            {
                // Thêm vào danh sách ghế đang khóa
                lockedSeats.Add(ToSeatMapItem(showtimeSeat, SeatLocked, showtime.BasePrice));
                // Bỏ qua các bước sau
                continue;
            }

            // Nếu không thuộc 2 trường hợp trên, ghế được coi là đang Trống
            availableSeats.Add(ToSeatMapItem(showtimeSeat, SeatAvailable, showtime.BasePrice));
        }

        // Trả về dữ liệu toàn bộ sơ đồ ghế
        return ServiceResult<SeatMapResponse>.Ok(
            new SeatMapResponse
            {
                // Gán mã suất chiếu
                ShowtimeId = showtimeId,
                // Danh sách ghế trống
                AvailableSeats = availableSeats,
                // Danh sách ghế đang giữ chỗ
                LockedSeats = lockedSeats,
                // Danh sách ghế đã bán
                SoldSeats = soldSeats
            },
            "Seat map retrieved successfully.");
    }

    // helper methods for pending request workflow removed

    // Phương thức áp dụng việc tạo ghế (Thực thi chuỗi JSON trực tiếp)
    public async Task<ServiceResult<SeatResponse>> ApplyCreateAsync(
        string requestData,
        CancellationToken cancellationToken)
    {
        CreateSeatRequest? createRequest;
        try
        {
            // Giải mã chuỗi JSON đầu vào thành đối tượng CreateSeatRequest
            createRequest = JsonSerializer.Deserialize<CreateSeatRequest>(requestData);
        }
        catch (System.Text.Json.JsonException)
        {
            // Bắt lỗi giải mã JSON và trả về lỗi
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        // Nếu kết quả giải mã bị null
        if (createRequest == null)
        {
            // Trả về lỗi dữ liệu đầu vào
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        // Xây dựng mã hiển thị của ghế
        var seatCode = BuildSeatCode(createRequest.RowLabel, createRequest.SeatNumber);
        // Kiểm tra xem ghế đã tồn tại trong phòng chiếu đó chưa
        var seatExists = await _dbContext.Seats
            .AnyAsync(
                seat => seat.RoomId == createRequest.RoomId && seat.SeatCode == seatCode,
                cancellationToken);
        // Nếu ghế đã tồn tại
        if (seatExists)
        {
            // Trả về lỗi trùng mã ghế
            return ServiceResult<SeatResponse>.Fail(
                409,
                "Seat already exists.",
                "SEAT_ALREADY_EXISTS");
        }

        // Tạo đối tượng ghế mới
        var seat = new Seat
        {
            // Gán mã ID mới
            SeatId = Guid.NewGuid().ToString("N"),
            // Gán mã phòng chiếu
            RoomId = createRequest.RoomId,
            // Gán ký hiệu hàng ghế
            RowLabel = createRequest.RowLabel,
            // Gán số ghế
            SeatNumber = createRequest.SeatNumber,
            // Gán mã hiển thị của ghế
            SeatCode = seatCode,
            // Gán loại ghế
            SeatTypeId = createRequest.SeatTypeId,
            // Đặt trạng thái hoạt động mặc định
            IsActive = true
        };

        // Thêm đối tượng ghế vào DbContext
        _dbContext.Seats.Add(seat);
        // Lưu dữ liệu vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về phản hồi tạo ghế thành công
        return ServiceResult<SeatResponse>.Ok(
            ToSeatResponse(seat),
            "Seat created successfully.");
    }

    // Phương thức áp dụng cập nhật ghế từ chuỗi JSON
    public async Task<ServiceResult<SeatResponse>> ApplyUpdateAsync(
        string requestData,
        CancellationToken cancellationToken)
    {
        UpdateSeatRequest? updateRequest;
        try
        {
            // Giải mã chuỗi dữ liệu JSON
            updateRequest = JsonSerializer.Deserialize<UpdateSeatRequest>(requestData);
        }
        catch (System.Text.Json.JsonException)
        {
            // Bắt lỗi giải mã JSON
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        // Kiểm tra xem request giải mã xong có null không
        if (updateRequest == null)
        {
            // Báo lỗi dữ liệu không hợp lệ
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        // Tìm kiếm ghế theo ID
        var seat = await _dbContext.Seats
            .FirstOrDefaultAsync(item => item.SeatId == updateRequest.SeatId, cancellationToken);
        // Nếu ghế không tồn tại
        if (seat == null)
        {
            // Trả về lỗi không tìm thấy ghế
            return ServiceResult<SeatResponse>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        // Xây dựng lại mã ghế mới
        var newSeatCode = BuildSeatCode(updateRequest.RowLabel, updateRequest.SeatNumber);
        // Kiểm tra xem mã ghế mới có bị trùng với ghế nào khác trong cùng phòng chiếu không
        var duplicatedSeat = await _dbContext.Seats
            .AnyAsync(
                item =>
                    item.RoomId == seat.RoomId
                    && item.SeatCode == newSeatCode
                    && item.SeatId != seat.SeatId,
                cancellationToken);
        // Nếu có trùng lặp
        if (duplicatedSeat)
        {
            // Báo lỗi mã ghế đã bị sử dụng
            return ServiceResult<SeatResponse>.Fail(
                409,
                "Seat code already exists in this room.",
                "DUPLICATE_SEAT");
        }

        // Cập nhật lại nhãn hàng ghế
        seat.RowLabel = updateRequest.RowLabel;
        // Cập nhật lại số thứ tự ghế
        seat.SeatNumber = updateRequest.SeatNumber;
        // Cập nhật mã ghế hiển thị
        seat.SeatCode = newSeatCode;
        // Cập nhật lại loại ghế
        seat.SeatTypeId = updateRequest.SeatTypeId;

        // Lưu thay đổi cập nhật vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về phản hồi ghế đã cập nhật thành công
        return ServiceResult<SeatResponse>.Ok(
            ToSeatResponse(seat),
            "Seat updated successfully.");
    }

    // Phương thức áp dụng xóa ghế
    public async Task<ServiceResult<SeatResponse>> ApplyDeleteAsync(
        string? seatId,
        CancellationToken cancellationToken)
    {
        // Kiểm tra mã ghế truyền vào có trống hay không
        if (string.IsNullOrWhiteSpace(seatId))
        {
            // Báo lỗi dữ liệu không hợp lệ
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        // Tìm kiếm ghế trong database theo ID
        var seat = await _dbContext.Seats
            .FirstOrDefaultAsync(item => item.SeatId == seatId, cancellationToken);
        // Nếu không tìm thấy
        if (seat == null)
        {
            // Trả về lỗi không tìm thấy ghế
            return ServiceResult<SeatResponse>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        // Kiểm tra xem có bất kỳ suất chiếu tương lai nào đang dùng ghế này không
        var hasFutureShowtime = await _dbContext.ShowtimeSeats
            .AnyAsync(
                item =>
                    item.SeatId == seatId
                    && item.Showtime.Status == DomainConstants.ShowtimeStatus.Open
                    && item.Showtime.StartTime > DateTime.UtcNow,
                cancellationToken);
        // Nếu có suất chiếu tương lai sử dụng ghế này
        if (hasFutureShowtime)
        {
            // Báo lỗi xung đột vì ghế đang hoạt động trong suất chiếu
            return ServiceResult<SeatResponse>.Fail(
                409,
                "Seat is being used by future showtimes.",
                "SEAT_IN_ACTIVE_SHOWTIME");
        }

        // Vô hiệu hóa hoạt động của ghế (Soft delete)
        seat.IsActive = false;
        // Lưu thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả xóa ghế thành công
        return ServiceResult<SeatResponse>.Ok(
            ToSeatResponse(seat),
            "Seat deleted successfully.");
    }

    // Hàm riêng hỗ trợ giải phóng các ghế đã hết hạn khóa
    private async Task ReleaseExpiredLocksAsync(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        // Lấy thời điểm hiện tại chuẩn UTC
        var now = DateTime.UtcNow;
        // Tìm các ghế thuộc suất chiếu đang bị khóa và thời hạn khóa đã ở trong quá khứ
        var expiredLocks = await _dbContext.ShowtimeSeats
            .Where(
                item =>
                    item.ShowtimeId == showtimeId
                    && item.SeatStatus == SeatLocked
                    && item.LockedUntil <= now)
            .ToListAsync(cancellationToken);

        // Nếu không có ghế nào hết hạn khóa
        if (expiredLocks.Count == 0)
        {
            // Thoát khỏi hàm
            return;
        }

        // Duyệt qua từng ghế đã hết hạn
        foreach (var expiredLock in expiredLocks)
        {
            // Đặt lại trạng thái của ghế thành Trống
            expiredLock.SeatStatus = SeatAvailable;
            // Xóa thời gian khóa tạm
            expiredLock.LockedUntil = null;
            // Xóa thông tin người giữ khóa
            expiredLock.LockedByUserId = null;
            // Xóa khóa trên bộ đệm bộ nhớ Cache
            await _seatLockStore.ReleaseAsync(
                BuildSeatLockKey(expiredLock.ShowtimeId, expiredLock.SeatId),
                cancellationToken);
        }

        // Cập nhật tất cả vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Hàm chuyển đổi từ Entity Ghế sang DTO phản hồi API
    private static SeatResponse ToSeatResponse(Seat seat)
    {
        // Khởi tạo và trả về đối tượng phản hồi SeatResponse
        return new SeatResponse
        {
            // Ánh xạ ID ghế
            SeatId = seat.SeatId,
            // Ánh xạ ID phòng chiếu
            RoomId = seat.RoomId,
            // Ánh xạ ký hiệu hàng
            RowLabel = seat.RowLabel,
            // Ánh xạ số thứ tự ghế
            SeatNumber = seat.SeatNumber,
            // Ánh xạ mã hiển thị
            SeatCode = seat.SeatCode,
            // Ánh xạ loại ghế
            SeatTypeId = seat.SeatTypeId,
            // Ánh xạ trạng thái hoạt động
            IsActive = seat.IsActive
        };
    }

    // Hàm chuyển đổi thành phần tử bản đồ ghế
    private static SeatMapItemResponse ToSeatMapItem(
        ShowtimeSeat showtimeSeat,
        string status,
        decimal basePrice)
    {
        // Khởi tạo và trả về dữ liệu 1 ghế trên sơ đồ
        return new SeatMapItemResponse
        {
            // ID của ghế trong suất chiếu cụ thể đó
            ShowtimeSeatId = showtimeSeat.ShowtimeSeatId,
            // ID thực tế của ghế
            SeatId = showtimeSeat.SeatId,
            // Ký hiệu hàng lấy từ bảng ghế
            RowLabel = showtimeSeat.Seat.RowLabel,
            // Số thứ tự ghế
            SeatNumber = showtimeSeat.Seat.SeatNumber,
            // Mã hiển thị ghế
            SeatCode = showtimeSeat.Seat.SeatCode,
            // Loại ghế
            SeatTypeId = showtimeSeat.Seat.SeatTypeId,
            // Giá tính theo giá sàn cộng thêm phí phụ thu của loại ghế
            Price = basePrice + showtimeSeat.Seat.SeatType.ExtraFee,
            // Trạng thái ghế trên bản đồ (Trống, Đang giữ, Đã bán)
            SeatStatus = status,
            // Thời hạn khóa (nếu có)
            LockedUntil = showtimeSeat.LockedUntil
        };
    }

    // Hàm chuyển đổi kết quả mở khóa ghế
    private static UnlockSeatResponse ToUnlockSeatResponse(ShowtimeSeat showtimeSeat)
    {
        // Khởi tạo và trả về DTO
        return new UnlockSeatResponse
        {
            // Mã ghế của suất chiếu
            ShowtimeSeatId = showtimeSeat.ShowtimeSeatId,
            // Mã suất chiếu
            ShowtimeId = showtimeSeat.ShowtimeId,
            // Mã ghế
            SeatId = showtimeSeat.SeatId,
            // Trạng thái ghế sau khi mở khóa
            SeatStatus = showtimeSeat.SeatStatus
        };
    }

    // Hàm xây dựng chuỗi mã hiển thị ghế (VD: A1, B2)
    private static string BuildSeatCode(string rowLabel, int seatNumber)
    {
        // Cắt bỏ khoảng trắng 2 đầu ký hiệu hàng, viết hoa tất cả và ghép cùng số ghế
        return $"{rowLabel.Trim().ToUpperInvariant()}{seatNumber}";
    }

    // Hàm tạo chuỗi khóa cache cho thao tác giữ chỗ
    private static string BuildSeatLockKey(string showtimeId, string seatId)
    {
        // Trả về chuỗi khóa kết hợp từ tiền tố, mã suất chiếu và mã ghế
        return $"seat-lock:{showtimeId}:{seatId}";
    }

    // Record nội bộ dùng để map cấu trúc dữ liệu cho yêu cầu xóa
    private sealed record DeleteSeatRequestData(string SeatId);
}
