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

public class MovieHighlightClassificationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MovieHighlightClassificationJob> _logger;

    public MovieHighlightClassificationJob(IServiceProvider serviceProvider, ILogger<MovieHighlightClassificationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MovieHighlightClassificationJob running at: {time}", DateTimeOffset.Now);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

                var movies = await dbContext.Movies.ToListAsync(stoppingToken);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                var maxViews = movies.Any() ? movies.Max(m => m.ViewCount) : 0;

                foreach (var movie in movies)
                {
                    string? newHighlight = null;

                    if (maxViews > 0 && movie.ViewCount == maxViews)
                    {
                        newHighlight = "POPULAR";
                    }
                    else if (movie.ReleaseDate > today)
                    {
                        newHighlight = "COMING_SOON";
                    }
                    else if (movie.ReleaseDate <= today && movie.ReleaseDate > today.AddDays(-14))
                    {
                        newHighlight = "NEW";
                    }
                    else if (movie.TotalViews > 5000 || movie.DailyViews > 500 || movie.ViewCount >= 1000)
                    {
                        newHighlight = "HOT";
                    }
                    else if (movie.ViewCount >= 500)
                    {
                        newHighlight = "TRENDING";
                    }

                    movie.Highlight = newHighlight;
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Movie Highlight Classification");
            }

            // Runs every 1 hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
