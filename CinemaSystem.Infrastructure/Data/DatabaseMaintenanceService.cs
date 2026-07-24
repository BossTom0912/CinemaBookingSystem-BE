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
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        // 1. Tự động bổ sung các cột mới cho bảng VOUCHER trước bằng Raw SQL để đảm bảo schema luôn đầy đủ
        const string sqlScript = """
            IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[VOUCHER]'))
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'category')
                    ALTER TABLE [VOUCHER] ADD [category] NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'applicableScope')
                    ALTER TABLE [VOUCHER] ADD [applicableScope] NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'targetType')
                    ALTER TABLE [VOUCHER] ADD [targetType] NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'targetCustomerIds')
                    ALTER TABLE [VOUCHER] ADD [targetCustomerIds] NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'specificFbItemIds')
                    ALTER TABLE [VOUCHER] ADD [specificFbItemIds] NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'isPrivate')
                    ALTER TABLE [VOUCHER] ADD [isPrivate] BIT NOT NULL CONSTRAINT [DF_VOUCHER_isPrivate] DEFAULT 0;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[VOUCHER]') AND name = 'requiredTicketCount')
                    ALTER TABLE [VOUCHER] ADD [requiredTicketCount] INT NULL;

                -- Gán giá trị mặc định cho các dòng dữ liệu voucher đã tồn tại trước đó
                UPDATE [VOUCHER] SET [category] = 'EVENT' WHERE [category] IS NULL;
                UPDATE [VOUCHER] SET [applicableScope] = 'TOTAL_ORDER' WHERE [applicableScope] IS NULL;
                UPDATE [VOUCHER] SET [targetType] = 'ALL_CUSTOMERS' WHERE [targetType] IS NULL;
            END
            """;

        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync(sqlScript, cancellationToken);
        }
        catch
        {
            // Ignore if DB is not ready yet
        }

        // 2. Chạy EF Core Migration
        try
        {
            await _dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch
        {
            // Ignore EF migration exceptions if schema is already up-to-date
        }
    }

    // Phương thức bất đồng bộ để tạo dữ liệu mẫu (Seed data) vào cơ sở dữ liệu ban đầu
    public Task SeedAsync(bool isDevelopment, CancellationToken cancellationToken = default)
    {
        // Chuyển giao công việc tạo dữ liệu mẫu cho lớp DbInitializer
        return DbInitializer.SeedAsync(_serviceProvider, isDevelopment);
    }
}
