using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Banners;

public record UpdateBannerRequest(
    [Required] string Title,
    string? LinkUrl,
    [Required] string BannerType,
    int DisplayOrder,
    bool IsActive,
    string? ImageUrl
);
