using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Banners;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Banners;

public sealed class BannerService : IBannerService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly FileStorageSettings _fileStorageSettings;

    public BannerService(
        CinemaDbContext dbContext,
        IFileStorageService fileStorageService,
        IOptions<FileStorageSettings> fileStorageOptions)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _fileStorageSettings = fileStorageOptions.Value;
    }

    public async Task<ServiceResult<List<BannerResponse>>> GetActiveBannersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var banners = await _dbContext.Banners
                .Where(b => b.IsActive)
                .OrderBy(b => b.DisplayOrder)
                .ThenByDescending(b => b.CreatedAt)
                .Select(b => new BannerResponse(
                    b.BannerId,
                    b.Title,
                    b.ImageUrl,
                    b.LinkUrl,
                    b.BannerType,
                    b.DisplayOrder,
                    b.IsActive,
                    b.CreatedAt
                ))
                .ToListAsync(cancellationToken);

            return ServiceResult<List<BannerResponse>>.Ok(banners);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<BannerResponse>>.Fail(500, $"Lỗi lấy danh sách banner: {ex.Message}", "INTERNAL_ERROR");
        }
    }

    public async Task<ServiceResult<List<BannerResponse>>> GetAllBannersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var banners = await _dbContext.Banners
                .OrderBy(b => b.DisplayOrder)
                .ThenByDescending(b => b.CreatedAt)
                .Select(b => new BannerResponse(
                    b.BannerId,
                    b.Title,
                    b.ImageUrl,
                    b.LinkUrl,
                    b.BannerType,
                    b.DisplayOrder,
                    b.IsActive,
                    b.CreatedAt
                ))
                .ToListAsync(cancellationToken);

            return ServiceResult<List<BannerResponse>>.Ok(banners);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<BannerResponse>>.Fail(500, $"Lỗi lấy danh sách banner: {ex.Message}", "INTERNAL_ERROR");
        }
    }

    public async Task<ServiceResult<BannerResponse>> GetBannerByIdAsync(string bannerId, CancellationToken cancellationToken)
    {
        try
        {
            var b = await _dbContext.Banners.FirstOrDefaultAsync(x => x.BannerId == bannerId, cancellationToken);
            if (b == null)
            {
                return ServiceResult<BannerResponse>.Fail(404, $"Không tìm thấy banner với ID '{bannerId}'", "BANNER_NOT_FOUND");
            }

            var res = new BannerResponse(
                b.BannerId,
                b.Title,
                b.ImageUrl,
                b.LinkUrl,
                b.BannerType,
                b.DisplayOrder,
                b.IsActive,
                b.CreatedAt
            );

            return ServiceResult<BannerResponse>.Ok(res);
        }
        catch (Exception ex)
        {
            return ServiceResult<BannerResponse>.Fail(500, $"Lỗi: {ex.Message}", "INTERNAL_ERROR");
        }
    }

    public async Task<ServiceResult<BannerResponse>> CreateBannerAsync(
        CreateBannerRequest request, 
        Stream? fileStream, 
        string? fileName, 
        CancellationToken cancellationToken)
    {
        try
        {
            string? imageUrl = null;

            // 1. Lưu file ảnh tải lên nếu có
            if (fileStream != null && !string.IsNullOrWhiteSpace(fileName))
            {
                imageUrl = await _fileStorageService.SaveFileAsync(
                    fileStream,
                    fileName,
                    _fileStorageSettings.GeneralImageFolder,
                    cancellationToken
                );
            }
            else if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                imageUrl = request.ImageUrl;
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                return ServiceResult<BannerResponse>.Fail(400, "Yêu cầu cung cấp file ảnh banner hoặc đường dẫn URL ảnh.", "BAD_REQUEST");
            }

            // 2. Tạo entity Banner mới
            var banner = new Banner
            {
                BannerId = $"BN_{Guid.NewGuid():N}",
                Title = request.Title,
                ImageUrl = imageUrl,
                LinkUrl = request.LinkUrl,
                BannerType = request.BannerType,
                DisplayOrder = request.DisplayOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Banners.Add(banner);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var res = new BannerResponse(
                banner.BannerId,
                banner.Title,
                banner.ImageUrl,
                banner.LinkUrl,
                banner.BannerType,
                banner.DisplayOrder,
                banner.IsActive,
                banner.CreatedAt
            );

            return ServiceResult<BannerResponse>.Ok(res, "Đã tạo banner thành công.");
        }
        catch (Exception ex)
        {
            return ServiceResult<BannerResponse>.Fail(500, $"Lỗi khi tạo banner: {ex.Message}", "INTERNAL_ERROR");
        }
    }

    public async Task<ServiceResult<BannerResponse>> UpdateBannerAsync(
        string bannerId, 
        UpdateBannerRequest request, 
        Stream? fileStream, 
        string? fileName, 
        CancellationToken cancellationToken)
    {
        try
        {
            var banner = await _dbContext.Banners.FirstOrDefaultAsync(b => b.BannerId == bannerId, cancellationToken);
            if (banner == null)
            {
                return ServiceResult<BannerResponse>.Fail(404, $"Không tìm thấy banner với ID '{bannerId}'", "BANNER_NOT_FOUND");
            }

            // 1. Cập nhật ảnh nếu có file mới
            if (fileStream != null && !string.IsNullOrWhiteSpace(fileName))
            {
                // Xóa file cũ nếu lưu local
                if (!string.IsNullOrWhiteSpace(banner.ImageUrl) && !banner.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await _fileStorageService.DeleteFileAsync(banner.ImageUrl, cancellationToken);
                    }
                    catch
                    {
                        // Bỏ qua lỗi xóa file cũ
                    }
                }

                banner.ImageUrl = await _fileStorageService.SaveFileAsync(
                    fileStream,
                    fileName,
                    _fileStorageSettings.GeneralImageFolder,
                    cancellationToken
                );
            }
            else if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                banner.ImageUrl = request.ImageUrl;
            }

            // 2. Cập nhật các trường thông tin khác
            banner.Title = request.Title;
            banner.LinkUrl = request.LinkUrl;
            banner.BannerType = request.BannerType;
            banner.DisplayOrder = request.DisplayOrder;
            banner.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var res = new BannerResponse(
                banner.BannerId,
                banner.Title,
                banner.ImageUrl,
                banner.LinkUrl,
                banner.BannerType,
                banner.DisplayOrder,
                banner.IsActive,
                banner.CreatedAt
            );

            return ServiceResult<BannerResponse>.Ok(res, "Cập nhật banner thành công.");
        }
        catch (Exception ex)
        {
            return ServiceResult<BannerResponse>.Fail(500, $"Lỗi cập nhật banner: {ex.Message}", "INTERNAL_ERROR");
        }
    }

    public async Task<ServiceResult<object>> DeleteBannerAsync(string bannerId, CancellationToken cancellationToken)
    {
        try
        {
            var banner = await _dbContext.Banners.FirstOrDefaultAsync(b => b.BannerId == bannerId, cancellationToken);
            if (banner == null)
            {
                return ServiceResult<object>.Fail(404, $"Không tìm thấy banner với ID '{bannerId}'", "BANNER_NOT_FOUND");
            }

            // Xóa file vật lý lưu cục bộ
            if (!string.IsNullOrWhiteSpace(banner.ImageUrl) && !banner.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _fileStorageService.DeleteFileAsync(banner.ImageUrl, cancellationToken);
                }
                catch
                {
                    // Bỏ qua lỗi xóa file
                }
            }

            _dbContext.Banners.Remove(banner);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<object>.Ok(new object(), "Đã xóa banner thành công.");
        }
        catch (Exception ex)
        {
            return ServiceResult<object>.Fail(500, $"Lỗi xóa banner: {ex.Message}", "INTERNAL_ERROR");
        }
    }
}
