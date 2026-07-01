// Import các interface của Application layer để dùng cho DI
using CinemaSystem.Application.Interfaces;
// Import Entity Framework Core để thao tác với cơ sở dữ liệu
using Microsoft.EntityFrameworkCore;

// Định nghĩa namespace cho các thành phần Persistence trong Infrastructure
namespace CinemaSystem.Infrastructure.Persistence;

// Lớp CinemaDiagnosticsService dùng để kiểm tra, chuẩn đoán thông tin hệ thống, kế thừa ICinemaDiagnosticsService
public sealed class CinemaDiagnosticsService : ICinemaDiagnosticsService
{
    // Biến private chỉ đọc để lưu trữ DbContext (kết nối với cơ sở dữ liệu)
    private readonly CinemaDbContext _dbContext;

    // Constructor nhận CinemaDbContext qua Dependency Injection (DI)
    public CinemaDiagnosticsService(CinemaDbContext dbContext)
    {
        // Gán DbContext từ DI vào biến private
        _dbContext = dbContext;
    }

    // Phương thức đếm tổng số bộ phim hiện có trong hệ thống một cách bất đồng bộ
    public Task<int> GetMoviesCountAsync(CancellationToken cancellationToken)
    {
        // Gọi phương thức CountAsync của Entity Framework để đếm số lượng bản ghi trong bảng Movies và trả về Task
        return _dbContext.Movies.CountAsync(cancellationToken);
    }
}
