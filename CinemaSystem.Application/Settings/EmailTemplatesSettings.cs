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
    
    public string ShowtimeCancellationSubject { get; set; } = "Thông báo hủy suất chiếu / Showtime Cancellation Notice";
    public string ShowtimeCancellationBody { get; set; } = "[VI] Xuất chiếu của bạn đã được hủy do sự cố: {0}. Quý khách vui lòng chờ hệ thống hoàn tiền.\n\n[EN] Your showtime has been cancelled due to: {0}. Please wait for your refund to be processed.";
}
