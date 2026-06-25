namespace CinemaSystem.Application.Settings;

public class CinemaProcessingSettings
{
    public int PreShowtimeBlockingMinutes { get; set; } = 30;
    public int ScreeningRoomCleaningMinutes { get; set; } = 15;
    public int MovieRankingMilestoneScore { get; set; } = 100;
    public int RedisSeatReservationMinutes { get; set; } = 10;
}
