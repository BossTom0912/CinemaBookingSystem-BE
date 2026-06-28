using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// SePay webhook orchestration reached from
/// <c>PaymentController.SepayWebhook</c>.
/// </summary>
/// <remarks>
/// The service validates required headers, delegates HMAC verification to
/// <see cref="IWebhookSignatureVerifier"/>, deserializes the provider payload,
/// then hands the state-changing transaction to
/// <see cref="IPaymentService.ConfirmPaymentAsync"/>. It does not write the
/// database directly.
/// </remarks>
public sealed class PaymentWebhookService : IPaymentWebhookService
{
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly IPaymentService _paymentService;

    public PaymentWebhookService(
        IWebhookSignatureVerifier signatureVerifier,
        IPaymentService paymentService)
    {
        _signatureVerifier = signatureVerifier;
        _paymentService = paymentService;
    }

    public async Task<ServiceResult<object>> HandleSepayWebhookAsync(
        string payload,
        string? signatureHeader,
        string? timestampHeader,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return ServiceResult<object>.Fail(
                401,
                "Missing SePay signature.",
                "INVALID_SIGNATURE");
        }

        if (string.IsNullOrWhiteSpace(timestampHeader))
        {
            return ServiceResult<object>.Fail(
                401,
                "Missing SePay timestamp.",
                "INVALID_SIGNATURE");
        }

        // Chặng tiếp theo: IWebhookSignatureVerifier được DI map sang
        // HmacVerifyHelper trong Infrastructure/Services. Tách verifier để
        // PaymentWebhookService chỉ điều phối và có thể test độc lập HMAC.
        if (!_signatureVerifier.Verify(signatureHeader, timestampHeader, payload))
        {
            return ServiceResult<object>.Fail(
                401,
                "Invalid SePay signature.",
                "INVALID_SIGNATURE");
        }

        var webhook = JsonSerializer.Deserialize<SepayWebhookRequest>(payload);
        if (webhook is null)
        {
            return ServiceResult<object>.Fail(
                400,
                "Invalid SePay webhook payload.",
                "INVALID_WEBHOOK_PAYLOAD");
        }

        // Chặng tiếp theo: IPaymentService -> PaymentService trong cùng folder.
        // PaymentService mới là class sở hữu transaction thay đổi PAYMENT,
        // BOOKING, SHOWTIME_SEAT và TICKET; webhook service không ghi DB trực tiếp.
        await _paymentService.ConfirmPaymentAsync(
            webhook.Content,
            webhook.Amount,
            webhook.ReferenceCode,
            payload,
            cancellationToken);

        // PaymentService commit xong thì kết quả quay qua PaymentController để
        // trả ACK cho SePay; nếu service ném lỗi, GlobalExceptionMiddleware xử lý.
        return ServiceResult<object>.Ok(null, "Payment confirmed.");
    }
}
