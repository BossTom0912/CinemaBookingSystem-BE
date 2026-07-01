// Sử dụng các thư viện và namespace cần thiết cho chức năng chung của ứng dụng
using CinemaSystem.Application.Common;
// Khai báo các interface được định nghĩa trong tầng Application
using CinemaSystem.Application.Interfaces;
// Bao gồm các Data Transfer Object (DTO) liên quan đến rạp chiếu phim
using CinemaSystem.Contracts.Cinemas;
// Sử dụng lớp kết nối cơ sở dữ liệu (DbContext) từ tầng Infrastructure
using CinemaSystem.Infrastructure.Persistence;
// Sử dụng Entity Framework Core để thao tác với cơ sở dữ liệu
using Microsoft.EntityFrameworkCore;

// Khai báo namespace cho các service xử lý rạp chiếu trong tầng Infrastructure
namespace CinemaSystem.Infrastructure.Cinemas;

// Định nghĩa lớp CinemaService triển khai interface ICinemaService, đánh dấu là sealed để không cho kế thừa
public sealed class CinemaService : ICinemaService
{
    // Biến private chỉ đọc để giữ kết nối với cơ sở dữ liệu qua CinemaDbContext
    private readonly CinemaDbContext _dbContext;

    // Constructor khởi tạo CinemaService với phụ thuộc CinemaDbContext được tiêm vào (Dependency Injection)
    public CinemaService(CinemaDbContext dbContext)
    {
        // Gán dbContext được tiêm vào biến thành viên _dbContext
        _dbContext = dbContext;
    }

    // Phương thức bất đồng bộ để lấy danh sách tất cả các rạp chiếu phim
    public async Task<ServiceResult<IReadOnlyList<CinemaResponse>>> GetCinemasAsync(
        // Token để hủy tác vụ bất đồng bộ nếu cần
        CancellationToken cancellationToken)
    {
        // Truy vấn danh sách các rạp chiếu phim từ cơ sở dữ liệu
        var cinemas = await _dbContext.Cinemas
            // Tắt chức năng theo dõi thay đổi (tracking) của EF Core để tăng hiệu suất truy vấn đọc
            .AsNoTracking()
            // Sắp xếp danh sách rạp chiếu theo tên rạp theo thứ tự bảng chữ cái
            .OrderBy(cinema => cinema.CinemaName)
            // Ánh xạ (Map) từ Entity Cinema (dữ liệu thô) sang DTO CinemaResponse để trả về cho Client
            .Select(cinema => new CinemaResponse
            {
                // Gán ID rạp chiếu phim
                CinemaId = cinema.CinemaId,
                // Gán tên rạp chiếu phim
                CinemaName = cinema.CinemaName,
                // Gán địa chỉ rạp chiếu phim
                Address = cinema.Address,
                // Gán thành phố nơi đặt rạp chiếu phim
                City = cinema.City,
                // Gán số điện thoại liên hệ của rạp
                PhoneNumber = cinema.PhoneNumber,
                // Gán trạng thái hoạt động hiện tại của rạp chiếu phim
                CinemaStatus = cinema.CinemaStatus
            })
            // Chạy bất đồng bộ và chuyển đổi kết quả thành danh sách (List)
            .ToListAsync(cancellationToken);

        // Trả về kết quả thành công chứa danh sách rạp chiếu phim và thông báo tương ứng
        return ServiceResult<IReadOnlyList<CinemaResponse>>.Ok(cinemas, "Cinemas retrieved successfully.");
    }
}
