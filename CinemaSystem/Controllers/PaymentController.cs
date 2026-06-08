using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // POST /api/payment
    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await _paymentService.CreatePaymentAsync(request, cancellationToken);
        return Ok(ApiResponse<CreatePaymentResponse>.Ok(response, "Payment created."));
    }

    // POST /api/payment/sepay-webhook
    [HttpPost("sepay-webhook")]
    public async Task<IActionResult> SepayWebhook(
        [FromServices] HmacVerifyHelper hmacHelper,
        [FromServices] IPaymentService paymentService,
        [FromBody] JsonElement payload,
        [FromHeader(Name = "x-sepay-signature")] string? signatureHeader = null,
        [FromHeader(Name = "x-sepay-timestamp")] string? timestampHeader = null)
    {
        var body = payload.GetRawText();

        // Get headers
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return Unauthorized(ApiResponse<object>.Fail("Missing SePay signature.", "INVALID_WEBHOOK_SIGNATURE"));
        if (string.IsNullOrWhiteSpace(timestampHeader))
            return Unauthorized(ApiResponse<object>.Fail("Missing SePay timestamp.", "INVALID_WEBHOOK_SIGNATURE"));

        // Verify HMAC
        if (!hmacHelper.Verify(signatureHeader, timestampHeader, body))
            return Unauthorized(ApiResponse<object>.Fail("Invalid SePay signature.", "INVALID_WEBHOOK_SIGNATURE"));

        // Deserialize
        var webhook = payload.Deserialize<SepayWebhookRequest>();
        if (webhook == null)
            return BadRequest(ApiResponse<object>.Fail("Invalid SePay webhook payload.", "INVALID_WEBHOOK_PAYLOAD"));

        // Confirm the payment
        await paymentService.ConfirmPaymentAsync(webhook.Content, webhook.Amount, webhook.ReferenceCode, body);

        return Ok(ApiResponse<object>.Ok(null, "Payment confirmed."));
    }
}
