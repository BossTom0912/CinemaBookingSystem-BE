using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Interfaces;
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
    private readonly CinemaSystem.Application.Settings.CinemaProcessingSettings _settings;

    public MovieHighlightClassificationJob(IServiceProvider serviceProvider, ILogger<MovieHighlightClassificationJob> logger, Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.CinemaProcessingSettings> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                _logger.LogInformation(
                    "MovieHighlightClassificationJob running at: {Time}",
                    clock.UtcNow);

                var movies = await dbContext.Movies.ToListAsync(stoppingToken);
                var today = DateOnly.FromDateTime(clock.UtcNow);

                var maxViews = movies.Any() ? movies.Max(m => m.ViewCount) : 0;

                foreach (var movie in movies)
                {
                    string? newHighlight = null;

                    if (maxViews > 0 && movie.ViewCount == maxViews)
                    {
                        newHighlight = CinemaSystem.Domain.Constants.DomainConstants.MovieHighlight.Popular;
                    }
                    else if (movie.ReleaseDate > today)
                    {
                        newHighlight = CinemaSystem.Domain.Constants.DomainConstants.MovieHighlight.ComingSoon;
                    }
                    else if (movie.ReleaseDate <= today
                        && movie.ReleaseDate > today.AddDays(-_settings.MovieNewReleaseWindowDays))
                    {
                        newHighlight = CinemaSystem.Domain.Constants.DomainConstants.MovieHighlight.New;
                    }
                    else if (movie.TotalViews > _settings.MovieHotTotalViewThreshold || movie.DailyViews > _settings.MovieHotDailyViewThreshold || movie.ViewCount >= _settings.MovieHotViewThreshold)
                    {
                        newHighlight = CinemaSystem.Domain.Constants.DomainConstants.MovieHighlight.Hot;
                    }
                    else if (movie.ViewCount >= _settings.MovieTrendingViewThreshold)
                    {
                        newHighlight = CinemaSystem.Domain.Constants.DomainConstants.MovieHighlight.Trending;
                    }

                    movie.Highlight = newHighlight;
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Movie Highlight Classification");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_settings.MovieClassificationIntervalMinutes),
                stoppingToken);
        }
    }
}
