using System;

namespace CinemaSystem.Contracts.Banners;

public record BannerResponse(
    string BannerId,
    string Title,
    string ImageUrl,
    string? LinkUrl,
    string BannerType,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt
);
