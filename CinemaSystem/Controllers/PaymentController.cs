using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CinemaSystem.Controllers;

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
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await _paymentService.CreatePaymentAsync(
            request.MapTo<Contracts.Payments.CreatePaymentRequest>(),
            cancellationToken);
        return Ok(ApiResponse<CreatePaymentResponse>.Ok(
            response.MapTo<CreatePaymentResponse>(),
            "Payment created."));
    }

    // POST /api/payment/sepay-webhook
    [HttpPost("sepay-webhook")]
    public async Task<IActionResult> SepayWebhook(
        [FromBody] JsonElement payload,
        [FromHeader(Name = "x-sepay-signature")] string? signatureHeader = null,
        [FromHeader(Name = "x-sepay-timestamp")] string? timestampHeader = null)
    {
        var body = payload.GetRawText();
        var result = await _paymentWebhookService.HandleSepayWebhookAsync(
            body,
            signatureHeader,
            timestampHeader,
            HttpContext.RequestAborted);

        var response = result.Success
            ? ApiResponse<object>.Ok(result.Data, result.Message)
            : ApiResponse<object>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
