using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly SepaySettings _sepaySettings;

    public PaymentController(IPaymentService paymentService, IOptions<SepaySettings> sepayOptions)
    {
        _paymentService = paymentService;
        _sepaySettings = sepayOptions.Value;
    }

    // POST /api/payment
    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await _paymentService.CreatePaymentAsync(request, cancellationToken);
        return Ok(response);
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
            return Unauthorized();
        if (!Request.Headers.TryGetValue("x-sepay-timestamp", out var timestamp))
            return Unauthorized();

        // Verify HMAC
        if (!hmacHelper.Verify(signature, timestamp, body))
            return Unauthorized();

        // Deserialize
        var payload = System.Text.Json.JsonSerializer.Deserialize<SepayWebhookRequest>(body);
        if (payload == null)
            return BadRequest();

        // Confirm the payment
        await paymentService.ConfirmPaymentAsync(payload.Content, payload.Amount);

        return Ok();
    }
}
