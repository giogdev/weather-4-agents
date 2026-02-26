using Microsoft.Extensions.Caching.Hybrid;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Infrastructure.Scrapers.Base;

public abstract class BaseWeatherScraper : IWeatherProviderScraper
{
    protected readonly HttpClient HttpClient;
    private readonly HybridCache _hybridCache;

    protected BaseWeatherScraper(HttpClient httpClient, HybridCache hybridCache)
    {
        HttpClient = httpClient;
        _hybridCache = hybridCache;
    }

    public abstract string ProviderName { get; }

    protected abstract Task<IEnumerable<DayWeather>> ScrapeAsync(string location, CancellationToken ct);

    public async Task<IEnumerable<DayWeather>> GetForecastAsync(
        string location,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var cacheKey = $"{ProviderName.ToLowerInvariant()}:{location.ToLowerInvariant()}";

        if (forceRefresh)
        {
            var fresh = await ScrapeAsync(location, ct);
            await _hybridCache.SetAsync(cacheKey, fresh, cancellationToken: ct);
            return fresh;
        }

        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async innerCt => await ScrapeAsync(location, innerCt),
            cancellationToken: ct);
    }
}
