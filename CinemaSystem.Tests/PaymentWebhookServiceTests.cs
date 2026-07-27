using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Services;
using Moq;

namespace CinemaSystem.Tests;

public sealed class PaymentWebhookServiceTests
{
    [Fact]
    public async Task HandleSepayWebhook_ConvertsVietnamTransactionDateToUtc()
    {
        const string payload =
            """{"transactionDate":"2026-07-27 20:08:00","content":"T8QBA0UKU84","transferAmount":100000,"referenceCode":"FT26208511507831"}""";
        var verifier = new Mock<IWebhookSignatureVerifier>(MockBehavior.Strict);
        verifier
            .Setup(item => item.Verify("signature", "timestamp", payload))
            .Returns(true);
        var paymentService = new Mock<IPaymentService>(MockBehavior.Strict);
        paymentService
            .Setup(item => item.ConfirmPaymentAsync(
                "T8QBA0UKU84",
                100000m,
                "FT26208511507831",
                payload,
                It.IsAny<CancellationToken>(),
                new DateTime(2026, 7, 27, 13, 8, 0, DateTimeKind.Utc)))
            .Returns(Task.CompletedTask);
        var service = new PaymentWebhookService(verifier.Object, paymentService.Object);

        var result = await service.HandleSepayWebhookAsync(
            payload,
            "signature",
            "timestamp",
            CancellationToken.None);

        Assert.True(result.Success);
        paymentService.VerifyAll();
        verifier.VerifyAll();
    }

    [Fact]
    public async Task HandleSepayWebhook_InvalidTransactionDate_ReturnsBadRequest()
    {
        const string payload =
            """{"transactionDate":"not-a-date","content":"T8QBA0UKU84","transferAmount":100000,"referenceCode":"FT26208511507831"}""";
        var verifier = new Mock<IWebhookSignatureVerifier>(MockBehavior.Strict);
        verifier
            .Setup(item => item.Verify("signature", "timestamp", payload))
            .Returns(true);
        var paymentService = new Mock<IPaymentService>(MockBehavior.Strict);
        var service = new PaymentWebhookService(verifier.Object, paymentService.Object);

        var result = await service.HandleSepayWebhookAsync(
            payload,
            "signature",
            "timestamp",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_WEBHOOK_PAYLOAD", result.ErrorCode);
        paymentService.VerifyNoOtherCalls();
    }
}
