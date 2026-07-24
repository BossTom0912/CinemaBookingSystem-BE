using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/payment")]
public sealed class VnPayController : ControllerBase
{
    private readonly IVnPayCallbackService _callbackService;

    public VnPayController(IVnPayCallbackService callbackService)
    {
        _callbackService = callbackService;
    }

    [HttpGet(ApiConstants.VnPayIpnRouteSegment)]
    public async Task<IActionResult> Ipn(CancellationToken cancellationToken)
    {
        var parameters = ReadQueryParameters();
        var response = await _callbackService.HandleIpnAsync(
            parameters,
            cancellationToken);
        return Ok(response);
    }

    [HttpGet(ApiConstants.VnPayReturnRouteSegment)]
    public IActionResult Return()
    {
        var parameters = ReadQueryParameters();
        if (!_callbackService.HasValidSignature(parameters))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Invalid VNPAY signature.",
                "VNPAY_INVALID_SIGNATURE"));
        }

        return Ok(ApiResponse<object>.Ok(
            new
            {
                transactionCode = parameters.GetValueOrDefault("vnp_TxnRef"),
                responseCode = parameters.GetValueOrDefault("vnp_ResponseCode"),
                transactionStatus = parameters.GetValueOrDefault("vnp_TransactionStatus")
            },
            "VNPAY returned to the merchant. Payment status is confirmed by IPN."));
    }

    private Dictionary<string, string> ReadQueryParameters()
    {
        return Request.Query.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToString(),
            StringComparer.Ordinal);
    }
}
