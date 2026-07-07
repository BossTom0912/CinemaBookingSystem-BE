using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Policy = AuthConstants.Policies.CanManageShowtime)]
public sealed class AdminBookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public AdminBookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost("reassign-seat")]
    public async Task<IActionResult> ReassignSeat(
        [FromBody] ReassignSeatRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", "VALIDATION_ERROR"));
        }

        var result = await _bookingService.ReassignBookingSeatAsync(request, cancellationToken);

        var response = result.Success
            ? ApiResponse<bool>.Ok(result.Data, result.Message)
            : ApiResponse<bool>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
