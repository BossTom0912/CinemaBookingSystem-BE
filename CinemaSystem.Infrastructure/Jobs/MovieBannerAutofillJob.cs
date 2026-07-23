using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Jobs;

public class MovieBannerAutofillJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MovieBannerAutofillJob> _logger;

    public MovieBannerAutofillJob(
        IServiceProvider serviceProvider, 
        ILogger<MovieBannerAutofillJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chạy lần đầu tiên sau khi khởi động app 10 giây
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

                _logger.LogInformation("MovieBannerAutofillJob: Quét và cập nhật trạng thái banner phim...");

                // Gỡ/xóa banner của các phim đã hết chiếu (khác NOW_SHOWING) để không hiển thị trên trang Home
                var endedMoviesWithBanner = await dbContext.Movies
                    .Where(m => m.MovieStatus != "NOW_SHOWING" && m.BannerUrl != null && m.BannerUrl != "")
                    .ToListAsync(stoppingToken);

                if (endedMoviesWithBanner.Any())
                {
                    _logger.LogInformation("MovieBannerAutofillJob: Tìm thấy {Count} phim đã hết chiếu vẫn còn banner. Tiến hành gỡ banner...", endedMoviesWithBanner.Count);
                    foreach (var movie in endedMoviesWithBanner)
                    {
                        movie.BannerUrl = null;
                        _logger.LogInformation("MovieBannerAutofillJob: Đã gỡ banner cho phim '{Title}'", movie.Title);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MovieBannerAutofillJob: Lỗi xảy ra trong quá trình chạy background job");
            }

            // Chạy định kỳ mỗi 5 phút
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
