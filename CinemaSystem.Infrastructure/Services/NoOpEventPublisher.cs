using CinemaSystem.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Services;

public class NoOpEventPublisher : IEventPublisher
{
    private readonly ILogger<NoOpEventPublisher> _logger;

    public NoOpEventPublisher(ILogger<NoOpEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        _logger.LogInformation("Event published: {EventType}. Payload: {@Event}", typeof(TEvent).Name, @event);
        return Task.CompletedTask;
    }
}
