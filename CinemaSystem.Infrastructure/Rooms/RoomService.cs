using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Constants;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Rooms;

public sealed class RoomService : IRoomService
{
    // ID loại ghế mặc định
    private const string DefaultSeatTypeId = "SEAT_TYPE_STANDARD";
    // Tên loại ghế mặc định
    private const string DefaultSeatTypeName = "STANDARD";

    // Danh sách các trạng thái hợp lệ của phòng chiếu
    private static readonly HashSet<string> ValidRoomStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE",
        "INACTIVE",
        "MAINTENANCE"
    };

    // Khai báo biến DbContext để tương tác với cơ sở dữ liệu
    private readonly CinemaDbContext _dbContext;
    // Khai báo dịch vụ xử lý hoàn tiền
    private readonly IAdminRefundService _refundService;
    // Khai báo biến lưu trữ cấu hình xử lý của hệ thống
    private readonly CinemaSystem.Application.Settings.CinemaProcessingSettings _settings;

    // Constructor khởi tạo các dịch vụ phụ thuộc
    public RoomService(CinemaDbContext dbContext, IAdminRefundService refundService, Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.CinemaProcessingSettings> options)
    {
        // Gán DbContext
        _dbContext = dbContext;
        // Gán dịch vụ hoàn tiền
        _refundService = refundService;
        // Lấy giá trị cấu hình từ IOptions
        _settings = options.Value;
    }

    public async Task<ServiceResult<RoomResponse>> CreateRoomAsync(
    string cinemaId,
    CreateRoomRequest request,
    CancellationToken cancellationToken)
    {
        // Chuẩn hóa trạng thái phòng chiếu (viết hoa và bỏ khoảng trắng thừa)
        var roomStatus = NormalizeStatus(request.RoomStatus);

        // Kiểm tra xem trạng thái truyền vào có nằm trong danh sách hợp lệ không
        if (!ValidRoomStatuses.Contains(roomStatus))
        {
            // Trả về lỗi nếu trạng thái không hợp lệ
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Room status is invalid.",
                "INVALID_ROOM_STATUS");
        }

        // Tìm rạp chiếu phim trong cơ sở dữ liệu dựa trên ID
        var cinema = await _dbContext.Cinemas
            .FirstOrDefaultAsync(
                x => x.CinemaId == cinemaId,
                cancellationToken);

        // Nếu không tìm thấy rạp chiếu phim
        if (cinema == null)
        {
            // Trả về lỗi rạp không tồn tại
            return ServiceResult<RoomResponse>.Fail(
                404,
                "Cinema not found.",
                "CINEMA_NOT_FOUND");
        }

        // Tạo ID mới cho phòng chiếu
        var roomId = NewId("ROOM");
        // Khởi tạo đối tượng phòng chiếu mới
        var room = new Room
        {
            // Gán ID phòng chiếu
            RoomId = roomId,
            // Gán ID rạp chiếu phim
            CinemaId = cinemaId,
            // Gán tên phòng chiếu sau khi đã loại bỏ khoảng trắng ở 2 đầu
            RoomName = request.RoomName.Trim(),
            // Gán sức chứa của phòng
            Capacity = request.Capacity,
            // Gán trạng thái phòng chiếu
            RoomStatus = roomStatus
        };

        // Thêm phòng chiếu mới vào DbContext
        _dbContext.Rooms.Add(room);
        // Lưu thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Chuyển đổi thực thể Room sang RoomResponse
        var response = ToResponse(room);
        // Trả về kết quả thành công kèm dữ liệu phòng chiếu vừa tạo
        return ServiceResult<RoomResponse>.Ok(response, "Room created successfully.", 201);
    }
    public async Task<ServiceResult<IReadOnlyList<RoomResponse>>> GetRoomsAsync(
        string? cinemaScopeId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        // Khởi tạo truy vấn danh sách phòng chiếu và không theo dõi sự thay đổi (AsNoTracking) để tối ưu hiệu suất
        var query = _dbContext.Rooms.AsNoTracking();

        // Nếu không bao gồm các phòng chiếu không hoạt động
        if (!includeInactive)
        {
            // Lọc bỏ các phòng chiếu có trạng thái INACTIVE
            query = query.Where(room => room.RoomStatus != "INACTIVE");
        }

        if (!string.IsNullOrWhiteSpace(cinemaScopeId))
        {
            query = query.Where(room => room.CinemaId == cinemaScopeId);
        }

        // Thực thi truy vấn lấy danh sách phòng chiếu
        var rooms = await query
            // Bao gồm thông tin rạp chiếu phim liên kết
            .Include(room => room.Cinema)
            // Bao gồm thông tin danh sách ghế của phòng
            .Include(room => room.Seats)
            // Sắp xếp theo tên rạp chiếu phim tăng dần
            .OrderBy(room => room.Cinema.CinemaName)
            // Sắp xếp tiếp theo tên phòng chiếu tăng dần
            .ThenBy(room => room.RoomName)
            // Ánh xạ từng thực thể Room sang RoomResponse
            .Select(room => ToResponse(room))
            // Thực thi truy vấn bất đồng bộ và chuyển thành List
            .ToListAsync(cancellationToken);

        // Trả về kết quả thành công chứa danh sách phòng chiếu
        return ServiceResult<IReadOnlyList<RoomResponse>>.Ok(rooms, "Rooms retrieved successfully.");
    }

    public async Task<ServiceResult<RoomResponse>> GetRoomByIdAsync(
    string roomId,
    bool includeInactive,
    CancellationToken cancellationToken)
    {
        // Truy vấn tìm phòng chiếu dựa trên ID, không theo dõi sự thay đổi
        var room = await _dbContext.Rooms
            .AsNoTracking()
            // Bao gồm thông tin rạp chiếu phim
            .Include(item => item.Cinema)
            // Bao gồm danh sách các ghế
            .Include(item => item.Seats)
            // Lấy phần tử đầu tiên khớp với ID hoặc null nếu không tìm thấy
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);

        // Nếu không tìm thấy phòng chiếu
        if (room is null)
        {
            // Trả về lỗi không tìm thấy phòng
            return ServiceResult<RoomResponse>.Fail(
                404,
                "Room was not found.",
                "ROOM_NOT_FOUND");
        }

        // Nếu cấu hình không cho phép lấy phòng INACTIVE và phòng hiện tại đang INACTIVE
        if (!includeInactive && string.Equals(room.RoomStatus, "INACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            // Trả về lỗi không tìm thấy (che giấu phòng INACTIVE)
            return ServiceResult<RoomResponse>.Fail(
                404,
                "Room was not found.",
                "ROOM_NOT_FOUND");
        }

        // Trả về kết quả thành công chứa thông tin chi tiết phòng chiếu
        return ServiceResult<RoomResponse>.Ok(
            ToResponse(room),
            "Room retrieved successfully.");
    }
    public async Task<ServiceResult<object>> GenerateSeatsAsync(
    string roomId,
    GenerateSeatsRequest request,
    CancellationToken cancellationToken)
    {
        // Kiểm tra số lượng hàng ghế phải lớn hơn 0
        if (request.Rows <= 0)
        {
            // Trả về lỗi số hàng ghế không hợp lệ
            return ServiceResult<object>.Fail(
                400,
                "Rows must be greater than zero.",
                "INVALID_ROWS");
        }

        // Kiểm tra số lượng cột ghế phải lớn hơn 0
        if (request.Columns <= 0)
        {
            // Trả về lỗi số cột ghế không hợp lệ
            return ServiceResult<object>.Fail(
                400,
                "Columns must be greater than zero.",
                "INVALID_COLUMNS");
        }

        // Kiểm tra tổng số ghế tạo ra không vượt quá sức chứa tối đa theo cấu hình hệ thống
        if (request.Rows * request.Columns > _settings.MaxRoomCapacity)
        {
            // Trả về lỗi vượt quá sức chứa
            return ServiceResult<object>.Fail(
                400,
                $"Total seats cannot exceed {_settings.MaxRoomCapacity}.",
                "CAPACITY_EXCEEDED");
        }
        // Truy vấn thông tin phòng chiếu theo ID
        var room = await _dbContext.Rooms
            // Bao gồm danh sách các ghế hiện tại của phòng
            .Include(x => x.Seats)
            // Lấy phòng chiếu đầu tiên khớp với ID
            .FirstOrDefaultAsync(
                x => x.RoomId == roomId,
                cancellationToken);

        // Nếu không tìm thấy phòng chiếu
        if (room == null)
        {
            // Trả về lỗi phòng chiếu không tồn tại
            return ServiceResult<object>.Fail(
                404,
                "Room not found.",
                "ROOM_NOT_FOUND");
        }

        // Kiểm tra nếu phòng chiếu đã có ghế rồi thì không cho phép tạo mới
        if (room.Seats.Any())
        {
            // Trả về lỗi xung đột do phòng đã có ghế
            return ServiceResult<object>.Fail(
                409,
                "Room already has seats.",
                "ROOM_HAS_SEATS");
        }

        // Khởi tạo danh sách chứa các ghế sẽ được tạo
        var seats = new List<Seat>();

        // Duyệt qua từng hàng ghế theo số lượng yêu cầu
        for (int row = 0; row < request.Rows; row++)
        {
            // Tính toán ký tự đánh dấu cho hàng (A, B, C...)
            var rowLabel = ((char)('A' + row)).ToString();

            // Duyệt qua từng cột trong hàng
            for (int col = 1; col <= request.Columns; col++)
            {
                // Khởi tạo và thêm ghế mới vào danh sách
                seats.Add(new Seat
                {
                    // Sinh ID tự động cho ghế
                    SeatId = NewId("SEAT"),
                    // Gán ID phòng chiếu
                    RoomId = roomId,
                    // Gán ID loại ghế
                    SeatTypeId = request.SeatTypeId,
                    // Gán nhãn hàng (VD: A)
                    RowLabel = rowLabel,
                    // Gán số thứ tự ghế trong hàng (VD: 1)
                    SeatNumber = col,
                    // Kết hợp thành mã ghế (VD: A1)
                    SeatCode = $"{rowLabel}{col}",
                    // Thiết lập trạng thái hoạt động
                    IsActive = true
                });
            }
        }

        // Thêm toàn bộ danh sách ghế vừa tạo vào DbContext
        await _dbContext.Seats.AddRangeAsync(
            seats,
            cancellationToken);

        // Cập nhật lại sức chứa của phòng chiếu bằng đúng số lượng ghế vừa tạo
        room.Capacity = seats.Count;

        // Lưu thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(
            cancellationToken);

        // Trả về kết quả thành công với thông tin số ghế đã tạo
        return ServiceResult<object>.Ok(
            new
            {
                // Thông tin ID phòng chiếu
                RoomId = roomId,
                // Tổng số lượng ghế đã tạo
                TotalSeats = seats.Count
            },
            "Seats generated successfully.");
    }


    public async Task<ServiceResult<RoomResponse>> UpdateRoomAsync(
        string roomId,
        UpdateRoomRequest request,
        string actionUserId,
        CancellationToken cancellationToken)
    {
        // Chuẩn hóa trạng thái truyền vào
        var roomStatus = NormalizeStatus(request.RoomStatus);
        // Kiểm tra nếu trạng thái không hợp lệ
        if (!ValidRoomStatuses.Contains(roomStatus))
        {
            // Trả về lỗi
            return ServiceResult<RoomResponse>.Fail(400, "Room status is invalid.", "INVALID_ROOM_STATUS");
        }

        // Lấy thông tin phòng chiếu từ DB
        var room = await _dbContext.Rooms
            // Bao gồm rạp chiếu phim
            .Include(item => item.Cinema)
            // Bao gồm danh sách ghế
            .Include(item => item.Seats)
            // Tìm theo ID
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);
        // Nếu không tìm thấy phòng chiếu
        if (room is null)
        {
            // Trả về lỗi
            return ServiceResult<RoomResponse>.Fail(404, "Room was not found.", "ROOM_NOT_FOUND");
        }

        // Kiểm tra nếu tên phòng chiếu bị để trống
        if (string.IsNullOrWhiteSpace(request.RoomName))
        {
            // Trả về lỗi thiếu tên phòng
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Room name is required.",
                "ROOM_NAME_REQUIRED");
        }

        // Chuẩn hóa tên phòng chiếu (Xóa khoảng trắng thừa ở đầu, cuối và giữa các từ)
        var normalizedRoomName = string.Join(
            " ",
            request.RoomName
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // Kiểm tra xem có phòng chiếu nào khác trong cùng rạp trùng tên hay không
        var duplicatedName = await _dbContext.Rooms.AnyAsync(
            item =>
                // Bỏ qua phòng chiếu hiện tại
                item.RoomId != roomId &&
                // Thuộc cùng rạp
                item.CinemaId == room.CinemaId &&
                // So sánh tên không phân biệt hoa thường
                item.RoomName.ToUpper().Trim() ==
                normalizedRoomName.ToUpper(),
            cancellationToken);

        // Nếu trùng tên
        if (duplicatedName)
        {
            // Trả về lỗi trùng lặp
            return ServiceResult<RoomResponse>.Fail(
                409,
                "Room name already exists in this cinema.",
                "DUPLICATE_ROOM_NAME");
        }
        // Kiểm tra sức chứa truyền vào phải lớn hơn 0
        if (request.Capacity <= 0)
        {
            // Trả về lỗi
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Capacity must be greater than zero.",
                "INVALID_CAPACITY");
        }

        // Kiểm tra nếu sức chứa vượt quá giới hạn hệ thống
        if (request.Capacity > _settings.MaxRoomCapacity)
        {
            // Trả về lỗi vượt quá sức chứa
            return ServiceResult<RoomResponse>.Fail(
                400,
                $"Capacity cannot exceed {_settings.MaxRoomCapacity}.",
                "CAPACITY_EXCEEDED");
        }
        // Cập nhật sức chứa dự kiến để kiểm tra tính hợp lệ
        room.Capacity = request.Capacity;
        // Nếu phòng đã có ghế và sức chứa mới nhỏ hơn số lượng ghế hiện tại
        if (room.Seats.Any()
    && request.Capacity < room.Seats.Count)
        {
            // Trả về lỗi do không thể thiết lập sức chứa nhỏ hơn số ghế thực tế
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Capacity cannot be less than existing seat count.",
                "INVALID_CAPACITY");
        }

        // Nếu có sự thay đổi trạng thái và trạng thái mới là bảo trì hoặc ngưng hoạt động
        if (room.RoomStatus != roomStatus && (roomStatus == "MAINTENANCE" || roomStatus == "INACTIVE"))
        {
            // Tìm tất cả các suất chiếu đang mở (Open) tại phòng này
            var openShowtimes = await _dbContext.Showtimes
                .Where(s => s.RoomId == roomId && s.Status == DomainConstants.EntityStatus.Open)
                .ToListAsync(cancellationToken);

            // Duyệt qua từng suất chiếu đang mở
            foreach (var st in openShowtimes)
            {
                // Cập nhật trạng thái suất chiếu thành bị đình chỉ (SUSPENDED)
                st.Status = "SUSPENDED";
            }
        }

        // Áp dụng các thay đổi thông tin phòng chiếu trực tiếp
        // Cập nhật tên phòng
        room.RoomName = normalizedRoomName;
        // Cập nhật sức chứa
        room.Capacity = request.Capacity;
        // Cập nhật trạng thái
        room.RoomStatus = roomStatus;

        // Lưu toàn bộ thay đổi vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Map sang DTO Response
        var response = ToResponse(room);
        // Trả về kết quả thành công
        return ServiceResult<RoomResponse>.Ok(response, "Room updated successfully.", 200);
    }

    public async Task<ServiceResult<object>> DeleteRoomAsync(
    string roomId,
    string actionUserId,
    CancellationToken cancellationToken)
    {
        // Tìm phòng chiếu cần xóa theo ID
        var room = await _dbContext.Rooms
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);

        // Nếu không tìm thấy
        if (room is null)
        {
            // Trả về lỗi
            return ServiceResult<object>.Fail(
                404,
                "Room was not found.",
                "ROOM_NOT_FOUND");
        }

        // Tìm các suất chiếu đang mở liên quan tới phòng chiếu này
        var openShowtimes = await _dbContext.Showtimes
            .Where(s => s.RoomId == roomId && s.Status == DomainConstants.EntityStatus.Open)
            .ToListAsync(cancellationToken);

        // Lặp qua từng suất chiếu đang mở
        foreach (var st in openShowtimes)
        {
            // Cập nhật trạng thái thành SUSPENDED do phòng bị xóa
            st.Status = "SUSPENDED";
        }

        // Tiến hành xóa mềm bằng cách đổi trạng thái thành INACTIVE
        room.RoomStatus = "INACTIVE";
        // Lưu thay đổi vào DB
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả thành công
        return ServiceResult<object>.Ok(new { RoomId = roomId, RoomStatus = room.RoomStatus }, "Room deactivated successfully.");
    }

    // Các phương thức Apply* đã bị loại bỏ — sử dụng trực tiếp CRUD





    // Hàm tiện ích chuyển đổi từ Room sang RoomResponse
    private static RoomResponse ToResponse(Room room)
    {
        // Khởi tạo và trả về đối tượng RoomResponse
        return new RoomResponse
        {
            // Gán ID phòng
            RoomId = room.RoomId,
            // Gán ID rạp chiếu phim
            CinemaId = room.CinemaId,
            // Gán tên rạp chiếu phim hoặc chuỗi rỗng nếu rạp null
            CinemaName = room.Cinema?.CinemaName ?? string.Empty,
            // Gán tên phòng chiếu
            RoomName = room.RoomName,
            // Gán sức chứa
            Capacity = room.Capacity,
            // Gán trạng thái
            RoomStatus = room.RoomStatus,
            // Đếm số lượng ghế, nếu null thì trả về 0
            SeatCount = room.Seats?.Count ?? 0
        };
    }

    // Hàm tiện ích để chuẩn hóa chuỗi trạng thái
    private static string NormalizeStatus(string status)
    {
        // Cắt khoảng trắng 2 đầu và chuyển tất cả sang chữ in hoa
        return status.Trim().ToUpperInvariant();
    }

    // Hàm tiện ích để tạo nhãn hàng ghế (VD: A, B, C... Z, AA, AB)
    private static string ToRowLabel(int rowIndex)
    {
        // Khởi tạo chuỗi rỗng
        var label = string.Empty;
        // Gán chỉ số hàng hiện tại
        var current = rowIndex;
        // Lặp tạo ký tự
        do
        {
            // Lấy ký tự tương ứng và ghép vào đầu chuỗi
            label = (char)('A' + current % 26) + label;
            // Tính lại số dư để xử lý trường hợp vượt quá 26 chữ cái
            current = current / 26 - 1;
        }
        // Dừng khi chỉ số nhỏ hơn 0
        while (current >= 0);

        // Trả về nhãn hàng ghế
        return label;
    }

    // Hàm tiện ích tạo ID mới với tiền tố được cung cấp
    private static string NewId(string prefix)
    {
        // Trả về định dạng chuỗi: TiềnTố_GuidKhongDauGachNgang
        return $"{prefix}_{Guid.NewGuid():N}";
    }
}
