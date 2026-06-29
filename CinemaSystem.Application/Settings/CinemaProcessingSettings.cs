namespace CinemaSystem.Application.Settings;

public class CinemaProcessingSettings
{
    public int PreShowtimeBlockingMinutes { get; set; } = 30;
    public int ScreeningRoomCleaningMinutes { get; set; } = 15;
    public int MovieRankingMilestoneScore { get; set; } = 100;
    public int RedisSeatReservationMinutes { get; set; } = 10;
    
    public int MovieHotViewThreshold { get; set; } = 1000;
    public int MovieTrendingViewThreshold { get; set; } = 500;
    public int MovieHotTotalViewThreshold { get; set; } = 5000;
    public int MovieHotDailyViewThreshold { get; set; } = 500;
    public int MaxRoomCapacity { get; set; } = 500;
    public int ReviewSpamLockoutMinutes { get; set; } = 1;
    public int ReviewSpamLockoutWarningDays { get; set; } = 7;
}
