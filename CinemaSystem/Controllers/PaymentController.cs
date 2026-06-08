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
    public async Task<IActionResult> SepayWebhook([
        FromServices] HmacVerifyHelper hmacHelper,
        [FromServices] IPaymentService paymentService)
    {
        // Read raw body
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        // Get headers
        if (!Request.Headers.TryGetValue("x-sepay-signature", out var signature))
            return Unauthorized(ApiResponse<object>.Fail("Missing SePay signature.", "INVALID_WEBHOOK_SIGNATURE"));
        if (!Request.Headers.TryGetValue("x-sepay-timestamp", out var timestamp))
            return Unauthorized(ApiResponse<object>.Fail("Missing SePay timestamp.", "INVALID_WEBHOOK_SIGNATURE"));

        // Verify HMAC
        if (!hmacHelper.Verify(signature.ToString(), timestamp.ToString(), body))
            return Unauthorized(ApiResponse<object>.Fail("Invalid SePay signature.", "INVALID_WEBHOOK_SIGNATURE"));

        // Deserialize
        var payload = JsonSerializer.Deserialize<SepayWebhookRequest>(body);
        if (payload == null)
            return BadRequest(ApiResponse<object>.Fail("Invalid SePay webhook payload.", "INVALID_WEBHOOK_PAYLOAD"));

        // Confirm the payment
        await paymentService.ConfirmPaymentAsync(payload.Content, payload.Amount, payload.ReferenceCode, body);

        return Ok(ApiResponse<object>.Ok(null, "Payment confirmed."));
    }
}
