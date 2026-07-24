namespace CinemaSystem;

public static class ApiConstants
{
    public const string FrontendCorsPolicy = "FrontendCors";
    public const string SepayWebhookRouteSegment = "sepay-webhook";
    public const string SepayWebhookPath = "/api/payment/" + SepayWebhookRouteSegment;
    public const string SepayWebhookRelativePath = "api/Payment/" + SepayWebhookRouteSegment;
    public const string VnPayIpnRouteSegment = "vnpay-ipn";
    public const string VnPayReturnRouteSegment = "vnpay-return";
}
