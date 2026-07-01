using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Cổng xử lý callback SePay: xác thực chữ ký, đọc payload và chuyển giao thanh toán.
/// </summary>
/// <remarks>
/// Nhận payload từ <c>CinemaSystem/Controllers/PaymentController.cs</c>, gọi
/// <see cref="IWebhookSignatureVerifier"/>, sau đó gọi
/// <see cref="IPaymentService.ConfirmPaymentAsync"/> trong PaymentService.
/// </remarks>
public sealed class PaymentWebhookService : IPaymentWebhookService
{
    // Khai báo dependency để xác thực chữ ký của webhook
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    // Khai báo dependency để gọi các nghiệp vụ xử lý thanh toán
    private readonly IPaymentService _paymentService;

    // Khởi tạo service với các dependency injection được truyền vào
    public PaymentWebhookService(
        IWebhookSignatureVerifier signatureVerifier,
        IPaymentService paymentService)
    {
        // Gán bộ xác thực chữ ký vào biến readonly
        _signatureVerifier = signatureVerifier;
        // Gán service thanh toán vào biến readonly
        _paymentService = paymentService;
    }

    // Phương thức xử lý webhook gửi về từ SePay
    public async Task<ServiceResult<object>> HandleSepayWebhookAsync(
        string payload,
        string? signatureHeader,
        string? timestampHeader,
        CancellationToken cancellationToken)
    {
        // Kiểm tra xem tiêu đề (header) chữ ký có bị trống không
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            // Trả về kết quả thất bại (401) nếu thiếu chữ ký SePay
            return ServiceResult<object>.Fail(
                401,
                "Missing SePay signature.",
                "INVALID_WEBHOOK_SIGNATURE");
        }

        // Kiểm tra xem tiêu đề thời gian (timestamp) có bị trống không
        if (string.IsNullOrWhiteSpace(timestampHeader))
        {
            // Trả về kết quả thất bại (401) nếu thiếu thời gian của SePay
            return ServiceResult<object>.Fail(
                401,
                "Missing SePay timestamp.",
                "INVALID_WEBHOOK_SIGNATURE");
        }

        // Thực hiện xác thực nội dung payload có khớp với chữ ký và thời gian không
        if (!_signatureVerifier.Verify(signatureHeader, timestampHeader, payload))
        {
            // Trả về kết quả thất bại (401) nếu chữ ký không hợp lệ
            return ServiceResult<object>.Fail(
                401,
                "Invalid SePay signature.",
                "INVALID_WEBHOOK_SIGNATURE");
        }

        // Giải mã chuỗi JSON (payload) thành đối tượng SepayWebhookRequest
        var webhook = JsonSerializer.Deserialize<SepayWebhookRequest>(payload);
        // Nếu kết quả giải mã là null (chuỗi JSON không đúng cấu trúc)
        if (webhook is null)
        {
            // Trả về lỗi định dạng (400) do dữ liệu webhook không hợp lệ
            return ServiceResult<object>.Fail(
                400,
                "Invalid SePay webhook payload.",
                "INVALID_WEBHOOK_PAYLOAD");
        }

        // Chuyển tiếp dữ liệu thanh toán đến PaymentService để tiến hành xác nhận (Confirm)
        await _paymentService.ConfirmPaymentAsync(
            // Nội dung chuyển khoản chứa mã giao dịch
            webhook.Content,
            // Số tiền được chuyển
            webhook.Amount,
            // Mã giao dịch từ phía SePay cung cấp (nếu có)
            webhook.ReferenceCode,
            // Toàn bộ chuỗi payload gốc để lưu lại log
            payload,
            // Token hỗ trợ hủy bỏ tác vụ
            cancellationToken);

        // Trả về phản hồi thành công sau khi xác nhận thanh toán xong
        return ServiceResult<object>.Ok(null, "Payment confirmed.");
    }
}
