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
    private readonly HybridCache _hybridCache;

    protected BaseWeatherScraper(HttpClient httpClient, HybridCache hybridCache, ILogger logger)
    {
        HttpClient = httpClient;
        _hybridCache = hybridCache;
        Logger = logger;
    }

    public abstract string ProviderName { get; }

    public abstract TimeZoneInfo TimeZone { get; }

    /// <param name="location">Canonical location spelling (see <see cref="LocationName.Normalize"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    protected abstract Task<IEnumerable<DayWeather>> ScrapeAsync(string location, CancellationToken ct);

    public async Task<IEnumerable<DayWeather>> GetForecastAsync(
        string location,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        // One canonical spelling feeds both the cache key and the scrape, so "San Pellegrino
        // Terme" and "san-pellegrino-terme" share a single cache entry and a single scrape.
        var normalizedLocation = LocationName.Normalize(location);
        var cacheKey = $"{ProviderName.ToLowerInvariant()}:{normalizedLocation}";

        if (forceRefresh)
        {
            var fresh = (await ScrapeAsync(normalizedLocation, ct)).ToList();
            await _hybridCache.SetAsync(cacheKey, fresh, OptionsFor(fresh), cancellationToken: ct);
            return fresh;
        }

        // GetOrCreateAsync writes the factory result with a single set of options chosen before the
        // result is known, so default to the short negative-cache TTL and promote a real forecast to
        // the standard 24h TTL once it has actually been produced by the factory.
        var created = false;
        var result = (await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async innerCt =>
            {
                created = true;
                return (await ScrapeAsync(normalizedLocation, innerCt)).ToList();
            },
            NegativeCacheOptions,
            cancellationToken: ct)).ToList();

        if (created && result.Count > 0)
            await _hybridCache.SetAsync(cacheKey, result, cancellationToken: ct);

        return result;
    }

    // Empty forecasts get the short negative-cache TTL; a real forecast keeps the default 24h TTL.
    private static HybridCacheEntryOptions? OptionsFor(IReadOnlyCollection<DayWeather> forecast)
        => forecast.Count == 0 ? NegativeCacheOptions : null;
}
