using System.Collections.Generic;
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
[Route("api/admin/vouchers")]
[Authorize(Policy = AuthConstants.Policies.CanManageVoucher)]
public sealed class AdminVouchersController : ControllerBase
{
    private readonly IVoucherService _voucherService;

    public AdminVouchersController(IVoucherService voucherService)
    {
        _voucherService = voucherService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateVoucher(
        CreateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _voucherService.CreateVoucherAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetVouchers(
        [FromQuery] string? searchCode,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _voucherService.GetAllVouchersAsync(searchCode, status, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{voucherId}")]
    public async Task<IActionResult> GetVoucherById(
        string voucherId,
        CancellationToken cancellationToken)
    {
        var result = await _voucherService.GetVoucherByIdAsync(voucherId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{voucherId}")]
    public async Task<IActionResult> UpdateVoucher(
        string voucherId,
        UpdateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _voucherService.UpdateVoucherAsync(voucherId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{voucherId}")]
    public async Task<IActionResult> DeleteVoucher(
        string voucherId,
        CancellationToken cancellationToken)
    {
        var result = await _voucherService.DeleteVoucherAsync(voucherId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("issue-compensation")]
    public async Task<IActionResult> IssueCompensation(
        IssueCompensationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _voucherService.IssueCompensationVoucherAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("customers-by-showtime")]
    public async Task<IActionResult> GetCustomerIdsByShowtimeOrRoom(
        [FromQuery] string? showtimeId,
        [FromQuery] string? roomId,
        CancellationToken cancellationToken)
    {
        var result = await _voucherService.GetCustomerIdsByShowtimeOrRoomAsync(showtimeId, roomId, cancellationToken);
        return ToActionResult(result);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
