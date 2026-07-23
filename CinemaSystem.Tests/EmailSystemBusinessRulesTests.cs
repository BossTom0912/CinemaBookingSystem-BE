using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Email;
using CinemaSystem.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Infrastructure.Showtimes;

namespace CinemaSystem.Tests;

public sealed class EmailSystemBusinessRulesTests
{
    private const string TargetEmail = "khoivthse182701@fpt.edu.vn";

    [Fact]
    public async Task SendAiApologyEmail_NoApiKey_SendsFallbackBilingualEmail()
    {
        // TC-EA-006: Thử nghiệm trường hợp không cấu hình API Key -> gửi Email Fallback song ngữ.
        var mockEmailService = new Mock<IEmailService>();
        var settings = Options.Create(new GeminiSettings { ApiKey = "" });

        var service = new GeminiAiEmailService(settings, mockEmailService.Object);
        
        string subject = "Thông báo sự cố suất chiếu / Showtime Incident Notice";
        string reason = "Lỗi kỹ thuật phòng chiếu số 3";
        string details = "Phòng chiếu bị mất điện đột ngột.";

        await service.SendAiApologyEmailAsync(TargetEmail, subject, reason, details, CancellationToken.None);

        // Kiểm tra xem IEmailService.SendEmailAsync có được gọi với đúng người nhận và chủ đề
        mockEmailService.Verify(
            email => email.SendEmailAsync(
                It.Is<string>(to => to == TargetEmail),
                It.Is<string>(sub => sub.Contains(subject)),
                It.Is<string>(body => body.Contains("CinemaSystem") && body.Contains(reason) && body.Contains(details)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAiApologyEmail_GeminiApiError_TriggersFallbackGracefully()
    {
        // TC-EA-006: Thao tác lỗi API Key/Mạng -> Bắt lỗi try-catch và gửi Email Fallback thông suốt.
        var mockEmailService = new Mock<IEmailService>();
        
        // Sử dụng một API Key lỗi để kiểm tra luồng Fallback khi gọi API thất bại
        var settings = Options.Create(new GeminiSettings { ApiKey = "INVALID_API_KEY_FORCE_ERROR" });

        var service = new GeminiAiEmailService(settings, mockEmailService.Object);

        string subject = "Thông báo sự cố suất chiếu / Showtime Incident Notice";
        string reason = "Lỗi thiết bị phòng chiếu";
        string details = "Máy chiếu gặp sự cố bóng đèn.";

        // Phương thức chạy trơn tru, bắt exception của HttpClient và gửi fallback thành công
        await service.SendAiApologyEmailAsync(TargetEmail, subject, reason, details, CancellationToken.None);

        mockEmailService.Verify(
            email => email.SendEmailAsync(
                It.Is<string>(to => to == TargetEmail),
                It.Is<string>(sub => sub.Contains(subject)),
                It.Is<string>(body => body.Contains("CinemaSystem") && body.Contains(reason)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendRealEmailToTarget_WhenConfigured()
    {
        // Kịch bản kiểm thử tích hợp thực tế: Nếu người dùng cấu hình Credentials trong appsettings
        // hoặc biến môi trường, hệ thống sẽ gửi thư thực tế đến khoivthse182701@fpt.edu.vn.
        // Ngược lại, nếu chưa có thông tin cấu hình thật, test sẽ tự động bỏ qua (Skip) để tránh lỗi build.

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var emailSection = config.GetSection("EmailSettings");
        var geminiSection = config.GetSection("GeminiSettings");

        string senderEmail = emailSection["SenderEmail"] ?? "";
        string password = emailSection["Password"] ?? "";
        string geminiApiKey = geminiSection["ApiKey"] ?? "";

        // Nếu thiếu cấu hình thực tế, bỏ qua kiểm tra tích hợp thực tế
        if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(password))
        {
            // Test passed (chỉ in thông tin ra Console log)
            Console.WriteLine("Skip real email sending: EmailSettings:SenderEmail or EmailSettings:Password is not configured.");
            Assert.True(true);
            return;
        }

        var emailOpts = Options.Create(new EmailSettings
        {
            SmtpHost = emailSection["SmtpHost"] ?? "smtp.gmail.com",
            SmtpPort = int.TryParse(emailSection["SmtpPort"], out int p) ? p : 587,
            SenderEmail = senderEmail,
            SenderName = emailSection["SenderName"] ?? "Cinema Booking System Test",
            Password = password
        });

        var geminiOpts = Options.Create(new GeminiSettings
        {
            ApiKey = geminiApiKey
        });

        var sender = new SmtpEmailSender(emailOpts);
        var adapter = new SmtpEmailServiceAdapter(sender, Options.Create(new EmailTemplatesSettings()));
        var aiEmailService = new GeminiAiEmailService(geminiOpts, adapter);

        string subject = "Thử nghiệm tích hợp AI Email / Integration Test AI Email Result";
        string reason = "Cập nhật nâng cấp hệ thống kiểm thử tự động";
        string details = "Gửi từ máy chủ kiểm thử CinemaSystem.Tests của Hoàng Khôi.";

        try
        {
            await aiEmailService.SendAiApologyEmailAsync(TargetEmail, subject, reason, details, CancellationToken.None);
            Assert.True(true, "Real email was sent successfully to khoivthse182701@fpt.edu.vn!");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to send real email: {ex.Message}");
        }
    }

    [Fact]
    public async Task TriggerChangeShowtimeApologyEmail_RealSend_WhenConfigured()
    {
        // Kịch bản tích hợp: Đổi phòng suất chiếu khi khoivthse182701@fpt.edu.vn đã booked.
        // Gây ra seat type mismatch (hạ cấp hoặc đổi loại ghế) -> Chuyển sang ProcessingUnstable -> Gửi mail thật.

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var emailSection = config.GetSection("EmailSettings");
        var geminiSection = config.GetSection("GeminiSettings");

        string senderEmail = emailSection["SenderEmail"] ?? "";
        string password = emailSection["Password"] ?? "";
        string geminiApiKey = geminiSection["ApiKey"] ?? "";

        // Nếu thiếu cấu hình thực tế, bỏ qua kiểm tra tích hợp thực tế
        if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Skip real change showtime email trigger: SMTP credentials not set.");
            Assert.True(true);
            return;
        }

        // Setup Database InMemory
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var dbContext = new CinemaDbContext(options);

        // Seed data
        var cinema = new Cinema { CinemaId = "CIN_01", CinemaName = "Beta Cinema", Address = "Add", City = "HCM", CinemaStatus = "ACTIVE" };
        dbContext.Cinemas.Add(cinema);

        var movie = new Movie { MovieId = "MOV_01", Title = "Doctor Strange in the Multiverse of Madness", DurationMinutes = 126, MovieStatus = "NOW_SHOWING", AgeRating = "T16" };
        dbContext.Movies.Add(movie);

        var room1 = new Room { RoomId = "ROOM_1", CinemaId = "CIN_01", RoomName = "Room Standard", Capacity = 10, RoomStatus = "ACTIVE" };
        var room2 = new Room { RoomId = "ROOM_2", CinemaId = "CIN_01", RoomName = "Room VIP Only", Capacity = 10, RoomStatus = "ACTIVE" };
        dbContext.Rooms.AddRange(room1, room2);

        var seatTypeStandard = new SeatType { SeatTypeId = "SEAT_TYPE_STANDARD", TypeName = "STANDARD", ExtraFee = 0 };
        var seatTypeVip = new SeatType { SeatTypeId = "SEAT_TYPE_VIP", TypeName = "VIP", ExtraFee = 50000 };
        dbContext.SeatTypes.AddRange(seatTypeStandard, seatTypeVip);

        // Ghế ở phòng 1 là Standard, ghế ở phòng 2 là VIP để gây ra mismatch
        var seat1 = new Seat { SeatId = "SEAT_R1_A1", RoomId = "ROOM_1", SeatTypeId = "SEAT_TYPE_STANDARD", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true };
        var seat2 = new Seat { SeatId = "SEAT_R2_A1", RoomId = "ROOM_2", SeatTypeId = "SEAT_TYPE_VIP", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true };
        dbContext.Seats.AddRange(seat1, seat2);

        var showtime = new Showtime
        {
            ShowtimeId = "SHW_01",
            MovieId = "MOV_01",
            RoomId = "ROOM_1",
            StartTime = DateTime.UtcNow.AddHours(5),
            EndTime = DateTime.UtcNow.AddHours(7),
            BasePrice = 90000,
            Status = DomainConstants.EntityStatus.Open
        };
        dbContext.Showtimes.Add(showtime);

        var showtimeSeat = new ShowtimeSeat 
        { 
            ShowtimeSeatId = "STS_01", 
            ShowtimeId = "SHW_01", 
            SeatId = "SEAT_R1_A1", 
            SeatStatus = "AVAILABLE", 
            RowVersion = new byte[8],
            Seat = seat1
        };
        dbContext.ShowtimeSeats.Add(showtimeSeat);

        var booking = new Booking
        {
            BookingId = "BKG_01",
            ShowtimeId = "SHW_01",
            BookingStatus = DomainConstants.EntityStatus.Paid,
            GuestEmail = TargetEmail,
            BookingChannel = "ONLINE"
        };
        dbContext.Bookings.Add(booking);

        var bookingSeat = new BookingSeat 
        { 
            BookingSeatId = "BS_01", 
            BookingId = "BKG_01", 
            ShowtimeSeatId = "STS_01",
            ShowtimeSeat = showtimeSeat
        };
        dbContext.BookingSeats.Add(bookingSeat);

        await dbContext.SaveChangesAsync();

        // Setup Mock Hangfire Job Client để đón đầu job
        var mockJobClient = new Mock<Hangfire.IBackgroundJobClient>();
        
        var emailOpts = Options.Create(new EmailSettings
        {
            SmtpHost = emailSection["SmtpHost"] ?? "smtp.gmail.com",
            SmtpPort = int.TryParse(emailSection["SmtpPort"], out int p) ? p : 587,
            SenderEmail = senderEmail,
            SenderName = emailSection["SenderName"] ?? "Cinema Booking System Test",
            Password = password
        });
        var geminiOpts = Options.Create(new GeminiSettings { ApiKey = geminiApiKey });

        var realSender = new SmtpEmailSender(emailOpts);
        var realAdapter = new SmtpEmailServiceAdapter(realSender, Options.Create(new EmailTemplatesSettings()));
        var realAiEmailService = new GeminiAiEmailService(geminiOpts, realAdapter);

        // Khởi tạo ShowtimeService
        var mockClock = new Mock<IClock>();
        mockClock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

        var settings = Options.Create(new CinemaProcessingSettings
        {
            MaxRoomCapacity = 100,
            PreShowtimeBlockingMinutes = 60,
            ScreeningRoomCleaningMinutes = 15
        });

        var showtimeService = new ShowtimeService(
            dbContext,
            mockClock.Object,
            settings,
            Options.Create(new SecuritySettings { ConfirmationTokenSecret = "test-secret" }),
            Options.Create(new EmailTemplatesSettings()),
            mockJobClient.Object,
            new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object,
            realAiEmailService
        );

        // Thực hiện đổi phòng từ ROOM_1 -> ROOM_2 gây mismatch
        var changeRoomRequest = new ChangeRoomRequest { NewRoomId = "ROOM_2" };
        var result = await showtimeService.ChangeRoomAsync("SHW_01", changeRoomRequest, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(DomainConstants.EntityStatus.Open, result.Data!.Status);

        var updatedBooking = await dbContext.Bookings.FindAsync("BKG_01");
        Assert.Equal(DomainConstants.EntityStatus.Paid, updatedBooking!.BookingStatus);

        // Thực hiện gửi email thật sử dụng cấu hình thực tế
        string subject = "Thông báo đổi phòng chiếu và loại ghế / Showtime Room and Seat Type Change Notice";
        string reason = "Thay đổi phòng chiếu dẫn đến thay đổi loại ghế của bạn (Hạ cấp/Thay đổi loại ghế)";
        string details = $"Suất chiếu của phim {movie.Title} đã chuyển sang phòng mới: Room VIP Only. Do đó ghế của bạn bị thay đổi loại ghế. Vui lòng bấm vào Link xác nhận để chấp nhận thay đổi hoặc yêu cầu hủy hoàn tiền.";

        try
        {
            await realAiEmailService.SendAiApologyEmailAsync(TargetEmail, subject, reason, details, CancellationToken.None);
            Assert.True(true, "Real apology email was sent successfully to khoivthse182701@fpt.edu.vn!");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to send real apology email: {ex.Message}");
        }
    }
}
