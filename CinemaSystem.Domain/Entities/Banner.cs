using System;

namespace CinemaSystem.Domain.Entities;

public class Banner
{
    public string BannerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string BannerType { get; set; } = "SYSTEM"; // MOVIE, PROMOTION, FOOD_BEVERAGE, SYSTEM
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
