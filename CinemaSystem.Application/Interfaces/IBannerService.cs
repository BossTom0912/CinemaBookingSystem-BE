using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Banners;

namespace CinemaSystem.Application.Interfaces;

public interface IBannerService
{
    Task<ServiceResult<List<BannerResponse>>> GetActiveBannersAsync(CancellationToken cancellationToken);

    Task<ServiceResult<List<BannerResponse>>> GetAllBannersAsync(CancellationToken cancellationToken);

    Task<ServiceResult<BannerResponse>> GetBannerByIdAsync(string bannerId, CancellationToken cancellationToken);

    Task<ServiceResult<BannerResponse>> CreateBannerAsync(
        CreateBannerRequest request, 
        Stream? fileStream, 
        string? fileName, 
        CancellationToken cancellationToken);

    Task<ServiceResult<BannerResponse>> UpdateBannerAsync(
        string bannerId, 
        UpdateBannerRequest request, 
        Stream? fileStream, 
        string? fileName, 
        CancellationToken cancellationToken);

    Task<ServiceResult<object>> DeleteBannerAsync(string bannerId, CancellationToken cancellationToken);
}
