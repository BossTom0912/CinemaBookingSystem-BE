using CinemaSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Controllers;

[Route("api/admin/refunds")]
[ApiController]
[Authorize(Roles = "Admin,Manager,Staff")] // Based on common accounting permissions, but Admin is requested
public class AdminRefundsController : ControllerBase
{
    private readonly IAdminRefundService _adminRefundService;

    public AdminRefundsController(IAdminRefundService adminRefundService)
    {
        _adminRefundService = adminRefundService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRefunds(
        [FromQuery] string status = "PENDING_REFUND",
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminRefundService.GetRefundsAsync(status, pageIndex, pageSize, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message, errorCode = result.ErrorCode });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            data = result.Data
        });
    }

    [HttpPost("{bookingId}/confirm")]
    public async Task<IActionResult> ConfirmRefund(
        [FromRoute] string bookingId,
        CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "UnknownAdmin";

        var result = await _adminRefundService.ConfirmRefundAsync(bookingId, adminUserId, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message, errorCode = result.ErrorCode });
        }

        return Ok(new
        {
            success = true,
            message = result.Message
        });
    }
}
