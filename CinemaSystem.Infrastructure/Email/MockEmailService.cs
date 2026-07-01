// Import các interface của Application layer để dùng cho DI
using CinemaSystem.Application.Interfaces;
// Import thư viện logging của Microsoft để ghi log
using Microsoft.Extensions.Logging;

// Định nghĩa namespace cho các thành phần liên quan đến Email trong Infrastructure
namespace CinemaSystem.Infrastructure.Email;

// Lớp MockEmailService kế thừa từ IEmailSender và IEmailService để giả lập việc gửi email (dùng cho môi trường dev/test)
public sealed class MockEmailService : IEmailSender, IEmailService
{
    // Biến private chỉ đọc để lưu trữ Logger
    private readonly ILogger<MockEmailService> _logger;

    // Constructor nhận Logger qua Dependency Injection (DI)
    public MockEmailService(ILogger<MockEmailService> logger)
    {
        // Gán Logger từ DI vào biến private
        _logger = logger;
    }

    // Phương thức gửi email bất đồng bộ (giả lập)
    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        // Ghi log thông tin email bao gồm người nhận, tiêu đề và nội dung
        _logger.LogInformation(
            // Định dạng chuỗi log
            "[MockEmail] To={ToEmail} Subject={Subject}\n{Body}",
            // Truyền tham số email người nhận
            toEmail,
            // Truyền tham số tiêu đề email
            subject,
            // Truyền tham số nội dung email
            body);
        // In thông tin người nhận ra cửa sổ Console
        Console.WriteLine($"[MockEmail] To: {toEmail}");
        // In thông tin tiêu đề ra cửa sổ Console
        Console.WriteLine($"[MockEmail] Subject: {subject}");
        // In nội dung email ra cửa sổ Console
        Console.WriteLine($"[MockEmail] Body:\n{body}");
        // Trả về một Task hoàn thành ngay lập tức vì đây chỉ là giả lập, không gửi thật
        return Task.CompletedTask;
    }

    // Phương thức gửi email mời nhân viên bất đồng bộ (giả lập)
    public Task SendInvitationAsync(string toEmail, string invitationToken, CancellationToken cancellationToken)
    {
        // Tạo chuỗi nội dung email mời tham gia hệ thống
        var body = $"""
            Hello,

            You have been invited to join Cinema Booking as staff.

            Use this invitation code to set your password: {invitationToken}

            If you did not expect this invitation, please ignore this email.
            """;

        // Gọi lại phương thức SendEmailAsync để in thông tin ra log/console thay vì gửi thật
        return SendEmailAsync(
            // Gửi đến email người nhận
            toEmail,
            // Đặt tiêu đề cho email là thư mời nhân viên
            "Cinema Booking - Staff Invitation",
            // Đưa nội dung đã tạo vào tham số body
            body,
            // Chuyển tiếp CancellationToken
            cancellationToken);
    }
}
