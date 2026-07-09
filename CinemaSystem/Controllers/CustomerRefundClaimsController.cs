using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Refunds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/customer")]
[Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
public sealed class CustomerRefundClaimsController : ControllerBase
{
    private readonly IRefundClaimService _service;

    public CustomerRefundClaimsController(IRefundClaimService service)
    {
        _service = service;
    }

    [HttpGet("banks")]
    public async Task<IActionResult> GetBanks(CancellationToken cancellationToken)
        => ToActionResult(await _service.GetBanksAsync(cancellationToken));

    [HttpPost("refund-claims/resolve")]
    public async Task<IActionResult> Resolve(
        ResolveRefundClaimRequest request,
        CancellationToken cancellationToken)
        => await WithUserId(userId => _service.ResolveAsync(userId, request, cancellationToken));

    [HttpPut("refund-claims/{claimId}/bank-account")]
    public async Task<IActionResult> SaveBankAccount(
        string claimId,
        SaveRefundBankAccountRequest request,
        CancellationToken cancellationToken)
        => await WithUserId(userId => _service.SaveBankAccountAsync(userId, claimId, request, cancellationToken));

    [HttpPost("refund-claims/{claimId}/submit")]
    public async Task<IActionResult> Submit(
        string claimId,
        CancellationToken cancellationToken)
        => await WithUserId(userId => _service.SubmitAsync(userId, claimId, cancellationToken));

    [HttpPost("refund-requests")]
    public async Task<IActionResult> RequestNewLink(
        RequestRefundLinkRequest request,
        CancellationToken cancellationToken)
        => await WithUserId(userId => _service.RequestNewLinkAsync(userId, request, cancellationToken));

    private async Task<IActionResult> WithUserId<T>(
        Func<string, Task<ServiceResult<T>>> operation)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                "Unauthorized.",
                BookingConstants.ErrorCodes.Unauthorized));
        }
        return ToActionResult(await operation(userId));
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);
        return StatusCode(result.StatusCode, response);
    }
}
