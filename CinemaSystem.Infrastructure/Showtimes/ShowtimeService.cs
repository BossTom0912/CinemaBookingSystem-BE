    using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Events;
using CinemaSystem.Infrastructure.Persistence;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CinemaSystem.Infrastructure.Showtimes;

public sealed class ShowtimeService : IShowtimeService
{
    // Tập hợp các trạng thái suất chiếu hợp lệ (Không phân biệt hoa thường)
    private static readonly HashSet<string> ValidShowtimeStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Trạng thái mở bán
        DomainConstants.ShowtimeStatus.Open,
        // Trạng thái đóng không bán vé nữa
        DomainConstants.ShowtimeStatus.Closed,
        // Trạng thái đã hủy
        DomainConstants.ShowtimeStatus.Cancelled,
        // Trạng thái đã hoàn thành (chiếu xong)
        DomainConstants.ShowtimeStatus.Completed,
        // Trạng thái tạm hoãn / tạm ngưng
        DomainConstants.ShowtimeStatus.Suspended,
        // Trạng thái đang xử lý không ổn định (ví dụ đang đổi rạp/giờ)
        DomainConstants.ShowtimeStatus.ProcessingUnstable
    };

    // Khai báo biến DbContext để tương tác với cơ sở dữ liệu
    private readonly CinemaDbContext _dbContext;
    // Khai báo biến IClock để lấy thời gian hiện tại
    private readonly IClock _clock;
    // Khai báo biến cài đặt xử lý chung của hệ thống rạp
    private readonly CinemaProcessingSettings _settings;
    // Khai báo biến client của Hangfire để chạy các job nền
    private readonly IBackgroundJobClient _backgroundJobClient;
    // Khai báo biến để truy cập HttpContext hiện tại (lấy thông tin user đăng nhập)
    private readonly IHttpContextAccessor _httpContextAccessor;
    // Khai báo biến chứa các cài đặt về bảo mật
    private readonly CinemaSystem.Application.Settings.SecuritySettings _securitySettings;
    // Khai báo biến chứa cấu hình mẫu email gửi đi
    private readonly CinemaSystem.Application.Settings.EmailTemplatesSettings _emailTemplates;
    // Khai báo biến dịch vụ AI viết thư xin lỗi
    private readonly IAiEmailService _aiEmailService;
    public ShowtimeService(
        CinemaDbContext dbContext,
        IClock clock,
        IOptions<CinemaProcessingSettings>? options = null,
        IOptions<CinemaSystem.Application.Settings.SecuritySettings>? securityOptions = null,
        IOptions<CinemaSystem.Application.Settings.EmailTemplatesSettings>? emailTemplatesOptions = null,
        IBackgroundJobClient? backgroundJobClient = null,
        IHttpContextAccessor? httpContextAccessor = null,
        IAiEmailService? aiEmailService = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _settings = options?.Value ?? new CinemaProcessingSettings();
        _securitySettings = securityOptions?.Value ?? new CinemaSystem.Application.Settings.SecuritySettings();
        _emailTemplates = emailTemplatesOptions?.Value ?? new CinemaSystem.Application.Settings.EmailTemplatesSettings();
        _backgroundJobClient = backgroundJobClient!;
        _httpContextAccessor = httpContextAccessor!;
        _aiEmailService = aiEmailService!;
    }

    // Phương thức lấy danh sách tất cả các suất chiếu
    public async Task<ServiceResult<IReadOnlyList<ShowtimeResponse>>> GetShowtimesAsync(
        CancellationToken cancellationToken)
    {
        var nowThreshold = DateTime.UtcNow.AddDays(-1);
        var showtimes = await _dbContext.Showtimes
            .AsNoTracking()
            .Where(item => item.EndTime >= nowThreshold)
            .OrderBy(item => item.StartTime)
            // Ánh xạ sang đối tượng ShowtimeResponse (DTO trả về)
            .Select(item => new ShowtimeResponse
            {
                // Gán ID suất chiếu
                ShowtimeId = item.ShowtimeId,
                // Gán ID bộ phim
                MovieId = item.MovieId,
                // Gán tên bộ phim thông qua điều hướng Movie
                MovieTitle = item.Movie.Title,
                // Gán ID phòng chiếu
                RoomId = item.RoomId,
                // Gán tên phòng chiếu thông qua điều hướng Room
                RoomName = item.Room.RoomName,
                // Gán ID rạp thông qua điều hướng Room -> Cinema
                CinemaId = item.Room.CinemaId,
                // Gán tên rạp thông qua điều hướng Room -> Cinema
                CinemaName = item.Room.Cinema.CinemaName,
                // Gán thời gian bắt đầu
                StartTime = item.StartTime,
                // Gán thời gian kết thúc
                EndTime = item.EndTime,
                // Gán giá vé cơ bản của suất chiếu
                BasePrice = item.BasePrice,
                // Gán trạng thái suất chiếu
                Status = item.Status,
                // Gán số lượng ghế của suất chiếu (bằng tổng số ghế trong collection)
                ShowtimeSeatCount = item.ShowtimeSeats.Count,
                HasBookings = item.Bookings.Any(b => b.BookingStatus != DomainConstants.BookingStatus.Cancelled) || item.ShowtimeSeats.Any(sts => sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Booked || sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Locked || sts.SeatStatus == "BOOKED" || sts.SeatStatus == "Booked")
            })
            // Chuyển kết quả truy vấn thành một List bất đồng bộ
            .ToListAsync(cancellationToken);

        // Trả về kết quả thành công với danh sách suất chiếu
        return ServiceResult<IReadOnlyList<ShowtimeResponse>>.Ok(
            showtimes,
            "Showtimes retrieved successfully.");
    }

    public async Task<ServiceResult<IReadOnlyList<ShowtimeResponse>>> GetShowtimesByCinemaAsync(
        string? cinemaId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Showtimes
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(cinemaId))
        {
            query = query.Where(item => item.Room.CinemaId == cinemaId);
        }

        var showtimes = await query
            .OrderBy(item => item.StartTime)
            .Select(item => new ShowtimeResponse
            {
                ShowtimeId = item.ShowtimeId,
                MovieId = item.MovieId,
                MovieTitle = item.Movie.Title,
                RoomId = item.RoomId,
                RoomName = item.Room.RoomName,
                CinemaId = item.Room.CinemaId,
                CinemaName = item.Room.Cinema.CinemaName,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                BasePrice = item.BasePrice,
                Status = item.Status,
                ShowtimeSeatCount = item.ShowtimeSeats.Count,
                HasBookings = item.Bookings.Any(b => b.BookingStatus != DomainConstants.BookingStatus.Cancelled) || item.ShowtimeSeats.Any(sts => sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Booked || sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Locked || sts.SeatStatus == "BOOKED" || sts.SeatStatus == "Booked")
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<ShowtimeResponse>>.Ok(
            showtimes,
            "Scoped showtimes retrieved successfully.");
    }

    // Phương thức lấy chi tiết một suất chiếu theo ID
    public async Task<ServiceResult<ShowtimeResponse>> GetShowtimeByIdAsync(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        // Truy vấn bảng Showtimes
        var showtime = await _dbContext.Showtimes
            // Không tracking để tối ưu hiệu suất đọc
            .AsNoTracking()
            // Lọc suất chiếu có ID trùng khớp
            .Where(item => item.ShowtimeId == showtimeId)
            // Ánh xạ đối tượng sang DTO trả về
            .Select(item => new ShowtimeResponse
            {
                // Gán ID suất chiếu
                ShowtimeId = item.ShowtimeId,
                // Gán ID phim
                MovieId = item.MovieId,
                // Lấy tên phim
                MovieTitle = item.Movie.Title,
                // Gán ID phòng
                RoomId = item.RoomId,
                // Lấy tên phòng
                RoomName = item.Room.RoomName,
                // Gán ID rạp
                CinemaId = item.Room.CinemaId,
                // Lấy tên rạp
                CinemaName = item.Room.Cinema.CinemaName,
                // Gán thời gian bắt đầu
                StartTime = item.StartTime,
                // Gán thời gian kết thúc
                EndTime = item.EndTime,
                // Gán giá cơ bản
                BasePrice = item.BasePrice,
                // Gán trạng thái suất chiếu
                Status = item.Status,
                // Đếm số ghế trong suất chiếu
                ShowtimeSeatCount = item.ShowtimeSeats.Count,
                HasBookings = item.Bookings.Any(b => b.BookingStatus != DomainConstants.BookingStatus.Cancelled) || item.ShowtimeSeats.Any(sts => sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Booked || sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Locked || sts.SeatStatus == "BOOKED" || sts.SeatStatus == "Booked")
            })
            // Lấy ra phần tử đầu tiên thỏa mãn hoặc trả về null nếu không có
            .FirstOrDefaultAsync(cancellationToken);
            
        // Kiểm tra nếu không tìm thấy suất chiếu
        if (showtime is null)
        {
            // Trả về kết quả lỗi 404 (Không tìm thấy)
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }

        // Trả về chi tiết suất chiếu thành công
        return ServiceResult<ShowtimeResponse>.Ok(showtime, "Showtime retrieved successfully.");
    }

    // Phương thức tạo mới một suất chiếu
    public async Task<ServiceResult<ShowtimeResponse>> CreateShowtimeAsync(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        // Chuẩn hóa trạng thái truyền vào (Viết hoa, xóa khoảng trắng thừa)
        var status = NormalizeStatus(request.Status);
        
        // Kiểm tra xem trạng thái truyền vào có hợp lệ hay không
        if (!ValidShowtimeStatuses.Contains(status))
        {
            // Trả về lỗi 400 nếu trạng thái không hợp lệ
            return ServiceResult<ShowtimeResponse>.Fail(400, "Showtime status is invalid.", "INVALID_SHOWTIME_STATUS");
        }
        
        // Chuyển đổi thời gian bắt đầu thành UTC nếu chưa phải chuẩn UTC
        var normalizedStartTime = EnsureUtc(request.StartTime);
        
        // Tính toán khoảng cách (phút) từ hiện tại đến lúc bắt đầu suất chiếu
        var minutesUntilShowtime = (normalizedStartTime - _clock.UtcNow).TotalMinutes;
        
        // Nếu khoảng cách này nhỏ hơn thời gian khóa quy định (không được tạo sát giờ)
        if (minutesUntilShowtime < _settings.PreShowtimeBlockingMinutes)
        {
            // Trả về lỗi 400
            return ServiceResult<ShowtimeResponse>.Fail(400, $"Cannot create showtime closer than {_settings.PreShowtimeBlockingMinutes} minutes to start.", "PRE_SHOWTIME_BLOCK");
        }

        // Thực hiện kiểm tra tính hợp lệ của phim, phòng và sự chồng chéo thời gian
        var validation = await ValidateMovieRoomAndOverlapAsync(
            request.MovieId,
            request.RoomId,
            request.StartTime,
            // ID để loại trừ trong kiểm tra chồng chéo (tạo mới nên bằng null)
            excludeShowtimeId: null,
            // Thời gian cũ để so sánh (tạo mới nên bằng null)
            existingStartTime: null,
            cancellationToken);
            
        // Nếu việc kiểm tra thất bại
        if (!validation.Success)
        {
            // Trả về mã lỗi, thông báo lỗi tương ứng
            return ServiceResult<ShowtimeResponse>.Fail(
                validation.StatusCode,
                validation.Message,
                validation.ErrorCode!);
        }

        // Truy vấn lấy danh sách ghế đang hoạt động của phòng chiếu
        var roomActiveSeats = await _dbContext.Seats
            // Lọc theo ID phòng và trạng thái ghế kích hoạt
            .Where(item => item.RoomId == request.RoomId && item.IsActive)
            // Sắp xếp theo tên hàng
            .OrderBy(item => item.RowLabel)
            // Tiếp tục sắp xếp theo số ghế
            .ThenBy(item => item.SeatNumber)
            // Trả về dạng List
            .ToListAsync(cancellationToken);
            
        // Nếu phòng không có ghế nào hoạt động
        if (roomActiveSeats.Count == 0)
        {
            // Báo lỗi 400 vì không có ghế
            return ServiceResult<ShowtimeResponse>.Fail(400, "Room has no active seats.", "ROOM_HAS_NO_SEATS");
        }

        // create showtime immediately
        // Khởi tạo một ID duy nhất cho suất chiếu với tiền tố 'SHW'
        var showtimeId = NewId(DomainConstants.EntityIdPrefix.Showtime);
        
        // Khởi tạo Entity suất chiếu mới
        var showtime = new Showtime
        {
            // Gán ID suất chiếu
            ShowtimeId = showtimeId,
            // Gán ID phim
            MovieId = request.MovieId,
            // Gán ID phòng chiếu
            RoomId = request.RoomId,
            // Gán thời gian bắt đầu
            StartTime = normalizedStartTime,
            // Gán thời gian kết thúc (đã được tính toán qua hàm validate)
            EndTime = validation.EndTime,
            // Gán giá cơ bản
            BasePrice = request.BasePrice,
            // Gán trạng thái
            Status = status,
            // Gán thời gian tạo
            CreatedAt = _clock.UtcNow
        };

        // Truy vấn lại để lấy danh sách ghế hoạt động trong phòng của suất chiếu này (thực ra có thể dùng luôn danh sách roomActiveSeats ở trên)
        var activeSeatsForShowtime = await _dbContext.Seats
            .Where(item => item.RoomId == showtime.RoomId && item.IsActive)
            .ToListAsync(cancellationToken);

        // Tạo ra danh sách các đối tượng ShowtimeSeat bằng cách lặp qua từng ghế kích hoạt và gán ID
        var showtimeSeats = activeSeatsForShowtime.Select(seat => CreateShowtimeSeat(showtime.ShowtimeId, seat.SeatId)).ToList();

        // Thêm suất chiếu mới vào DbSet của Entity Framework
        _dbContext.Showtimes.Add(showtime);
        
        // Thêm đồng loạt các bản ghi ShowtimeSeat vào DbSet
        await _dbContext.ShowtimeSeats.AddRangeAsync(showtimeSeats, cancellationToken);
        
        // Thực thi việc lưu thông tin vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Tải lại bản ghi suất chiếu từ database (kèm các property điều hướng) để tạo dữ liệu Response chuẩn xác
        var created = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        
        // Trả về kết quả thành công với HTTP Status 201 Created
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(created!), "Showtime created successfully.", 201);
    }

    // Phương thức cập nhật thông tin một suất chiếu đã có
    public async Task<ServiceResult<ShowtimeResponse>> UpdateShowtimeAsync(
        string showtimeId,
        UpdateShowtimeRequest request,
        bool force,
        CancellationToken cancellationToken)
    {
        // Tải thông tin suất chiếu từ CSDL kèm theo các thực thể phụ, có bật tracking để cập nhật
        var showtime = await LoadShowtimeAsync(showtimeId, tracking: true, cancellationToken);
        
        // Nếu không tìm thấy suất chiếu
        if (showtime is null)
        {
            // Trả về lỗi Not Found (404)
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }

        // Chuẩn hóa trạng thái mới được yêu cầu (Viết hoa toàn bộ)
        var status = NormalizeStatus(request.Status);
        
        // Kiểm tra xem giá vé cơ bản có hợp lệ (lớn hơn 0) không
        if (request.BasePrice <= 0)
        {
            // Nếu <= 0 thì trả lỗi 400
            return ServiceResult<ShowtimeResponse>.Fail(
                400,
                "Base price must be greater than zero.",
                "INVALID_BASE_PRICE");
        }
        
        // Kiểm tra trạng thái mới có hợp lệ với danh sách các trạng thái cho phép hay không
        if (!ValidShowtimeStatuses.Contains(status))
        {
            // Trả lỗi 400 nếu trạng thái không hợp lệ
            return ServiceResult<ShowtimeResponse>.Fail(400, "Showtime status is invalid.", "INVALID_SHOWTIME_STATUS");
        }

        // Chuyển đổi thời gian sang UTC nếu cần thiết
        var normalizedStartTime = EnsureUtc(request.StartTime);
        
        // Kiểm tra xem phòng chiếu có bị thay đổi không
        var roomChanged = !string.Equals(showtime.RoomId, request.RoomId, StringComparison.Ordinal);
        
        // Kiểm tra xem thời gian chiếu có bị thay đổi không
        var timeChanged = showtime.StartTime != normalizedStartTime;
        
        // Đánh dấu nếu có sự thay đổi thông tin cốt lõi (phòng chiếu hoặc giờ chiếu)
        var coreInfoChanged = roomChanged || timeChanged;

        // 1. Gọi hàm validate để kiểm tra lại phim, phòng và đụng độ khung giờ
        var validation = await ValidateMovieRoomAndOverlapAsync(
            request.MovieId,
            request.RoomId,
            request.StartTime,
            showtime.ShowtimeId,
            showtime.StartTime,
            cancellationToken);
            
        if (!validation.Success)
        {
            return ServiceResult<ShowtimeResponse>.Fail(
                validation.StatusCode,
                validation.Message,
                validation.ErrorCode!);
        }

        // 2. Nếu có sự thay đổi về phòng chiếu (phòng chiếu mới so với phòng chiếu cũ)
        if (roomChanged)
        {
            var hasBookings = await _dbContext.BookingSeats
                .AnyAsync(bs => bs.ShowtimeSeat.ShowtimeId == showtime.ShowtimeId, cancellationToken);

            if (hasBookings)
            {
                return ServiceResult<ShowtimeResponse>.Fail(
                    400,
                    "Không thể thay đổi phòng chiếu của suất chiếu này do đã có ghế được đặt. Vui lòng sử dụng tính năng Đổi phòng chuyên dụng (ChangeRoom) hoặc Hủy suất chiếu.",
                    "SHOWTIME_HAS_BOOKINGS");
            }

            var activeSeats2 = await _dbContext.Seats
                .Where(item => item.RoomId == request.RoomId && item.IsActive)
                .ToListAsync(cancellationToken);
                
            if (activeSeats2.Count == 0)
            {
                return ServiceResult<ShowtimeResponse>.Fail(400, "Room has no active seats.", "ROOM_HAS_NO_SEATS");
            }

            _dbContext.ShowtimeSeats.RemoveRange(showtime.ShowtimeSeats);
            await _dbContext.ShowtimeSeats.AddRangeAsync(activeSeats2.Select(seat => CreateShowtimeSeat(showtime.ShowtimeId, seat.SeatId)), cancellationToken);
        }

        var originalStartTime = showtime.StartTime;
        var oldStartTimeStr = originalStartTime.ToString("HH:mm - dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var oldRoomName = showtime.Room?.RoomName ?? showtime.RoomId;
        
        string newRoomName = request.RoomId;
        if (roomChanged)
        {
            var newRoomObj = await _dbContext.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.RoomId == request.RoomId, cancellationToken);
            if (newRoomObj != null)
            {
                newRoomName = newRoomObj.RoomName;
            }
        }

        // 3. Luôn luôn cập nhật thông tin Suất chiếu (Giờ chiếu mới, Phòng chiếu, Giá vé)
        showtime.MovieId = request.MovieId;
        showtime.RoomId = request.RoomId;
        showtime.StartTime = normalizedStartTime;
        showtime.EndTime = validation.EndTime;
        showtime.BasePrice = request.BasePrice;
        showtime.Status = DomainConstants.EntityStatus.Open;

        // 4. Nếu có thay đổi giờ chiếu VÀ suất chiếu này đã có vé được thanh toán -> Cập nhật trạng thái đơn vé và gửi email đền bù
        if (coreInfoChanged && showtime.Bookings.Any(b => b.BookingStatus == DomainConstants.EntityStatus.Paid))
        {
            var paidBookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.Paid).ToList();
            
            foreach (var booking in paidBookings)
            {
                booking.BookingStatus = DomainConstants.EntityStatus.ProcessingUnstable;
                
                var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
                if (!string.IsNullOrEmpty(customerEmail))
                {
                    var timeDiff = Math.Abs((normalizedStartTime - originalStartTime).TotalMinutes);
                    
                    if (timeChanged && timeDiff >= _settings.ShowtimeMaterialChangeThresholdMinutes)
                    {
                        var secret = _securitySettings.ConfirmationTokenSecret;
                        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
                        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(booking.BookingId));
                        var token = Convert.ToBase64String(hash);
                        var encodedToken = System.Uri.EscapeDataString(token);
                        
                        string subject = _emailTemplates.ShowtimeTimeChangeSubject;
                        var movieTitle = showtime.Movie?.Title ?? "bạn đã đặt";
                        var newTimeStr = normalizedStartTime.ToString("HH:mm - dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        var cutoffTimeStr = normalizedStartTime.AddHours(-2).ToString("HH:mm - dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        var bookingId = booking.BookingId;
                        var customerName = booking.CustomerProfile?.User?.FullName;
                        
                        _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                            ai.SendAiTimeChangeEmailAsync(
                                customerEmail, 
                                subject, 
                                movieTitle,
                                oldStartTimeStr,
                                newTimeStr,
                                cutoffTimeStr,
                                bookingId, 
                                encodedToken, 
                                CancellationToken.None,
                                request.CompensationVoucherCode,
                                request.CompensationNote,
                                request.TargetSeatType,
                                customerName));
                    }
                    else if (roomChanged && !timeChanged)
                    {
                        string subject = "Thông báo điều chỉnh phòng chiếu & Quyền lợi dành cho Quý khách / Showtime Room Update";
                        var movieTitle = showtime.Movie?.Title ?? "bạn đã đặt";
                        var timeStr = showtime.StartTime.ToString("HH:mm - dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        var customerName = booking.CustomerProfile?.User?.FullName;

                        _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                            ai.SendAiRoomChangeEmailAsync(
                                customerEmail, 
                                subject, 
                                movieTitle,
                                oldRoomName,
                                newRoomName,
                                timeStr,
                                booking.BookingId, 
                                CancellationToken.None,
                                request.CompensationVoucherCode,
                                request.CompensationNote,
                                request.TargetSeatType,
                                customerName));
                    }
                    else
                    {
                        string subject = _emailTemplates.ShowtimeTimeChangeNoticeSubject;
                        var movieTitleNotice = showtime.Movie?.Title ?? "bạn đã đặt";
                        var newTimeStrNotice = normalizedStartTime.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                        var customerName = booking.CustomerProfile?.User?.FullName;
                        
                        string updateCompInfo = "";
                        if (!string.IsNullOrWhiteSpace(request.CompensationVoucherCode))
                        {
                            updateCompInfo += $" Mã Voucher đền bù dành riêng cho bạn: [{request.CompensationVoucherCode.Trim()}].";
                        }
                        if (!string.IsNullOrWhiteSpace(request.CompensationNote))
                        {
                            updateCompInfo += $" Quyền lợi đền bù: {request.CompensationNote.Trim()}.";
                        }
                        if (!string.IsNullOrWhiteSpace(request.TargetSeatType))
                        {
                            updateCompInfo += $" Đặc biệt: Đã ưu tiên nâng hạng ghế của bạn lên loại [{request.TargetSeatType.Trim()}] miễn phí!";
                        }

                        _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                            ai.SendAiApologyEmailAsync(
                                customerEmail, 
                                subject, 
                                "Điều chỉnh thông tin suất chiếu", 
                                $"Suất chiếu của phim {movieTitleNotice} đã được điều chỉnh sang giờ mới: {newTimeStrNotice}.{updateCompInfo}", 
                                CancellationToken.None,
                                customerName));
                    }
                }
            }
        }

        // 5. Lưu toàn bộ thay đổi xuống Database
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Tải lại suất chiếu để lấy đủ thông tin phục vụ Response
        var updated = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(updated!), "Showtime updated successfully.", 200);
    }

    // Phương thức chuyên dùng để đổi phòng chiếu và cấu hình lại ghế cho vé đã bán
    public async Task<ServiceResult<ShowtimeResponse>> ChangeRoomAsync(
        string showtimeId,
        ChangeRoomRequest request,
        CancellationToken cancellationToken)
    {
        // Tải suất chiếu lên bao gồm phòng chiếu, phim, các ghế của nó và đơn đặt vé
        var showtime = await _dbContext.Showtimes
            .Include(s => s.Room)
            .Include(s => s.Movie)
            .Include(s => s.ShowtimeSeats)
                .ThenInclude(sts => sts.Seat)
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId, cancellationToken);

        // Nếu không tìm thấy suất chiếu
        if (showtime == null)
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime not found.", "NOT_FOUND");

        // Lưu tên phòng cũ TRƯỚC KHI cập nhật entity sang phòng mới
        var oldRoomName = showtime.Room?.RoomName ?? showtime.RoomId;

        // Lấy thông tin phòng chiếu mới bao gồm cả danh sách ghế (Seats)
        var newRoom = await _dbContext.Rooms
            .Include(r => r.Seats)
            .FirstOrDefaultAsync(r => r.RoomId == request.NewRoomId, cancellationToken);

        if (newRoom == null)
            return ServiceResult<ShowtimeResponse>.Fail(404, "New room not found.", "NOT_FOUND");

        var newRoomName = newRoom.RoomName;

        // Nếu phòng mới không ở trạng thái Active
        if (newRoom.RoomStatus != DomainConstants.EntityStatus.Active)
            // Trả lỗi 400
            return ServiceResult<ShowtimeResponse>.Fail(400, "New room is not active.", "ROOM_INACTIVE");

        // Kiểm tra xem thời gian suất chiếu trong phòng mới có bị trùng hoặc vi phạm thời gian dọn dẹp không
        var overlapValidation = await ValidateMovieRoomAndOverlapAsync(
            showtime.MovieId,
            request.NewRoomId,
            showtime.StartTime,
            excludeShowtimeId: showtime.ShowtimeId,
            existingStartTime: null,
            cancellationToken);

        if (!overlapValidation.Success)
        {
            return ServiceResult<ShowtimeResponse>.Fail(
                overlapValidation.StatusCode,
                overlapValidation.Message,
                overlapValidation.ErrorCode!);
        }

        var activeNewSeats = newRoom.Seats.Where(s => s.IsActive).ToList();
        var seatMapping = request.SeatMapping ?? new Dictionary<string, string>();

        var bookingIds = showtime.Bookings.Select(b => b.BookingId).ToList();
        var bookingSeats = await _dbContext.BookingSeats
            .Where(bs => bookingIds.Contains(bs.BookingId))
            .Include(bs => bs.Booking)
                .ThenInclude(b => b.CustomerProfile)
                    .ThenInclude(cp => cp!.User)
            .Include(bs => bs.ShowtimeSeat)
                .ThenInclude(sts => sts.Seat)
            .ToListAsync(cancellationToken);

        // Lấy danh sách các ghế đã phát sinh đơn hàng / đặt chỗ
        var bookedShowtimeSeats = showtime.ShowtimeSeats
            .Where(sts => sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Booked ||
                          sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Locked ||
                          sts.SeatStatus == "BOOKED" ||
                          sts.SeatStatus == "Booked" ||
                          bookingSeats.Any(bs => bs.ShowtimeSeatId == sts.ShowtimeSeatId))
            .ToList();

        // Nếu tổng số vé đã bán nhiều hơn số lượng ghế hoạt động của phòng mới
        if (activeNewSeats.Count < bookedShowtimeSeats.Count)
        {
            return ServiceResult<ShowtimeResponse>.Fail(
                400,
                $"Phòng chiếu mới ({activeNewSeats.Count} ghế) không đủ sức chứa cho {bookedShowtimeSeats.Count} vé đã đặt.",
                "ROOM_CAPACITY_EXCEEDED");
        }

        // 1. Ánh xạ thông minh đa cấp (Multi-level Fallback Seat Mapping)
        var mappedNewSeatIds = new System.Collections.Generic.HashSet<string>();
        var seatMapResult = new Dictionary<string, string>(); // oldSts.ShowtimeSeatId -> newSeatId

        foreach (var oldSts in bookedShowtimeSeats)
        {
            string? newSeatId = null;

            // Cấp 1: Lấy theo mapping chỉ định thủ công từ request
            if (seatMapping.TryGetValue(oldSts.SeatId, out var mappedId) &&
                activeNewSeats.Any(s => s.SeatId == mappedId && !mappedNewSeatIds.Contains(s.SeatId)))
            {
                newSeatId = mappedId;
            }

            // Cấp 2: Tìm ghế tương đương cùng SeatCode (ví dụ "A1" == "A1", không phân biệt hoa thường)
            if (newSeatId == null && oldSts.Seat != null && !string.IsNullOrWhiteSpace(oldSts.Seat.SeatCode))
            {
                var oldCode = oldSts.Seat.SeatCode.Trim();
                var matchCode = activeNewSeats.FirstOrDefault(s =>
                    !mappedNewSeatIds.Contains(s.SeatId) &&
                    string.Equals(s.SeatCode.Trim(), oldCode, StringComparison.OrdinalIgnoreCase));
                if (matchCode != null)
                {
                    newSeatId = matchCode.SeatId;
                }
            }

            // Cấp 3: Tìm ghế trống cùng loại (VIP/Thường/Đôi) trong phòng mới
            if (newSeatId == null && oldSts.Seat != null)
            {
                var matchType = activeNewSeats.FirstOrDefault(s =>
                    !mappedNewSeatIds.Contains(s.SeatId) &&
                    s.SeatTypeId == oldSts.Seat.SeatTypeId);
                if (matchType != null)
                {
                    newSeatId = matchType.SeatId;
                }
            }

            // Cấp 4: Tự động gán cho bất kỳ ghế trống hoạt động nào còn lại của phòng mới
            if (newSeatId == null)
            {
                var matchAny = activeNewSeats.FirstOrDefault(s => !mappedNewSeatIds.Contains(s.SeatId));
                if (matchAny != null)
                {
                    newSeatId = matchAny.SeatId;
                }
            }

            if (newSeatId == null)
            {
                return ServiceResult<ShowtimeResponse>.Fail(
                    400,
                    $"Không thể ánh xạ ghế {oldSts.Seat?.SeatCode ?? oldSts.SeatId} sang phòng chiếu mới.",
                    "MAPPING_FAILED");
            }

            mappedNewSeatIds.Add(newSeatId);
            seatMapResult[oldSts.ShowtimeSeatId] = newSeatId;
        }

        var affectedBookings = new System.Collections.Generic.HashSet<string>();

        // 2. Cập nhật trực tiếp (In-place Update) SeatId của các ghế đã được bán
        var oldShowtimeSeatsList = showtime.ShowtimeSeats.ToList();
        foreach (var oldSts in oldShowtimeSeatsList)
        {
            if (seatMapResult.TryGetValue(oldSts.ShowtimeSeatId, out var newSeatId))
            {
                var newSeatObj = activeNewSeats.FirstOrDefault(s => s.SeatId == newSeatId);
                if (newSeatObj != null)
                {
                    // Đổi SeatId của ghế sang SeatId phòng chiếu mới (Giữ nguyên PK ShowtimeSeatId -> Không lỗi FK!)
                    oldSts.SeatId = newSeatObj.SeatId;
                }
            }
            else
            {
                // Ghế chưa bán: Xóa an toàn không vi phạm FK
                _dbContext.ShowtimeSeats.Remove(oldSts);
            }
        }

        // 3. Tạo bản ghi ShowtimeSeat mới cho các ghế chưa được map trong phòng mới
        foreach (var newSeat in activeNewSeats)
        {
            if (!mappedNewSeatIds.Contains(newSeat.SeatId))
            {
                var newSts = CreateShowtimeSeat(showtime.ShowtimeId, newSeat.SeatId);
                _dbContext.ShowtimeSeats.Add(newSts);
            }
        }

        // 4. Cập nhật phòng chiếu mới và giữ trạng thái OPEN cho suất chiếu để khách hàng mới xem được
        showtime.RoomId = request.NewRoomId;
        showtime.Status = DomainConstants.EntityStatus.Open;

        // Lưu toàn bộ thay đổi xuống DB
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi email thông báo đổi phòng chiếu & quà đền bù (voucher / nâng hạng ghế) trực tiếp cho khách hàng
        var paidBookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.Paid).ToList();
        
        foreach(var booking in paidBookings)
        {
            var email = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
            
            if (!string.IsNullOrEmpty(email))
            {
                string subject = "Thông báo điều chỉnh phòng chiếu & Quyền lợi dành cho Quý khách / Showtime Room Update";
                var movieTitle = showtime.Movie?.Title ?? "bạn đã đặt";
                var timeStr = showtime.StartTime.ToString("HH:mm - dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                var customerName = booking.CustomerProfile?.User?.FullName;
                
                _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                    ai.SendAiRoomChangeEmailAsync(
                        email, 
                        subject, 
                        movieTitle, 
                        oldRoomName, 
                        newRoomName, 
                        timeStr, 
                        booking.BookingId, 
                        CancellationToken.None,
                        request.CompensationVoucherCode,
                        request.CompensationNote,
                        request.TargetSeatType,
                        customerName));
            }
        }

        // Tải lại dữ liệu suất chiếu hoàn chỉnh
        var updated = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        
        // Trả về kết quả thành công HTTP 200
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(updated!), "Room changed successfully.", 200);
    }

    // Phương thức xóa một suất chiếu
    public async Task<ServiceResult<object>> DeleteShowtimeAsync(string showtimeId, CancellationToken cancellationToken)
    {
        // Truy vấn suất chiếu kèm theo thông tin Booking, Payment, Customer, Seat và lịch sử Hủy (ShowtimeCancellation)
        var existing = await _dbContext.Showtimes
            // Bao gồm danh sách Booking
            .Include(s => s.Bookings)
                // Từ Booking lấy luôn Payment
                .ThenInclude(b => b.Payments)
            // Bao gồm danh sách Booking nhánh 2
            .Include(s => s.Bookings)
                // Lấy Customer Profile
                .ThenInclude(b => b.CustomerProfile)
                    // Lấy User profile
                    .ThenInclude(cp => cp!.User)
            // Bao gồm ShowtimeSeats
            .Include(s => s.ShowtimeSeats)
            // Bao gồm lịch sử Hủy (nếu có)
            .Include(s => s.ShowtimeCancellation)
                // Lấy luôn danh sách Hoàn tiền (Refunds) thuộc về lệnh Hủy đó
                .ThenInclude(sc => sc!.Refunds)
            // Phân tách thành nhiều truy vấn rời rạc (tránh cartesian explosion, tối ưu hóa bộ nhớ)
            .AsSplitQuery()
            // Lấy bản ghi đầu tiên khớp với ID
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId, cancellationToken);
            
        // Nếu không tìm thấy
        if (existing is null)
        {
            // Trả lỗi 404
            return ServiceResult<object>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }
        
        // Nếu suất chiếu đã hoàn tất, hoặc giờ bắt đầu đã ở trong quá khứ so với thời điểm hiện tại
        if (existing.Status == DomainConstants.EntityStatus.Completed || existing.StartTime < _clock.UtcNow)
        {
            // Không cho phép xóa, trả lỗi 409 Xung đột
            return ServiceResult<object>.Fail(409, "Cannot cancel a showtime that has already been completed or is in the past.", "PAST_SHOWTIME");
        }

        // Nếu suất chiếu này đã có Booking
        if (existing.Bookings.Any())
        {
            // Không thể xóa cứng ngay: hủy suất chiếu và khởi tạo hoàn tiền.
            await CancelShowtimeAndTriggerRefundsAsync(existing, cancellationToken);
            return ServiceResult<object>.Ok(
                new { showtimeId, deleted = true },
                "Showtime softly deleted and refunds initiated.");
        }

        // Kiểm tra xem lịch sử Hủy của suất chiếu này đã có phát sinh bản ghi Refunds nào chưa
        if (existing.ShowtimeCancellation?.Refunds.Any() == true)
        {
            // Nếu có phát sinh hoàn tiền rồi thì cấm xóa vĩnh viễn (để giữ log kế toán)
            return ServiceResult<object>.Fail(409, "Showtime has refund history and cannot be permanently deleted.", "RESOURCE_HAS_REFUNDS");
        }

        if (existing.ShowtimeCancellation is not null)
        {
            return ServiceResult<object>.Fail(
                409,
                "Showtime has cancellation history and cannot be permanently deleted.",
                "RESOURCE_HAS_CANCELLATION_HISTORY");
        }

        // Nếu thỏa mãn điều kiện an toàn, tiến hành xóa cứng các ShowtimeSeats của suất chiếu
        _dbContext.ShowtimeSeats.RemoveRange(existing.ShowtimeSeats);

        // Tiến hành xóa cứng suất chiếu ra khỏi bảng Showtimes
        _dbContext.Showtimes.Remove(existing);
        
        // Lưu thay đổi xuống cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả kết quả báo thành công
        return ServiceResult<object>.Ok(new { showtimeId = showtimeId, deleted = true }, "Showtime permanently deleted successfully.");
    }

    // Hàm private xử lý logic Hủy suất chiếu và Khởi tạo quá trình Hoàn tiền
    private async Task<ServiceResult<ShowtimeResponse>> CancelShowtimeAndTriggerRefundsAsync(Showtime showtime, CancellationToken cancellationToken)
    {
        // Chuyển trạng thái suất chiếu sang Đã hủy
        showtime.Status = DomainConstants.EntityStatus.Cancelled;

        // Duyệt qua tất cả các ghế của suất chiếu
        foreach (var seat in showtime.ShowtimeSeats)
        {
            // Reset trạng thái về "Trống"
            seat.SeatStatus = DomainConstants.EntityStatus.Available;
            // Xóa bỏ ai đang khóa ghế
            seat.LockedByUserId = null;
            // Xóa thời hạn khóa
            seat.LockedUntil = null;
        }

        // Lấy danh sách các đơn đặt vé đã Thanh toán hoặc đã Hoàn tất
        var paidBookings = showtime.Bookings
            .Where(b => b.BookingStatus == DomainConstants.EntityStatus.Paid || b.BookingStatus == DomainConstants.EntityStatus.Completed)
            .ToList();

        // Định nghĩa lý do Hủy mặc định
        var cancelReason = DomainConstants.ShowtimeCancellationReason.AdministrativeUpdate;
        
        // Lấy UserId của người đang thao tác từ JWT Token (fallback về admin hệ thống nếu gọi từ background job/service)
        var userId = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "USR_SYSTEM_ADMIN";

        // Lấy thông tin bản ghi Hủy hiện tại (nếu có)
        var cancellation = showtime.ShowtimeCancellation;
        
        // Nếu chưa từng Hủy
        if (cancellation == null)
        {
            // Tạo mới một bản ghi Hủy
            cancellation = new ShowtimeCancellation
            {
                // Tạo ID cho bản ghi hủy (Tiền tố STC)
                ShowtimeCancellationId = NewId(DomainConstants.EntityIdPrefix.ShowtimeCancellation),
                // Gán ID suất chiếu
                ShowtimeId = showtime.ShowtimeId,
                // Gán lý do
                CancelReason = cancelReason,
                // Gán thời điểm hiện tại
                CancelledAt = _clock.UtcNow,
                // Gán người hủy (Admin)
                CancelledByUserId = userId,
            };
            // Thêm vào DbContext
            _dbContext.ShowtimeCancellations.Add(cancellation);
        }

        // Duyệt qua từng đơn đặt vé đã thanh toán
        foreach (var booking in paidBookings)
        {
            // Chuyển đổi trạng thái vé sang Đang chờ hoàn tiền
            booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;

            // Tìm ID giao dịch thanh toán đầu tiên (Nếu có)
            var paymentId = booking.Payments.FirstOrDefault()?.PaymentId;
            // Tìm ID nhà cung cấp dịch vụ thanh toán (VNPAY, MoMo...)
            var paymentProviderId = booking.Payments.FirstOrDefault()?.PaymentProviderId;

            // Nếu trong Collection Payments không lấy được ID thanh toán
            if (string.IsNullOrEmpty(paymentId))
            {
                // Truy vấn trực tiếp từ bảng Payments xem có dữ liệu không
                var dbPayment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId, cancellationToken);
                
                // Nếu tìm thấy
                if (dbPayment != null)
                {
                    // Lấy ID và Provider ID
                    paymentId = dbPayment.PaymentId;
                    paymentProviderId = dbPayment.PaymentProviderId;
                }
            }
            else
            {
                // Nếu đã lấy được thì xác minh lại xem PaymentId này có thực sự tồn tại trong DB không
                bool paymentExists = await _dbContext.Payments.AnyAsync(p => p.PaymentId == paymentId, cancellationToken);
                
                // Nếu không tồn tại
                if (!paymentExists)
                {
                    // Hủy ID
                    paymentId = null;
                }
            }

            // Nếu rốt cuộc không tìm được ID thanh toán hợp lệ
            if (string.IsNullOrEmpty(paymentId) || string.IsNullOrEmpty(paymentProviderId))
            {
                // Bắn ngoại lệ do không đủ dữ liệu làm cơ sở hoàn tiền
                throw new Exception($"Cannot create refund for booking {booking.BookingId} because no valid payment or payment provider record exists in the database.");
            }

            // Khởi tạo một Record hoàn tiền mới
            var refund = new Refund
            {
                // Tạo ID mới tiền tố REF
                RefundId = NewId(DomainConstants.EntityIdPrefix.Refund),
                // Gán ID đơn hàng
                BookingId = booking.BookingId,
                // Gán ID giao dịch
                PaymentId = paymentId,
                // Gán ID nhà cung cấp
                PaymentProviderId = paymentProviderId,
                // Liên kết khóa ngoại với Bản ghi Hủy
                ShowtimeCancellationId = cancellation.ShowtimeCancellationId,
                // Gán tổng số tiền sẽ hoàn lại (bằng tổng tiền đã mua)
                RefundAmount = booking.TotalAmount,
                // Trạng thái ban đầu là Pending chờ duyệt
                RefundStatus = DomainConstants.RefundStatus.Pending,
                // Lý do hoàn
                RefundReason = cancelReason,
                // Thời điểm yêu cầu
                RequestedAt = _clock.UtcNow
            };
            // Thêm vào DbContext
            _dbContext.Refunds.Add(refund);
            
            // Lấy email khách hàng để thông báo
            var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
            
            // Nếu có email
            if (!string.IsNullOrEmpty(customerEmail))
            {
                string subject = _emailTemplates.ShowtimeCancellationSubject;
                var movieTitle = showtime.Movie?.Title ?? "bạn đã đặt";
                var startTimeStr = showtime.StartTime.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                var customerName = booking.CustomerProfile?.User?.FullName;
                
                // Đẩy tiến trình gửi Email vào Hangfire sử dụng AI viết thư xin lỗi
                _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                    ai.SendAiApologyEmailAsync(
                        customerEmail, 
                        subject, 
                        "Hủy suất chiếu", 
                        $"Suất chiếu của phim {movieTitle} vào lúc {startTimeStr} bị hủy bỏ do sự cố kỹ thuật đột xuất của rạp. Hệ thống đang tiến hành thủ tục hoàn tiền tự động.", 
                        CancellationToken.None,
                        customerName));
            }
        }

        // Lưu toàn bộ dữ liệu (Hủy, Chuyển trạng thái, Tạo record Hoàn tiền) xuống database
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Tải lại suất chiếu để có đầy đủ thông tin trả về
        var updated = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        
        // Trả về báo thành công thao tác
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(updated!), "Showtime cancelled softly and refunds initiated.", 200);
    }

    // Hàm private trợ giúp dùng để Load suất chiếu kèm nhiều bảng liên quan
    private async Task<Showtime?> LoadShowtimeAsync(
        string showtimeId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        // Khởi tạo truy vấn Showtimes
        var query = _dbContext.Showtimes
            // Kết nối Phim
            .Include(item => item.Movie)
            // Kết nối Phòng chiếu
            .Include(item => item.Room)
                // Kết nối Rạp chiếu
                .ThenInclude(room => room.Cinema)
            // Kết nối sơ đồ Ghế
            .Include(item => item.ShowtimeSeats)
            // Kết nối Đặt vé
            .Include(item => item.Bookings)
                // Kết nối Thanh toán
                .ThenInclude(b => b.Payments)
            // Kết nối Đặt vé nhánh 2
            .Include(item => item.Bookings)
                // Kết nối Customer Profile
                .ThenInclude(b => b.CustomerProfile)
                    // Kết nối User Profile
                    .ThenInclude(cp => cp!.User)
            // Chia truy vấn để tối ưu
            .AsSplitQuery()
            // Trả về dạng IQueryable
            .AsQueryable();

        // Nếu có tham số báo không tracking (chỉ để đọc)
        if (!tracking)
        {
            // Bổ sung AsNoTracking
            query = query.AsNoTracking();
        }

        // Lấy bản ghi đầu tiên thỏa mãn ID
        return await query.FirstOrDefaultAsync(item => item.ShowtimeId == showtimeId, cancellationToken);
    }

    // Hàm private kiểm tra tính hợp lệ của việc chọn Phim, Phòng, thời gian tạo mới và khả năng đụng giờ
    private async Task<ShowtimeValidationResult> ValidateMovieRoomAndOverlapAsync(
        string movieId,
        string roomId,
        DateTime startTime,
        string? excludeShowtimeId,
        DateTime? existingStartTime,
        CancellationToken cancellationToken)
    {
        // Kiểm tra xem ID phim có tồn tại trong DB không
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(
            item => item.MovieId == movieId,
            cancellationToken);
            
        // Nếu không có phim
        if (movie is null)
        {
            // Báo lỗi phim không tồn tại
            return ShowtimeValidationResult.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        // Nếu phim đang bị lưu trữ (Archived) hoặc không hoạt động (Inactive)
        if (movie.MovieStatus == DomainConstants.EntityStatus.Archived || movie.MovieStatus == DomainConstants.EntityStatus.Inactive)
        {
            // Báo lỗi không cho phép tạo suất chiếu
            return ShowtimeValidationResult.Fail(
                400,
                "Movie is not available for showtimes.",
                "MOVIE_NOT_SELLABLE");
        }

        // Kiểm tra ID phòng chiếu
        var room = await _dbContext.Rooms
            // Bao gồm thông tin rạp
            .Include(item => item.Cinema)
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);
            
        // Nếu không tồn tại phòng
        if (room is null)
        {
            // Trả lỗi không tìm thấy phòng
            return ShowtimeValidationResult.Fail(404, "Room was not found.", "ROOM_NOT_FOUND");
        }

        // Nếu phòng không Active hoặc bản thân cái rạp không Active
        if (room.RoomStatus != DomainConstants.EntityStatus.Active || room.Cinema.CinemaStatus != DomainConstants.EntityStatus.Active)
        {
            // Trả lỗi không thể sử dụng phòng
            return ShowtimeValidationResult.Fail(400, "Room or cinema is not active.", "ROOM_NOT_AVAILABLE");
        }

        // Chuyển đổi giờ bắt đầu về UTC
        var normalizedStartTime = EnsureUtc(startTime);
        
        // Nếu là trường hợp thêm mới (không có existingStartTime) hoặc giờ chiếu bị đổi
        if (existingStartTime == null || normalizedStartTime != EnsureUtc(existingStartTime.Value))
        {
            // Nếu thời gian bắt đầu nhỏ hơn hiện tại
            if (normalizedStartTime <= _clock.UtcNow)
            {
                // Trả lỗi giờ bắt đầu phải nằm ở tương lai
                return ShowtimeValidationResult.Fail(
                    400,
                    "Start time must be in the future.",
                    "INVALID_START_TIME");
            }
        }
        
        // Tính toán giờ kết thúc = giờ bắt đầu + (thời lượng phim + thời gian dọn rạp quy định)
        var endTime = normalizedStartTime.AddMinutes(movie.DurationMinutes + _settings.ScreeningRoomCleaningMinutes);
        
        // Truy vấn DB kiểm tra xem có suất chiếu nào bị đè giờ không và lấy thông tin suất chiếu xung đột đầu tiên
        var conflictingShowtime = await _dbContext.Showtimes
            .Include(item => item.Movie)
            .FirstOrDefaultAsync(
                item => item.RoomId == roomId
                    && item.ShowtimeId != excludeShowtimeId
                    && item.Status != DomainConstants.EntityStatus.Cancelled
                    && normalizedStartTime < item.EndTime // strict inequality allows touching ends
                    && endTime > item.StartTime,
                cancellationToken);

        // Nếu có trùng thời gian
        if (conflictingShowtime is not null)
        {
            var conflictMovieTitle = conflictingShowtime.Movie?.Title ?? "khác";
            var conflictStartStr = conflictingShowtime.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            var conflictEndStr = conflictingShowtime.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

            var detailedMessage = $"Suất chiếu bị trùng lịch với phim \"{conflictMovieTitle}\" ({conflictStartStr} - {conflictEndStr}) tại phòng này (bao gồm 15 phút dọn rạp).";

            // Trả lỗi 409 Xung đột kèm chi tiết
            return ShowtimeValidationResult.Fail(
                409,
                detailedMessage,
                "SHOWTIME_OVERLAP",
                endTime);
        }

        // Nếu qua hết các Validation, trả về OK kèm EndTime đã tính toán
        return ShowtimeValidationResult.Ok(endTime);
    }

    // Hàm private map từ Entity hệ thống sang đối tượng Response
    private static ShowtimeResponse ToResponse(Showtime showtime)
    {
        return new ShowtimeResponse
        {
            // Gán các thuộc tính
            ShowtimeId = showtime.ShowtimeId,
            MovieId = showtime.MovieId,
            // Tránh lỗi null nếu không có Movie
            MovieTitle = showtime.Movie?.Title ?? string.Empty,
            RoomId = showtime.RoomId,
            RoomName = showtime.Room?.RoomName ?? string.Empty,
            CinemaId = showtime.Room?.CinemaId ?? string.Empty,
            CinemaName = showtime.Room?.Cinema?.CinemaName ?? string.Empty,
            StartTime = showtime.StartTime,
            EndTime = showtime.EndTime,
            BasePrice = showtime.BasePrice,
            Status = showtime.Status,
            // Đếm số ghế của suất chiếu
            ShowtimeSeatCount = showtime.ShowtimeSeats.Count,
            HasBookings = showtime.Bookings.Any(b => b.BookingStatus != DomainConstants.BookingStatus.Cancelled) || showtime.ShowtimeSeats.Any(sts => sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Booked || sts.SeatStatus == DomainConstants.ShowtimeSeatStatus.Locked || sts.SeatStatus == "BOOKED" || sts.SeatStatus == "Booked")
        };
    }

    // Hàm private đảm bảo thời gian luôn mang định dạng UTC
    private static DateTime EnsureUtc(DateTime value)
    {
        // Switch kiểm tra thuộc tính Kind của DateTime
        return value.Kind switch
        {
            // Nếu là UTC chuẩn, trả về luôn
            DateTimeKind.Utc => value,
            // Nếu là dạng local, convert thành UTC
            DateTimeKind.Local => value.ToUniversalTime(),
            // Nếu không xác định, ép kiểu thành UTC
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    // Hàm private chuẩn hóa chuỗi trạng thái thành IN HOA
    private static string NormalizeStatus(string status)
    {
        return status.Trim().ToUpperInvariant();
    }

    // Hàm private tạo ID chuẩn ngẫu nhiên (Tiền tố + Guid)
    private static string NewId(string prefix)
    {
        return CinemaSystem.Domain.Utilities.IdGenerator.NewId(prefix);
    }

    // Hàm private khởi tạo Entity ghế cho một suất chiếu từ thông tin ID
    private ShowtimeSeat CreateShowtimeSeat(string showtimeId, string seatId)
    {
        // Khởi tạo đối tượng ShowtimeSeat
        var showtimeSeat = new ShowtimeSeat
        {
            // ID của ghế (prefix STS)
            ShowtimeSeatId = NewId(DomainConstants.EntityIdPrefix.ShowtimeSeat),
            // ID suất chiếu
            ShowtimeId = showtimeId,
            // ID thực tế của ghế cứng
            SeatId = seatId,
            // Đặt trạng thái ban đầu là Trống (Có thể đặt mua)
            SeatStatus = DomainConstants.EntityStatus.Available
        };

        // Nếu cơ sở dữ liệu không phải Relational DB (ví dụ InMemoryDb dùng lúc test)
        if (!_dbContext.Database.IsRelational())
        {
            // Mock RowVersion tránh lỗi
            showtimeSeat.RowVersion = new byte[8];
        }

        // Trả về ghế
        return showtimeSeat;
    }

    // Một Record riêng hỗ trợ bọc lại dữ liệu Validation
    private sealed record ShowtimeValidationResult(
        bool Success,
        int StatusCode,
        string Message,
        string? ErrorCode,
        DateTime EndTime)
    {
        // Khởi tạo đối tượng OK
        public static ShowtimeValidationResult Ok(DateTime endTime)
        {
            return new ShowtimeValidationResult(true, 200, string.Empty, null, endTime);
        }

        // Khởi tạo đối tượng Lỗi
        public static ShowtimeValidationResult Fail(
            int statusCode,
            string message,
            string errorCode,
            DateTime endTime = default)
        {
            return new ShowtimeValidationResult(false, statusCode, message, errorCode, endTime);
        }
    }
}
