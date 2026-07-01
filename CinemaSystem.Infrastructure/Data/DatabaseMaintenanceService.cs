// Khai báo các interface được định nghĩa trong tầng Application
using CinemaSystem.Application.Interfaces;
// Sử dụng các lớp và cấu hình liên quan đến truy xuất dữ liệu trong tầng Infrastructure
using CinemaSystem.Infrastructure.Persistence;
// Sử dụng Entity Framework Core để thao tác và quản lý cơ sở dữ liệu
using Microsoft.EntityFrameworkCore;

// Khai báo namespace quản lý dữ liệu (Data) trong tầng Infrastructure
namespace CinemaSystem.Infrastructure.Data;

// Định nghĩa lớp DatabaseMaintenanceService triển khai interface IDatabaseMaintenanceService, đánh dấu sealed để ngăn kế thừa
public sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    // Biến private chỉ đọc để giữ kết nối với cơ sở dữ liệu qua CinemaDbContext
    private readonly CinemaDbContext _dbContext;
    // Biến private chỉ đọc để giữ tham chiếu tới IServiceProvider nhằm resolve các dependency khi cần
    private readonly IServiceProvider _serviceProvider;

    // Constructor khởi tạo DatabaseMaintenanceService với các dependency được tiêm vào (DI)
    public DatabaseMaintenanceService(CinemaDbContext dbContext, IServiceProvider serviceProvider)
    {
        // Gán dbContext được tiêm vào biến _dbContext
        _dbContext = dbContext;
        // Gán serviceProvider được tiêm vào biến _serviceProvider
        _serviceProvider = serviceProvider;
    }

    // Phương thức bất đồng bộ để thực thi các file Migration lên cơ sở dữ liệu
    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        // Gọi phương thức MigrateAsync của EF Core để tự động cập nhật Database schema lên phiên bản mới nhất
        return _dbContext.Database.MigrateAsync(cancellationToken);
    }

    // Phương thức bất đồng bộ để tạo dữ liệu mẫu (Seed data) vào cơ sở dữ liệu ban đầu
    public Task SeedAsync(bool isDevelopment, CancellationToken cancellationToken = default)
    {
        // Chuyển giao công việc tạo dữ liệu mẫu cho lớp DbInitializer
        return DbInitializer.SeedAsync(_serviceProvider, isDevelopment);
    }
}
