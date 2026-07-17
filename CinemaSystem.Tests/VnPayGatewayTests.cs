using System.Web;
using CinemaSystem.Application.Common;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Tests;

public sealed class VnPayGatewayTests
{
    private const string TestHashSecret = "test-vnpay-hash-secret-123456";

    [Fact]
    public void CreateCheckoutUrl_BuildsSignedInternationalCardRequest()
    {
        var gateway = CreateGateway();
        var url = gateway.CreateCheckoutUrl(new PaymentGatewayCheckoutRequest(
            "PAY_TEST",
            "T1234567890",
            120000m,
            new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 17, 8, 10, 0, DateTimeKind.Utc),
            "203.0.113.10"));

        var parameters = ParseQuery(url);

        Assert.StartsWith(
            "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?",
            url,
            StringComparison.Ordinal);
        Assert.Equal("12000000", parameters["vnp_Amount"]);
        Assert.Equal("INTCARD", parameters["vnp_BankCode"]);
        Assert.Equal("20260717150000", parameters["vnp_CreateDate"]);
        Assert.Equal("20260717151000", parameters["vnp_ExpireDate"]);
        Assert.Equal("T1234567890", parameters["vnp_TxnRef"]);
        Assert.Equal("https://merchant.test/api/payment/vnpay-return", parameters["vnp_ReturnUrl"]);
        Assert.Contains("vnp_OrderInfo=Cinema%20booking%20PAY_TEST", url, StringComparison.Ordinal);
        Assert.True(gateway.HasValidSignature(parameters));
        Assert.DoesNotContain(TestHashSecret, url, StringComparison.Ordinal);
    }

    [Fact]
    public void HasValidSignature_WhenSignedValueChanges_ReturnsFalse()
    {
        var gateway = CreateGateway();
        var url = gateway.CreateCheckoutUrl(new PaymentGatewayCheckoutRequest(
            "PAY_TEST",
            "T1234567890",
            120000m,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(10),
            "203.0.113.10"));
        var parameters = ParseQuery(url);
        parameters["vnp_Amount"] = "100";

        Assert.False(gateway.HasValidSignature(parameters));
    }

    private static VnPayGateway CreateGateway()
    {
        return new VnPayGateway(Options.Create(new VnPaySettings
        {
            Enabled = true,
            TmnCode = "TEST_TMN",
            HashSecret = TestHashSecret,
            PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            ReturnUrl = "https://merchant.test/api/payment/vnpay-return",
            PreferredBankCode = "INTCARD",
            Locale = "vn",
            OrderType = "other"
        }));
    }

    private static Dictionary<string, string> ParseQuery(string url)
    {
        return new Uri(url).Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => HttpUtility.UrlDecode(part[0]),
                part => HttpUtility.UrlDecode(part[1]),
                StringComparer.Ordinal);
    }
}
