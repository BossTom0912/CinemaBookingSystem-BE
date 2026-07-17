using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Banners;

public record CreateBannerRequest(
    [Required] string Title,
    string? LinkUrl,
    [Required] string BannerType, // MOVIE, PROMOTION, FOOD_BEVERAGE, SYSTEM
    int DisplayOrder,
    string? ImageUrl
);
