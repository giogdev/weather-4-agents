using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather4Agents.Application.Settings;
using Weather4Agents.Application.UseCases.ScrapeAndCache;

namespace Weather4Agents.Infrastructure.Jobs;

public class WeatherScrapingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WeatherScrapingSettings _settings;
    private readonly ILogger<WeatherScrapingJob> _logger;

    public WeatherScrapingJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WeatherScrapingSettings> options,
        ILogger<WeatherScrapingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RunScrapingCycleAsync(ct);
            await Task.Delay(TimeSpan.FromMinutes(_settings.JobIntervalMinutes), ct);
        }
    }

    private async Task RunScrapingCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Weather scraping cycle started at {Time}", DateTimeOffset.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        foreach (var location in _settings.Locations)
        {
            foreach (var provider in _settings.EnabledProviders)
            {
                try
                {
                    await mediator.Send(new ScrapeAndCacheCommand(location, provider), ct);
                    _logger.LogInformation("Scraped {Provider} / {Location}", provider, location);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to scrape {Provider} / {Location}", provider, location);
                }
            }
        }

        _logger.LogInformation("Weather scraping cycle completed at {Time}", DateTimeOffset.UtcNow);
    }
}
