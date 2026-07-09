namespace CinemaSystem.Application.Settings;

public class CinemaProcessingSettings
{
    public const string SectionName = "CinemaProcessingSettings";

    public int PreShowtimeBlockingMinutes { get; set; } = 30;
    public int ScreeningRoomCleaningMinutes { get; set; } = 15;
    public int ShowtimeMaterialChangeThresholdMinutes { get; set; } = 15;
    
    public int MovieHotViewThreshold { get; set; } = 1000;
    public int MovieTrendingViewThreshold { get; set; } = 500;
    public int MovieHotTotalViewThreshold { get; set; } = 5000;
    public int MovieHotDailyViewThreshold { get; set; } = 500;
    public int MovieNewReleaseWindowDays { get; set; } = 14;
    public int MovieClassificationIntervalMinutes { get; set; } = 60;
    public int MaxRoomCapacity { get; set; } = 500;
    public int ReviewMaxEditCount { get; set; } = 1;
    public int ReviewSpamLockoutMinutes { get; set; } = 1;
}
