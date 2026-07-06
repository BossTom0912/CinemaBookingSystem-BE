using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.FoodAndBeverage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// API Controller for Food & Beverage (F&B) Catalogue, Branch Inventory, Counter POS Sales, and Fulfillment.
/// </summary>
[ApiController]
[Route("api/fb-items")]
public sealed class FbItemsController : ControllerBase
{
    private readonly IFbItemService _fbItemService;

    public FbItemsController(IFbItemService fbItemService)
    {
        _fbItemService = fbItemService;
    }

    /// <summary>
    /// Public endpoint to retrieve active F&B items.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllActive(CancellationToken cancellationToken)
    {
        var result = await _fbItemService.GetAllActiveAsync(cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Admin endpoint to retrieve all F&B catalog items.
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    public async Task<IActionResult> GetAllForAdmin(CancellationToken cancellationToken)
    {
        var result = await _fbItemService.GetAllForAdminAsync(cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get details of a single F&B item.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await _fbItemService.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Create a new master F&B catalog item (Central Admin/Manager).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    public async Task<IActionResult> Create(CreateFbItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _fbItemService.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Update a master F&B catalog item (Central Admin/Manager).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    public async Task<IActionResult> Update(string id, UpdateFbItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _fbItemService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Soft delete/deactivate an F&B item (Central Admin/Manager).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    public async Task<IActionResult> Deactivate(string id, CancellationToken cancellationToken)
    {
        var result = await _fbItemService.DeactivateAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get F&B inventory stock for a specific cinema branch.
    /// </summary>
    [HttpGet("cinemas/{cinemaId}/inventory")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCinemaInventory(string cinemaId, CancellationToken cancellationToken)
    {
        var result = await _fbItemService.GetCinemaInventoryAsync(cinemaId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Update stock level for a cinema branch (Branch Staff / Branch Manager restricted to assigned cinema).
    /// </summary>
    [HttpPut("cinemas/inventory")]
    [Authorize]
    public async Task<IActionResult> UpdateCinemaInventory(UpdateCinemaFbInventoryRequest request, CancellationToken cancellationToken)
    {
        var staffCinemaId = GetStaffCinemaId();
        var isSystemAdmin = User.IsInRole(AuthConstants.Roles.Admin) || User.HasClaim(ClaimTypes.Role, AuthConstants.Roles.Admin);

        var result = await _fbItemService.UpdateCinemaInventoryAsync(request, staffCinemaId, isSystemAdmin, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Process direct counter POS sale for F&B items (Branch Staff / POS).
    /// </summary>
    [HttpPost("counter-orders")]
    [Authorize(Policy = AuthConstants.Policies.CanScanTicket)]
    public async Task<IActionResult> CreateCounterOrder(CreateCounterFbOrderRequest request, CancellationToken cancellationToken)
    {
        var staffProfileId = GetStaffProfileId() ?? GetUserId() ?? "STAFF_UNKNOWN";
        var staffCinemaId = GetStaffCinemaId() ?? request.CinemaId;

        var result = await _fbItemService.CreateCounterOrderAsync(request, staffProfileId, staffCinemaId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Scan QR code & fulfill pre-ordered F&B items (Anti-fraud verification).
    /// </summary>
    [HttpPatch("bookings/{bookingId}/fulfill")]
    [Authorize(Policy = AuthConstants.Policies.CanScanTicket)]
    public async Task<IActionResult> FulfillOrder(string bookingId, CancellationToken cancellationToken)
    {
        var staffProfileId = GetStaffProfileId() ?? GetUserId() ?? "STAFF_UNKNOWN";
        var staffCinemaId = GetStaffCinemaId() ?? string.Empty;

        var result = await _fbItemService.FulfillOrderAsync(bookingId, staffProfileId, staffCinemaId, cancellationToken);
        return ToActionResult(result);
    }

    private string? GetUserId() => User.FindFirst("userId")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    private string? GetStaffProfileId() => User.FindFirst("staffProfileId")?.Value;
    private string? GetStaffCinemaId() => User.FindFirst("cinemaId")?.Value;

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
