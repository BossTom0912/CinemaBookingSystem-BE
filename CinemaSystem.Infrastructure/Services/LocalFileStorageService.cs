using CinemaSystem.Application.Interfaces;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    // Hàm khởi tạo không đối số
    public LocalFileStorageService()
    {
    }

    // Hàm thực hiện chức năng lưu trữ một tập tin (File) cục bộ lên hệ thống thư mục server
    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName, CancellationToken cancellationToken)
    {
        // Kiểm tra tính hợp lệ của dòng luồng (stream), nếu null hoặc trống thì từ chối lưu
        if (fileStream == null || fileStream.Length == 0)
            // Trả về chuỗi định dạng URL trống do file không hợp lệ
            return string.Empty;

        // Lấy ra đường dẫn gốc cấp thư mục hiện hành và ghép thêm vị trí wwwroot
        var wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        // Ghép thư mục đích nằm trong uploads và thư mục phân loại riêng (folderName)
        var uploadsFolder = Path.Combine(wwwRootPath, "uploads", folderName);
        
        // Kiểm tra xem vị trí thư mục đích này đã được tạo vật lý trên ổ đĩa hay chưa
        if (!Directory.Exists(uploadsFolder))
        {
            // Tiến hành khởi tạo thư mục mới nhằm sẵn sàng lưu file
            Directory.CreateDirectory(uploadsFolder);
        }

        // Dùng định danh Guid và kết hợp đuôi file nhằm sinh ra tên duy nhất, chống ghi đè do trùng lặp
        var uniqueFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(fileName);
        // Thiết lập đường dẫn đầy đủ đến vị trí chính xác của tập tin
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        // Mở một kết nối dòng lệnh ghi file tại đường dẫn với chế độ Tạo mới (Create)
        using (var outputStream = new FileStream(filePath, FileMode.Create))
        {
            // Thực thi việc copy nội dung dữ liệu từ Stream yêu cầu vào cấu trúc File cục bộ
            await fileStream.CopyToAsync(outputStream, cancellationToken);
        }

        // Kết thúc hàm và trả về một đường link tương đối sử dụng để truy cập nội dung vừa lưu qua nền tảng HTTP
        return $"/uploads/{folderName}/{uniqueFileName}";
    }

    // Hàm thực hiện xóa một tập tin khi chỉ định địa chỉ liên kết (URL)
    public Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken)
    {
        // Kiểm tra nếu URL không chứa nội dung văn bản nào
        if (string.IsNullOrWhiteSpace(fileUrl))
            // Chấm dứt tác vụ tức thời và xem như hoàn thành do không có yêu cầu cụ thể
            return Task.CompletedTask;

        // Tạo ra đường dẫn hệ thống để chỉ tới thư mục wwwroot
        var wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        // Giải mã URL và dùng logic định tuyến đổi ngược cấu trúc slash (/) sang dạng nhận dạng đường dẫn trên OS 
        var filePath = Path.Combine(wwwRootPath, fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        
        // Kiểm tra một tập tin tại đường dẫn trên có đang trực tiếp tồn tại trên hệ thống hay không
        if (File.Exists(filePath))
        {
            // Xóa triệt để tập tin để giải phóng tài nguyên lưu trữ
            File.Delete(filePath);
        }

        // Trả về thông báo thành công dưới dạng một tiến trình đồng bộ hoàn tất
        return Task.CompletedTask;
    }
}
