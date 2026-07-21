using CinemaSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Controllers;

using CinemaSystem.Application.Common;

/// <summary>
/// Điểm vào HTTP để Admin xem danh sách hoàn tiền và xác nhận hoàn tiền.
/// </summary>
/// <remarks>
/// Luồng tiếp theo: <see cref="IAdminRefundService"/> được DI ánh xạ tới
/// <c>CinemaSystem.Infrastructure/Services/AdminRefundService.cs</c>. Service
/// xử lý BOOKING, PAYMENT, REFUND, TICKET và gửi email nền; kết quả quay lại
/// controller để chuyển thành HTTP response.
/// </remarks>
[Route("api/admin/refunds")]
[ApiController]
[Authorize(Roles = AuthConstants.Roles.Admin)] // Restrict refund operations to Admin only
public class AdminRefundsController : ControllerBase
{
    private readonly IAdminRefundService _adminRefundService;
    private readonly IManualRefundService _manualRefundService;
    private readonly IRefundCustomerConfirmationService _customerConfirmationService;

    public AdminRefundsController(
        IAdminRefundService adminRefundService,
        IManualRefundService manualRefundService,
        IRefundCustomerConfirmationService customerConfirmationService)
    {
        _adminRefundService = adminRefundService;
        _manualRefundService = manualRefundService;
        _customerConfirmationService = customerConfirmationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CinemaSystem.Contracts.Common.ApiResponse<CinemaSystem.Contracts.Common.PagedList<CinemaSystem.Application.Interfaces.RefundDto>>), 200)]
    public async Task<ActionResult<CinemaSystem.Contracts.Common.ApiResponse<CinemaSystem.Contracts.Common.PagedList<CinemaSystem.Application.Interfaces.RefundDto>>>> GetRefunds(
        [FromQuery] string status = DomainConstants.RefundStatus.Pending,
        [FromQuery] int pageIndex = PaginationDefaults.FirstPageIndex,
        [FromQuery] int pageSize = PaginationDefaults.DefaultPageSize,
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

    [HttpGet("manual")]
    public async Task<IActionResult> GetManualRefunds(CancellationToken cancellationToken)
    {
        return ToActionResult(await _manualRefundService.GetPendingAsync(cancellationToken));
    }

    [HttpPost("{refundId}/assign")]
    public async Task<IActionResult> Assign(
        string refundId,
        CancellationToken cancellationToken)
    {
        return await WithAdminId(userId =>
            _manualRefundService.AssignAsync(refundId, userId, cancellationToken));
    }

    [HttpPost("{refundId}/manual-confirm")]
    public async Task<IActionResult> ConfirmManualRefund(
        string refundId,
        ManualRefundConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        return await WithAdminId(userId =>
            _manualRefundService.ConfirmAsync(
                refundId,
                userId,
                request,
                cancellationToken));
    }

    [HttpPost("{refundId}/request-customer-confirmation")]
    public async Task<IActionResult> RequestCustomerConfirmation(
        string refundId,
        CancellationToken cancellationToken)
    {
        return await WithAdminId(userId =>
            _customerConfirmationService.SendAsync(refundId, userId, cancellationToken));
    }

    private async Task<IActionResult> WithAdminId<T>(
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
