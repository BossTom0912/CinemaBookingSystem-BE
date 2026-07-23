using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Vouchers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/vouchers")]
[Authorize(Policy = AuthConstants.Policies.CanApplyVoucher)]
public sealed class VouchersController : ControllerBase
{
    private readonly IVoucherService _voucherService;

    public VouchersController(IVoucherService voucherService)
    {
        _voucherService = voucherService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveVouchers(CancellationToken cancellationToken)
    {
        var result = await _voucherService.GetActiveVouchersForCustomerAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicVouchers(CancellationToken cancellationToken)
    {
        var result = await _voucherService.GetActiveVouchersForCustomerAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("validate")]
    public async Task<IActionResult> ValidateVoucher(
        [FromQuery] string code,
        [FromQuery] decimal bookingAmount,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _voucherService.ValidateVoucherForCustomerAsync(code, bookingAmount, userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{voucherId}/claim")]
    public async Task<IActionResult> ClaimVoucher(
        string voucherId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _voucherService.ClaimVoucherForCustomerAsync(voucherId, userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("my-wallet")]
    public async Task<IActionResult> GetMyWallet(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _voucherService.GetMyVouchersAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    private string? GetUserId()
    {
        return User.FindFirst(AuthConstants.Claims.UserId)?.Value
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
