using CinemaSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Controllers;

using CinemaSystem.Application.Common;

[Route("api/admin/refunds")]
[ApiController]
[Authorize(Roles = AuthConstants.Roles.Admin)] // Restrict refund operations to Admin only
public class AdminRefundsController : ControllerBase
{
    private readonly IAdminRefundService _adminRefundService;

    public AdminRefundsController(IAdminRefundService adminRefundService)
    {
        _adminRefundService = adminRefundService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CinemaSystem.Contracts.Common.ApiResponse<CinemaSystem.Contracts.Common.PagedList<CinemaSystem.Application.Interfaces.RefundDto>>), 200)]
    public async Task<ActionResult<CinemaSystem.Contracts.Common.ApiResponse<CinemaSystem.Contracts.Common.PagedList<CinemaSystem.Application.Interfaces.RefundDto>>>> GetRefunds(
        [FromQuery] string status = "PENDING",
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminRefundService.GetRefundsAsync(status, pageIndex, pageSize, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message, errorCode = result.ErrorCode });
        }

        return Ok(new CinemaSystem.Contracts.Common.ApiResponse<CinemaSystem.Contracts.Common.PagedList<CinemaSystem.Application.Interfaces.RefundDto>>
        {
            Success = true,
            Message = result.Message,
            Data = result.Data
        });
    }

    [HttpPost("{bookingId}/confirm")]
    [ProducesResponseType(typeof(CinemaSystem.Contracts.Common.ApiResponse<object>), 200)]
    public async Task<ActionResult<CinemaSystem.Contracts.Common.ApiResponse<object>>> ConfirmRefund(
        [FromRoute] string bookingId,
        CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "UnknownAdmin";

        var result = await _adminRefundService.ConfirmRefundAsync(bookingId, adminUserId, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message, errorCode = result.ErrorCode });
        }

        return Ok(new CinemaSystem.Contracts.Common.ApiResponse<object>
        {
            Success = true,
            Message = result.Message
        });
    }
}
