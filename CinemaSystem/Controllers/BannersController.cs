using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Banners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Điểm vào HTTP để quản lý Banner của rạp (quảng cáo, bắp nước, sự kiện).
/// </summary>
[ApiController]
[Route("api/banners")]
public sealed class BannersController : ControllerBase
{
    private readonly IBannerService _bannerService;

    public BannersController(IBannerService bannerService)
    {
        _bannerService = bannerService ?? throw new ArgumentNullException(nameof(bannerService));
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<BannerResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveBanners(CancellationToken cancellationToken)
    {
        var result = await _bannerService.GetActiveBannersAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("all")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [ProducesResponseType(typeof(ApiResponse<List<BannerResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllBanners(CancellationToken cancellationToken)
    {
        var result = await _bannerService.GetAllBannersAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{bannerId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BannerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBannerById(string bannerId, CancellationToken cancellationToken)
    {
        var result = await _bannerService.GetBannerByIdAsync(bannerId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<BannerResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBanner(
        [FromForm] CreateBannerRequest request,
        IFormFile? bannerFile,
        CancellationToken cancellationToken)
    {
        using var stream = bannerFile?.OpenReadStream();
        var result = await _bannerService.CreateBannerAsync(request, stream, bannerFile?.FileName, cancellationToken);
        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, ApiResponse<BannerResponse>.Ok(result.Data, result.Message));
        }
        return ToActionResult(result);
    }

    [HttpPut("{bannerId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<BannerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBanner(
        string bannerId,
        [FromForm] UpdateBannerRequest request,
        IFormFile? bannerFile,
        CancellationToken cancellationToken)
    {
        using var stream = bannerFile?.OpenReadStream();
        var result = await _bannerService.UpdateBannerAsync(bannerId, request, stream, bannerFile?.FileName, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{bannerId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteBanner(string bannerId, CancellationToken cancellationToken)
    {
        var result = await _bannerService.DeleteBannerAsync(bannerId, cancellationToken);
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
