using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.Settings;
using Weather4Agents.Application.UseCases.ScrapeAndCache;
using Weather4Agents.Infrastructure.Diagnostics;
using Weather4Agents.Infrastructure.Storage;

namespace Weather4Agents.Infrastructure.Jobs;

/// <summary>
/// The single background job coordinating scraping and file storage. On startup it seeds the
/// cache from any JSON files left on disk (so forecasts are served immediately after a restart),
/// then on each cycle it scrapes every enabled provider for every location and, as a final step,
/// persists the fresh data to disk. Keeping storage a step of this one cycle means one schedule,
/// no startup race, and no independent timer that could self-trigger a scrape.
/// </summary>
public class WeatherScrapingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WeatherScrapingSettings _settings;
    private readonly ScrapeCycleTracker _cycleTracker;
    private readonly ILogger<WeatherScrapingJob> _logger;

    public WeatherScrapingJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WeatherScrapingSettings> options,
        ScrapeCycleTracker cycleTracker,
        ILogger<WeatherScrapingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _cycleTracker = cycleTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await BootstrapCacheFromDiskAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            await RunScrapingCycleAsync(ct);
            await Task.Delay(TimeSpan.FromMinutes(_settings.JobIntervalMinutes), ct);
        }
    }

    // Best-effort cache seeding from disk, once, before the first scrape. Failures here must never
    // stop the service from starting its scraping loop.
    private async Task BootstrapCacheFromDiskAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<WeatherFileStore>();

        if (!store.Enabled)
            return;

        try
        {
            await store.BootstrapCacheAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Cache bootstrap from disk failed; continuing with a fresh scrape.");
        }
    }

    private async Task RunScrapingCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Weather scraping cycle started at {Time}", DateTimeOffset.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var anySucceeded = false;

        foreach (var location in _settings.Locations)
        {
            foreach (var provider in _settings.EnabledProviders)
            {
                try
                {
                    await dispatcher.SendAsync(new ScrapeAndCacheCommand(location, provider), ct);
                    anySucceeded = true;
                    _logger.LogInformation("Scraped {Provider} / {Location}", provider, location);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to scrape {Provider} / {Location}", provider, location);
                }
            }
        }

        // Mark the cycle successful only if at least one scrape went through, so the health check
        // reports fresh data rather than a cycle that failed for every location.
        if (anySucceeded)
            _cycleTracker.MarkCycleSucceeded();

        _logger.LogInformation("Weather scraping cycle completed at {Time}", DateTimeOffset.UtcNow);

        // File storage runs as the final step of the same cycle (Option A): the freshly scraped
        // data is already cached, so persisting reads it back without triggering another scrape.
        var store = scope.ServiceProvider.GetRequiredService<WeatherFileStore>();
        if (store.Enabled)
        {
            try
            {
                await store.PersistForecastsAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Persisting forecasts to disk failed.");
            }
        }
    }
}
