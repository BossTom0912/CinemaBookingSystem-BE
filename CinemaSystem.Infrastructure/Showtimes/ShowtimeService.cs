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
        DomainConstants.EntityStatus.Open,
        // Trạng thái đóng không bán vé nữa
        DomainConstants.EntityStatus.Closed,
        // Trạng thái đã hủy
        DomainConstants.EntityStatus.Cancelled,
        // Trạng thái đã hoàn thành (chiếu xong)
        DomainConstants.EntityStatus.Completed,
        // Trạng thái đang xử lý không ổn định (ví dụ đang đổi rạp/giờ)
        DomainConstants.EntityStatus.ProcessingUnstable
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
        // Truy vấn bảng Showtimes
        var showtimes = await _dbContext.Showtimes
            // Không tracking để tăng hiệu suất do chỉ đọc dữ liệu
            .AsNoTracking()
            // Sắp xếp tăng dần theo thời gian bắt đầu
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
                ShowtimeSeatCount = item.ShowtimeSeats.Count
            })
            // Chuyển kết quả truy vấn thành một List bất đồng bộ
            .ToListAsync(cancellationToken);

        // Trả về kết quả thành công với danh sách suất chiếu
        return ServiceResult<IReadOnlyList<ShowtimeResponse>>.Ok(
            showtimes,
            "Showtimes retrieved successfully.");
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
                ShowtimeSeatCount = item.ShowtimeSeats.Count
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

        // Nếu có thay đổi cốt lõi VÀ suất chiếu này đã có vé được thanh toán thành công
        if (coreInfoChanged && showtime.Bookings.Any(b => b.BookingStatus == DomainConstants.EntityStatus.Paid))
        {
            // Khởi tạo Transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Chuyển trạng thái của suất chiếu thành "Đang xử lý không ổn định" do ảnh hưởng tới khách đã mua vé
                showtime.Status = DomainConstants.EntityStatus.ProcessingUnstable;
                
                // Lọc ra các đơn đặt vé đã được thanh toán của suất chiếu này
                var paidBookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.Paid).ToList();
                
                // Duyệt qua từng đơn đặt vé
                foreach (var booking in paidBookings)
                {
                    // Chuyển trạng thái đơn đặt vé thành "Đang xử lý không ổn định"
                    booking.BookingStatus = DomainConstants.EntityStatus.ProcessingUnstable;
                    
                    // Khởi tạo list để ghi lại chi tiết các thay đổi
                    var updateDetails = new List<string>();
                    // Nếu đổi phòng, ghi vào list
                    if (roomChanged) updateDetails.Add($"Room changed to {request.RoomId}");
                    // Nếu đổi giờ, ghi vào list
                    if (timeChanged) updateDetails.Add($"Start time changed to {normalizedStartTime:yyyy-MM-dd HH:mm}");
                    // Kết hợp các thay đổi thành một chuỗi lý do
                    var updateReason = string.Join(" and ", updateDetails);

                    // Lấy email khách hàng (ưu tiên email tài khoản, sau đó tới email khách vãng lai)
                    var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
                    
                    // Nếu có email hợp lệ
                    if (!string.IsNullOrEmpty(customerEmail))
                    {
                        // Tính chênh lệch thời gian giữa giờ chiếu cũ và mới (tính bằng phút)
                        var timeDiff = Math.Abs((normalizedStartTime - showtime.StartTime).TotalMinutes);
                        
                        if (timeChanged
                            && timeDiff >= _settings.ShowtimeMaterialChangeThresholdMinutes)
                        {
                            // Lấy secret key từ cấu hình bảo mật
                            var secret = _securitySettings.ConfirmationTokenSecret;
                            
                            // Sử dụng HMACSHA256 để băm ID của booking tạo mã token
                            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
                            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(booking.BookingId));
                            
                            // Chuyển mã hash sang dạng chuỗi Base64
                            var token = Convert.ToBase64String(hash);
                            // Mã hóa token để an toàn khi truyền qua URL
                            var encodedToken = System.Uri.EscapeDataString(token);
                            
                            // Lấy tiêu đề email cho sự kiện đổi giờ chiếu
                            string subject = _emailTemplates.ShowtimeTimeChangeSubject;
                            var movieTitle = showtime.Movie?.Title ?? "bạn đã đặt";
                            var newTimeStr = normalizedStartTime.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                            var bookingId = booking.BookingId;
                            
                            // Đẩy job gửi email AI song ngữ kèm các nút bấm chấp nhận/hoàn tiền qua Hangfire
                            _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                                ai.SendAiTimeChangeEmailAsync(
                                    customerEmail, 
                                    subject, 
                                    movieTitle,
                                    newTimeStr, 
                                    bookingId, 
                                    encodedToken, 
                                    CancellationToken.None));
                        }
                        else
                        {
                            // Nếu thay đổi dưới 15 phút hoặc chỉ đổi phòng thì gửi email thông báo nhẹ nhàng qua dịch vụ AI
                            string subject = _emailTemplates.ShowtimeTimeChangeNoticeSubject;
                            var movieTitleNotice = showtime.Movie?.Title ?? "bạn đã đặt";
                            var newTimeStrNotice = normalizedStartTime.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                            
                            _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                                ai.SendAiApologyEmailAsync(
                                    customerEmail, 
                                    subject, 
                                    "Điều chỉnh thông tin suất chiếu", 
                                    $"Suất chiếu của phim {movieTitleNotice} đã được điều chỉnh sang giờ mới: {newTimeStrNotice} (Chi tiết thay đổi: {updateReason}).", 
                                    CancellationToken.None));
                        }
                    }
                }
                
                // MediatR is not registered, so Hangfire cannot resolve IMediator, causing an abstract class instantiation error
                // _backgroundJobClient.Enqueue<IMediator>(m => m.Publish(new ShowtimeUnstableEvent { ShowtimeId = showtime.ShowtimeId, Reason = "Core info updated after tickets sold" }, CancellationToken.None));

                // Lưu tất cả các thay đổi trạng thái vào Database
                await _dbContext.SaveChangesAsync(cancellationToken);
                // Xác nhận (commit) Transaction thành công
                await transaction.CommitAsync(cancellationToken);
                
                // Tải lại dữ liệu suất chiếu từ DB (không tracking) để trả về API
                var unstable = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
                // Trả về báo thành công, nhưng trạng thái là cần xử lý thủ công (unstable)
                return ServiceResult<ShowtimeResponse>.Ok(ToResponse(unstable!), "Showtime unstable. Manual processing required.", 200);
            }
            catch (Exception)
            {
                // Nếu có bất kì lỗi nào, hoàn tác (Rollback) Transaction
                await transaction.RollbackAsync(cancellationToken);
                // Bắn lại lỗi (Throw exception)
                throw;
            }
        }

        // Nếu không có vé nào bị ảnh hưởng (hoặc không thay đổi thông tin cốt lõi)
        // Gọi hàm validate để kiểm tra lại phim, phòng và đụng độ khung giờ
        var validation = await ValidateMovieRoomAndOverlapAsync(
            request.MovieId,
            request.RoomId,
            request.StartTime,
            // Truyền ID của chính suất chiếu này vào để bỏ qua check trùng với chính nó
            showtime.ShowtimeId,
            showtime.StartTime,
            cancellationToken);
            
        // Nếu validate không hợp lệ
        if (!validation.Success)
        {
            // Trả về lỗi
            return ServiceResult<ShowtimeResponse>.Fail(
                validation.StatusCode,
                validation.Message,
                validation.ErrorCode!);
        }

        // Cập nhật ID phim
        showtime.MovieId = request.MovieId;
        // Cập nhật ID phòng chiếu
        showtime.RoomId = request.RoomId;
        // Cập nhật thời gian bắt đầu (chuẩn UTC)
        showtime.StartTime = normalizedStartTime;
        // Cập nhật thời gian kết thúc (lấy từ kết quả validate)
        showtime.EndTime = validation.EndTime;
        // Cập nhật giá vé cơ sở
        showtime.BasePrice = request.BasePrice;
        // Cập nhật trạng thái
        showtime.Status = status;

        // Nếu có sự thay đổi về phòng chiếu (phòng chiếu mới so với phòng chiếu cũ)
        if (roomChanged)
        {
            // Truy vấn lấy danh sách ghế đang hoạt động của phòng mới
            var activeSeats2 = await _dbContext.Seats
                .Where(item => item.RoomId == request.RoomId && item.IsActive)
                .ToListAsync(cancellationToken);
                
            // Nếu phòng mới không có ghế hoạt động
            if (activeSeats2.Count == 0)
            {
                // Trả về lỗi 400
                return ServiceResult<ShowtimeResponse>.Fail(400, "Room has no active seats.", "ROOM_HAS_NO_SEATS");
            }

            // Xóa bỏ tất cả các ghế đã tạo cũ của suất chiếu này (do đổi phòng)
            _dbContext.ShowtimeSeats.RemoveRange(showtime.ShowtimeSeats);
            // Thêm lại tập hợp ghế mới ứng với phòng mới
            await _dbContext.ShowtimeSeats.AddRangeAsync(activeSeats2.Select(seat => CreateShowtimeSeat(showtime.ShowtimeId, seat.SeatId)), cancellationToken);
        }

        // Lưu thông tin được cập nhật xuống cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Tải lại suất chiếu để lấy đủ thông tin phục vụ Response
        var updated = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        // Trả về thành công
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(updated!), "Showtime updated successfully.", 200);
    }

    // Phương thức chuyên dùng để đổi phòng chiếu và cấu hình lại ghế cho vé đã bán
    public async Task<ServiceResult<ShowtimeResponse>> ChangeRoomAsync(
        string showtimeId,
        ChangeRoomRequest request,
        CancellationToken cancellationToken)
    {
        // Tải suất chiếu lên bao gồm các ghế của nó và đơn đặt vé
        var showtime = await _dbContext.Showtimes
            // Bao gồm danh sách ghế của suất chiếu (ShowtimeSeats)
            .Include(s => s.ShowtimeSeats)
                // Từ ShowtimeSeat lấy thông tin Seat thật (ở bảng Seats)
                .ThenInclude(sts => sts.Seat)
            // Bao gồm danh sách Booking của suất chiếu
            .Include(s => s.Bookings)
            // Lấy dòng đầu tiên khớp ID suất chiếu
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId, cancellationToken);

        // Nếu không tìm thấy suất chiếu
        if (showtime == null)
            // Báo lỗi 404
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime not found.", "NOT_FOUND");

        // Lấy thông tin phòng chiếu mới bao gồm cả danh sách ghế (Seats)
        var newRoom = await _dbContext.Rooms
            // Bao gồm danh sách ghế
            .Include(r => r.Seats)
            // Tìm theo ID phòng chiếu mới yêu cầu
            .FirstOrDefaultAsync(r => r.RoomId == request.NewRoomId, cancellationToken);

        // Nếu không tìm thấy phòng mới
        if (newRoom == null)
            // Báo lỗi 404
            return ServiceResult<ShowtimeResponse>.Fail(404, "New room not found.", "NOT_FOUND");

        // Nếu phòng mới không ở trạng thái Active
        if (newRoom.RoomStatus != DomainConstants.EntityStatus.Active)
            // Trả lỗi 400
            return ServiceResult<ShowtimeResponse>.Fail(400, "New room is not active.", "ROOM_INACTIVE");

        // Lấy danh sách các ghế kích hoạt của phòng mới
        var activeNewSeats = newRoom.Seats.Where(s => s.IsActive).ToList();

        // Lấy mapping ghế người dùng truyền vào, nếu null thì khởi tạo Dictionary rỗng
        var seatMapping = request.SeatMapping ?? new Dictionary<string, string>();

        // Duyệt qua các ghế của suất chiếu cũ (để kiểm tra xem có map được với phòng mới không)
        foreach (var oldSts in showtime.ShowtimeSeats)
        {
            // Chỉ xét những ghế đã được đặt chỗ hoặc thanh toán hoặc đang gắn với vé
            if (oldSts.SeatStatus == DomainConstants.EntityStatus.Booked || oldSts.SeatStatus == DomainConstants.EntityStatus.Paid || oldSts.BookingSeat != null)
            {
                // Biến lưu trữ ID của ghế mới tương ứng
                string? newSeatId = null;
                
                // Nếu khách hàng cung cấp mapping cho ghế này thì lấy ID mới theo mapping
                if (seatMapping.TryGetValue(oldSts.SeatId, out var mappedId))
                {
                    // Gán ID theo mapping
                    newSeatId = mappedId;
                }
                else // Nếu không có mapping thủ công
                {
                    // Tìm kiếm một ghế ở phòng mới có cùng mã ghế (SeatCode) ví dụ "A1"
                    var equivalentSeat = activeNewSeats.FirstOrDefault(s => s.SeatCode == oldSts.Seat.SeatCode);
                    // Nếu tìm thấy
                    if (equivalentSeat != null)
                    {
                        // Gán ID của ghế tương đương đó
                        newSeatId = equivalentSeat.SeatId;
                    }
                }

                // Nếu sau quá trình tìm kiếm mà không có ghế nào khớp cho ghế đã bán
                if (newSeatId == null)
                {
                    // Trả lỗi 400 vì không thể chuyển đổi sơ đồ ghế
                    return ServiceResult<ShowtimeResponse>.Fail(400, $"Cannot map seat {oldSts.Seat.SeatCode} to new room.", "MAPPING_FAILED");
                }
            }
        }

        var bookingIds = showtime.Bookings.Select(b => b.BookingId).ToList();

        // Truy vấn tất cả BookingSeats liên quan đến những Booking của suất chiếu này kèm thông tin Booking
        var bookingSeats = await _dbContext.BookingSeats
            // Lọc những BookingSeat thuộc về các Booking của suất chiếu
            .Where(bs => bookingIds.Contains(bs.BookingId))
            // Include để lấy thông tin Booking liên quan để chuyển trạng thái và gửi email
            .Include(bs => bs.Booking)
                .ThenInclude(b => b.CustomerProfile)
                    .ThenInclude(cp => cp!.User)
            // Include để lấy ShowtimeSeat hiện tại của BookingSeat
            .Include(bs => bs.ShowtimeSeat)
                // Lấy thông tin Seat từ ShowtimeSeat
                .ThenInclude(sts => sts.Seat)
            // Lấy kết quả ra List
            .ToListAsync(cancellationToken);

        // Khởi tạo list để tạo các bản ghi ghế cho suất chiếu ở phòng mới
        var newShowtimeSeats = new List<ShowtimeSeat>();
        // Lặp qua tất cả ghế active của phòng mới
        foreach (var newSeat in activeNewSeats)
        {
            // Tạo đối tượng ShowtimeSeat và đưa vào list
            newShowtimeSeats.Add(CreateShowtimeSeat(showtime.ShowtimeId, newSeat.SeatId));
        }
        // Thêm tất cả vào Database context
        await _dbContext.ShowtimeSeats.AddRangeAsync(newShowtimeSeats, cancellationToken);

        var affectedBookings = new System.Collections.Generic.HashSet<string>();

        // Duyệt qua từng bản ghi ghế của đơn đặt vé
        foreach (var bs in bookingSeats)
        {
            if (bs.ShowtimeSeat?.Seat == null) continue;

            // Biến tạm để lưu SeatID mới
            string? newSeatId = null;
            // Nếu có trong map truyền vào thì lấy
            if (seatMapping.TryGetValue(bs.ShowtimeSeat.SeatId, out var mappedId))
            {
                newSeatId = mappedId;
            }
            else // Không có trong map thì tìm tự động theo tên ghế (SeatCode)
            {
                var equivalentSeat = activeNewSeats.FirstOrDefault(s => s.SeatCode == bs.ShowtimeSeat.Seat.SeatCode);
                if (equivalentSeat != null) newSeatId = equivalentSeat.SeatId;
            }
            
            // Nếu tìm được ghế thay thế
            if (newSeatId != null)
            {
                // Tìm kiếm ShowtimeSeat mới đã được add vào danh sách tạo ở bước trên
                var newSts = newShowtimeSeats.FirstOrDefault(sts => sts.SeatId == newSeatId);
                // Nếu tìm thấy
                if (newSts != null)
                {
                    // Cập nhật lại liên kết cho BookingSeat trỏ tới ShowtimeSeat mới
                    bs.ShowtimeSeatId = newSts.ShowtimeSeatId;
                    // Đánh dấu trạng thái ghế mới là đã bán (Booked)
                    newSts.SeatStatus = DomainConstants.EntityStatus.Booked;

                    // Kiểm tra xem loại ghế ở phòng mới có trùng khớp với phòng cũ không
                    var newSeat = activeNewSeats.FirstOrDefault(s => s.SeatId == newSeatId);
                    if (newSeat != null && newSeat.SeatTypeId != bs.ShowtimeSeat.Seat.SeatTypeId)
                    {
                        // Đánh dấu Booking bị ảnh hưởng (hạ cấp hoặc đổi loại ghế)
                        bs.Booking.BookingStatus = DomainConstants.EntityStatus.ProcessingUnstable;
                        affectedBookings.Add(bs.BookingId);
                    }
                }
            }
        }

        // Bắt đầu xóa toàn bộ bản ghi ghế của suất chiếu (thuộc phòng cũ)
        _dbContext.ShowtimeSeats.RemoveRange(showtime.ShowtimeSeats.ToList());

        // Cập nhật ID phòng mới cho suất chiếu
        showtime.RoomId = request.NewRoomId;
        // Đặt trạng thái của suất chiếu về Mở bán (Open) hoặc ProcessingUnstable nếu có ghế bị xung đột loại
        showtime.Status = affectedBookings.Any() 
            ? DomainConstants.EntityStatus.ProcessingUnstable 
            : DomainConstants.EntityStatus.Open;

        // Lưu toàn bộ thay đổi xuống DB
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // Gửi email thông báo sơ đồ ghế mới cho các khách hàng không bị ảnh hưởng (giữ nguyên loại ghế)
        var paidBookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.Paid).ToList();
        
        // Lặp qua từng Booking ổn định
        foreach(var booking in paidBookings)
        {
            // Lấy Email khách (ưu tiên email account)
            var email = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
            
            // Nếu có email
            if (!string.IsNullOrEmpty(email))
            {
                // Lấy tiêu đề từ cấu hình template cho việc đổi phòng
                string subject = _emailTemplates.ShowtimeRoomChangeSubject;
                var movieTitle = showtime.Movie?.Title ?? "bạn đã đặt";
                var roomName = newRoom.RoomName;
                
                // Đẩy job nền gửi email qua dịch vụ AI
                _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                    ai.SendAiApologyEmailAsync(
                        email, 
                        subject, 
                        "Thay đổi phòng chiếu của suất chiếu", 
                        $"Suất chiếu phim {movieTitle} của bạn đã được chuyển sang phòng chiếu mới: {roomName}. Vui lòng kiểm tra lại vé để xem vị trí ghế mới của bạn.", 
                        CancellationToken.None));
            }
        }

        // Gửi email AI thông báo và xin lỗi song ngữ cho các khách hàng có vé bị ảnh hưởng (ProcessingUnstable)
        var unstableBookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.ProcessingUnstable).ToList();
        foreach (var booking in unstableBookings)
        {
            var email = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
            if (!string.IsNullOrEmpty(email))
            {
                var secret = _securitySettings.ConfirmationTokenSecret;
                using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(booking.BookingId));
                var token = Convert.ToBase64String(hash);
                var encodedToken = System.Uri.EscapeDataString(token);

                string subject = "Thông báo đổi phòng chiếu và loại ghế / Showtime Room and Seat Type Change Notice";
                string reason = "Thay đổi phòng chiếu dẫn đến thay đổi loại ghế của bạn (Hạ cấp/Thay đổi loại ghế)";
                string details = $"Suất chiếu của phim {showtime.Movie.Title} đã chuyển sang phòng mới: {newRoom.RoomName}. Do đó ghế của bạn bị thay đổi loại ghế. Vui lòng bấm vào Link xác nhận để chấp nhận thay đổi hoặc yêu cầu hủy hoàn tiền.";

                // Đẩy job gửi Email AI ngầm qua Hangfire
                _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                    ai.SendAiApologyEmailAsync(email, subject, reason, details, CancellationToken.None));
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
             // Thì không thể xóa cứng ngay, mà phải thực hiện Cancel suất chiếu và tiến hành thủ tục Refund
             await CancelShowtimeAndTriggerRefundsAsync(existing, cancellationToken);
             // Trả về kết quả Xóa mềm
             return ServiceResult<object>.Ok(new { showtimeId = showtimeId, deleted = true }, "Showtime softly deleted and refunds initiated.");
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
        
        // Lấy UserId của người đang thao tác từ JWT Token.
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "An authenticated user is required to cancel a showtime.");
        }

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
                
                // Đẩy tiến trình gửi Email vào Hangfire sử dụng AI viết thư xin lỗi
                _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                    ai.SendAiApologyEmailAsync(
                        customerEmail, 
                        subject, 
                        "Hủy suất chiếu", 
                        $"Suất chiếu của phim {movieTitle} vào lúc {startTimeStr} bị hủy bỏ do sự cố kỹ thuật đột xuất của rạp. Hệ thống đang tiến hành thủ tục hoàn tiền tự động.", 
                        CancellationToken.None));
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
        
        // Truy vấn DB kiểm tra xem có bất kì suất chiếu nào bị đè giờ lên suất chiếu chuẩn bị tạo này không
        var hasOverlap = await _dbContext.Showtimes.AnyAsync(
            // Cùng phòng chiếu
            item => item.RoomId == roomId
                // Khác ID (để loại trừ trường hợp tự đụng chính nó lúc Update)
                && item.ShowtimeId != excludeShowtimeId
                // Bỏ qua các suất chiếu đã Hủy
                && item.Status != DomainConstants.EntityStatus.Cancelled
                // Điều kiện đụng độ: Giờ bắt đầu mới phải nhỏ hơn giờ kết thúc cũ
                && normalizedStartTime < item.EndTime // strict inequality allows touching ends
                // Và Giờ kết thúc mới phải lớn hơn giờ bắt đầu cũ
                && endTime > item.StartTime,
            cancellationToken);

        // Nếu có trùng thời gian
        if (hasOverlap)
        {
            // Trả lỗi 409 Xung đột
            return ShowtimeValidationResult.Fail(
                409,
                "Showtime overlaps with an existing showtime in the same room.",
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
            ShowtimeSeatCount = showtime.ShowtimeSeats.Count
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
        return $"{prefix}_{Guid.NewGuid():N}";
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
