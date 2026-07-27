using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Refunds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/admin/banks")]
[Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
public sealed class AdminBankDirectoryController : ControllerBase
{
    private readonly IBankDirectoryAdminService _service;

    public AdminBankDirectoryController(IBankDirectoryAdminService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => ToActionResult(await _service.GetAllAsync(cancellationToken));

    [HttpPut("{bankCode}")]
    public async Task<IActionResult> Upsert(
        string bankCode,
        UpsertBankDirectoryRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await _service.UpsertAsync(bankCode, request, cancellationToken));

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);
        return StatusCode(result.StatusCode, response);
    }
}
