using System.Security.Claims;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Customer payment creation and SePay webhook HTTP entry point.
/// </summary>
/// <remarks>
/// Payment creation continues through <see cref="IPaymentService"/> to
/// <c>CinemaSystem.Infrastructure.Services.PaymentService</c>. Webhooks first
/// continue through <see cref="IPaymentWebhookService"/> to
/// <c>PaymentWebhookService</c>, then signature verification calls back into
/// <c>PaymentService.ConfirmPaymentAsync</c> to update PAYMENT, BOOKING,
/// SHOWTIME_SEAT and TICKET in a transaction.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentWebhookService _paymentWebhookService;

    public PaymentController(
        IPaymentService paymentService,
        IPaymentWebhookService paymentWebhookService)
    {
        _paymentService = paymentService;
        _paymentWebhookService = paymentWebhookService;
    }

    // POST /api/payment
    [HttpPost]
    [Authorize(Policy = AuthConstants.Policies.CanPayOnline)]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized.", "UNAUTHORIZED"));
        }

        // Bước tiếp theo: IPaymentService được DI map sang PaymentService tại
        // CinemaSystem.Infrastructure/Services/PaymentService.cs. Service kiểm
        // chủ booking/provider, tạo hoặc tái sử dụng PAYMENT PENDING rồi trả
        // thông tin chuyển khoản SePay.
        var response = await _paymentService.CreatePaymentAsync(
            request.MapTo<Contracts.Payments.CreatePaymentRequest>(),
            userId,
            cancellationToken);

        // PaymentService xử lý DB xong thì DTO quay lại đây. Bước nghiệp vụ sau
        // đó không ở request này: SePay sẽ gọi endpoint webhook bên dưới.
        return Ok(ApiResponse<CreatePaymentResponse>.Ok(
            response.MapTo<CreatePaymentResponse>(),
            "Payment created."));
    }

    // POST /api/payment/sepay-webhook
    [HttpPost(ApiConstants.SepayWebhookRouteSegment)]
    public async Task<IActionResult> SepayWebhook(
        [FromBody] JsonElement payload,
        [FromHeader(Name = "x-sepay-signature")] string? signatureHeader = null,
        [FromHeader(Name = "x-sepay-timestamp")] string? timestampHeader = null)
    {
        var body = payload.GetRawText();

        // Bước tiếp theo 1: IPaymentWebhookService -> PaymentWebhookService tại
        // Infrastructure/Services để kiểm header, parse payload và gọi
        // IWebhookSignatureVerifier/HmacVerifyHelper.
        // Bước tiếp theo 2: nếu chữ ký hợp lệ, PaymentWebhookService gọi lại
        // IPaymentService -> PaymentService.ConfirmPaymentAsync để transaction
        // PAYMENT -> BOOKING -> SHOWTIME_SEAT -> TICKET.
        var result = await _paymentWebhookService.HandleSepayWebhookAsync(
            body,
            signatureHeader,
            timestampHeader,
            HttpContext.RequestAborted);

        // Toàn bộ chuỗi xác minh/cập nhật DB xong mới quay lại Controller để ACK
        // webhook bằng HTTP status; Controller không tự xác nhận thanh toán.
        var response = result.Success
            ? ApiResponse<object>.Ok(result.Data, result.Message)
            : ApiResponse<object>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    private string? GetUserId()
    {
        return User.FindFirst("userId")?.Value
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
