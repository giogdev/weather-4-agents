using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.ValueObjects;

namespace Weather4Agents.Infrastructure.Scrapers.Base;

public abstract class BaseWeatherScraper : IWeatherProviderScraper
{
    // An empty scrape (provider unreachable or unknown location) must not be served for the
    // standard 24h. It is negative-cached with a short TTL so a later request re-scrapes within
    // a few minutes instead of returning the empty result for a whole day.
    private static readonly HybridCacheEntryOptions NegativeCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly TimeProvider TimeProvider;
    private readonly HybridCache _hybridCache;

    protected BaseWeatherScraper(
        HttpClient httpClient,
        HybridCache hybridCache,
        TimeProvider timeProvider,
        ILogger logger)
    {
        HttpClient = httpClient;
        _hybridCache = hybridCache;
        TimeProvider = timeProvider;
        Logger = logger;
    }

    public abstract string ProviderName { get; }

    public abstract TimeZoneInfo TimeZone { get; }

    /// <param name="location">Canonical location spelling (see <see cref="LocationName.Normalize"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    protected abstract Task<IEnumerable<DayWeather>> ScrapeAsync(string location, CancellationToken ct);

    public async Task<ScrapedForecast> GetForecastAsync(
        string location,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        // One canonical spelling feeds both the cache key and the scrape, so "San Pellegrino
        // Terme" and "san-pellegrino-terme" share a single cache entry and a single scrape.
        var normalizedLocation = LocationName.Normalize(location);
        var cacheKey = CacheKeyFor(normalizedLocation);

        if (forceRefresh)
        {
            var fresh = await ScrapeStampedAsync(normalizedLocation, ct);
            await _hybridCache.SetAsync(cacheKey, fresh, OptionsFor(fresh), cancellationToken: ct);
            return fresh;
        }

        // GetOrCreateAsync writes the factory result with a single set of options chosen before the
        // result is known, so default to the short negative-cache TTL and promote a real forecast to
        // the standard 24h TTL once it has actually been produced by the factory. The scrape time is
        // captured in the cached value, so a later cache hit keeps reporting the original scrape.
        var created = false;
        var result = await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async innerCt =>
            {
                created = true;
                return await ScrapeStampedAsync(normalizedLocation, innerCt);
            },
            NegativeCacheOptions,
            cancellationToken: ct);

        if (created && result.Days.Count > 0)
            await _hybridCache.SetAsync(cacheKey, result, cancellationToken: ct);

        return result;
    }

    /// <summary>
    /// Seeds the cache with a forecast obtained from an external source (e.g. JSON files written
    /// on a previous run) so it can be served immediately, without a scrape. The forecast keeps its
    /// original <see cref="ScrapedForecast.ScrapedAt"/>, so freshness still reflects the original
    /// scrape; a later cache miss or <c>forceRefresh</c> re-scrapes normally.
    /// </summary>
    public async Task SeedAsync(string location, ScrapedForecast forecast, CancellationToken ct = default)
    {
        // Never seed an empty forecast: that would mask a genuine "unknown location" behind stale
        // emptiness for the full 24h TTL. An empty result belongs in the short negative cache,
        // reached only via an actual scrape.
        if (forecast.Days.Count == 0)
            return;

        var cacheKey = CacheKeyFor(LocationName.Normalize(location));
        await _hybridCache.SetAsync(cacheKey, forecast, cancellationToken: ct);
    }

    // Cache key is derived from the provider name and the canonical location spelling, so every
    // surface that touches the cache (scrape, serve, seed) agrees on the same entry.
    private string CacheKeyFor(string normalizedLocation)
        => $"{ProviderName.ToLowerInvariant()}:{normalizedLocation}";

    private async Task<ScrapedForecast> ScrapeStampedAsync(string normalizedLocation, CancellationToken ct)
        => new()
        {
            ScrapedAt = TimeProvider.GetUtcNow(),
            Days = (await ScrapeAsync(normalizedLocation, ct)).ToList()
        };

    // Empty forecasts get the short negative-cache TTL; a real forecast keeps the default 24h TTL.
    private static HybridCacheEntryOptions? OptionsFor(ScrapedForecast forecast)
        => forecast.Days.Count == 0 ? NegativeCacheOptions : null;
}
