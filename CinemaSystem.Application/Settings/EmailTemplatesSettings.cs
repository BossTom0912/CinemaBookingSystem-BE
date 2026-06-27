namespace CinemaSystem.Application.Settings;

public class EmailTemplatesSettings
{
    public string SeatMaintenanceSubject { get; set; } = string.Empty;
    public string SeatMaintenanceBody { get; set; } = string.Empty;
    
    public string ShowtimeTimeChangeSubject { get; set; } = string.Empty;
    public string ShowtimeTimeChangeBody { get; set; } = string.Empty;
    
    public string ShowtimeTimeChangeNoticeSubject { get; set; } = string.Empty;
    public string ShowtimeTimeChangeNoticeBody { get; set; } = string.Empty;
    
    public string ShowtimeRoomChangeSubject { get; set; } = string.Empty;
    public string ShowtimeRoomChangeBody { get; set; } = string.Empty;
    
    public string ShowtimeCancellationSubject { get; set; } = string.Empty;
    public string ShowtimeCancellationBody { get; set; } = string.Empty;
}
