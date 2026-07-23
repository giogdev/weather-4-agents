using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Application.Settings;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.ValueObjects;
using Weather4Agents.Infrastructure.Diagnostics;

namespace Weather4Agents.Infrastructure.Scrapers.Base;

public abstract class BaseWeatherScraper : IWeatherProviderScraper
{
    // The forecast is cached as two independent segments with different lifetimes: the current day
    // (which providers refresh often, ~30 min) and the following days (which change slowly). A cold
    // request scrapes the whole week once and populates both entries with a single, consistent
    // scrape time; once the short-lived "today" entry expires on its own, only that one page is
    // re-scraped instead of the whole week.
    private const string TodaySegment = "today";
    private const string ExtendedSegment = "extended";
    private const int TodayDayOffset = 0;
    private const int LastExtendedDayOffset = ForecastLimits.MaxDays - 1;

    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly TimeProvider TimeProvider;
    private readonly HybridCache _hybridCache;
    private readonly WeatherMetrics _metrics;

    private readonly HybridCacheEntryOptions _todayOptions;
    private readonly HybridCacheEntryOptions _extendedOptions;
    // An empty scrape (provider unreachable or unknown location) must not be served for the full
    // positive TTL. It is negative-cached with a short TTL so a later request re-scrapes within a
    // few minutes instead of returning the empty result for hours.
    private readonly HybridCacheEntryOptions _negativeOptions;

    protected BaseWeatherScraper(
        HttpClient httpClient,
        HybridCache hybridCache,
        TimeProvider timeProvider,
        WeatherMetrics metrics,
        IOptions<WeatherScrapingSettings> scrapingOptions,
        ILogger logger)
    {
        HttpClient = httpClient;
        _hybridCache = hybridCache;
        TimeProvider = timeProvider;
        _metrics = metrics;
        Logger = logger;

        var settings = scrapingOptions.Value;
        _todayOptions = ExpiringAfter(TimeSpan.FromMinutes(settings.TodayCacheMinutes));
        _extendedOptions = ExpiringAfter(TimeSpan.FromHours(settings.ExtendedCacheHours));
        _negativeOptions = ExpiringAfter(TimeSpan.FromMinutes(settings.NegativeCacheMinutes));
    }

    public abstract string ProviderName { get; }

    public abstract TimeZoneInfo TimeZone { get; }

    /// <summary>
    /// Scrapes a contiguous range of forecast days, from <paramref name="fromDayOffset"/> to
    /// <paramref name="toDayOffset"/> inclusive, where offset 0 is the provider-local current day.
    /// </summary>
    /// <param name="location">Canonical location spelling (see <see cref="LocationName.Normalize"/>).</param>
    /// <param name="fromDayOffset">First day offset to scrape (0 = today).</param>
    /// <param name="toDayOffset">Last day offset to scrape, inclusive.</param>
    /// <param name="ct">Cancellation token.</param>
    protected abstract Task<IEnumerable<DayWeather>> ScrapeAsync(
        string location, int fromDayOffset, int toDayOffset, CancellationToken ct);

    public async Task<ScrapedForecast> GetForecastAsync(
        string location,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        // One canonical spelling feeds both cache keys and the scrape, so "San Pellegrino Terme"
        // and "san-pellegrino-terme" share a single set of cache entries and a single scrape.
        var normalizedLocation = LocationName.Normalize(location);
        var todayKey = CacheKeyFor(normalizedLocation, TodaySegment);
        var extendedKey = CacheKeyFor(normalizedLocation, ExtendedSegment);

        if (forceRefresh)
            return await ScrapeWholeWeekAndCacheAsync(normalizedLocation, todayKey, extendedKey, ct);

        // The following-days segment is the anchor: on a miss it drives a single scrape of the
        // whole week (today + following days). GetOrCreateAsync writes the factory result with the
        // short negative TTL by default; a real forecast is promoted to the segment's positive TTL
        // once produced. The full scrape is stashed so the current-day entry can be written from
        // the same fetch — a cold request hits the provider exactly once.
        ScrapedForecast? wholeWeek = null;
        var extendedCreated = false;
        var extended = await _hybridCache.GetOrCreateAsync(
            extendedKey,
            async innerCt =>
            {
                extendedCreated = true;
                wholeWeek = await ScrapeStampedAsync(normalizedLocation, TodayDayOffset, LastExtendedDayOffset, innerCt);
                return FollowingDaysOf(wholeWeek);
            },
            _negativeOptions,
            cancellationToken: ct);

        if (extendedCreated && extended.Days.Count > 0)
            await _hybridCache.SetAsync(extendedKey, extended, _extendedOptions, cancellationToken: ct);

        ScrapedForecast today;
        if (extendedCreated)
        {
            // The whole-week scrape already fetched today; write it without a second provider hit.
            today = CurrentDayOf(wholeWeek!);
            await _hybridCache.SetAsync(
                todayKey, today,
                today.Days.Count > 0 ? _todayOptions : _negativeOptions,
                cancellationToken: ct);
        }
        else
        {
            // The following days were served from cache; refresh only the current day if its
            // shorter-lived entry has expired, scraping a single page rather than the whole week.
            var todayCreated = false;
            today = await _hybridCache.GetOrCreateAsync(
                todayKey,
                async innerCt =>
                {
                    todayCreated = true;
                    return await ScrapeStampedAsync(normalizedLocation, TodayDayOffset, TodayDayOffset, innerCt);
                },
                _negativeOptions,
                cancellationToken: ct);

            if (todayCreated && today.Days.Count > 0)
                await _hybridCache.SetAsync(todayKey, today, _todayOptions, cancellationToken: ct);
        }

        return Compose(today, extended);
    }

    /// <summary>
    /// Seeds the cache with a forecast obtained from an external source (e.g. JSON files written
    /// on a previous run) so it can be served immediately, without a scrape. The days are split
    /// into the current-day and following-days segments exactly as a live scrape would, and each
    /// keeps its original <see cref="ScrapedForecast.ScrapedAt"/>, so freshness still reflects the
    /// original scrape; a later cache miss or <c>forceRefresh</c> re-scrapes normally.
    /// </summary>
    public async Task SeedAsync(string location, ScrapedForecast forecast, CancellationToken ct = default)
    {
        // Never seed an empty forecast: that would mask a genuine "unknown location" behind stale
        // emptiness for the full positive TTL. An empty result belongs in the short negative
        // cache, reached only via an actual scrape.
        if (forecast.Days.Count == 0)
            return;

        var normalizedLocation = LocationName.Normalize(location);
        var today = CurrentDayOf(forecast);
        var extended = FollowingDaysOf(forecast);

        if (today.Days.Count > 0)
            await _hybridCache.SetAsync(
                CacheKeyFor(normalizedLocation, TodaySegment), today, _todayOptions, cancellationToken: ct);

        if (extended.Days.Count > 0)
            await _hybridCache.SetAsync(
                CacheKeyFor(normalizedLocation, ExtendedSegment), extended, _extendedOptions, cancellationToken: ct);
    }

    // Scrapes the whole week once and writes both segments from that single fetch, so freshness is
    // consistent across all days. Empty segments fall back to the short negative TTL.
    private async Task<ScrapedForecast> ScrapeWholeWeekAndCacheAsync(
        string normalizedLocation, string todayKey, string extendedKey, CancellationToken ct)
    {
        var wholeWeek = await ScrapeStampedAsync(normalizedLocation, TodayDayOffset, LastExtendedDayOffset, ct);
        var today = CurrentDayOf(wholeWeek);
        var extended = FollowingDaysOf(wholeWeek);

        await _hybridCache.SetAsync(
            todayKey, today, today.Days.Count > 0 ? _todayOptions : _negativeOptions, cancellationToken: ct);
        await _hybridCache.SetAsync(
            extendedKey, extended, extended.Days.Count > 0 ? _extendedOptions : _negativeOptions, cancellationToken: ct);

        return Compose(today, extended);
    }

    // Splits a forecast into its current-day part (the provider-local today) ...
    private ScrapedForecast CurrentDayOf(ScrapedForecast forecast)
    {
        var today = this.GetLocalToday(TimeProvider);
        return new ScrapedForecast
        {
            ScrapedAt = forecast.ScrapedAt,
            Days = [.. forecast.Days.Where(d => d.Date == today)]
        };
    }

    // ... and its following-days part (every day after the provider-local today).
    private ScrapedForecast FollowingDaysOf(ScrapedForecast forecast)
    {
        var today = this.GetLocalToday(TimeProvider);
        return new ScrapedForecast
        {
            ScrapedAt = forecast.ScrapedAt,
            Days = [.. forecast.Days.Where(d => d.Date > today)]
        };
    }

    // Merges the two segments into a single forecast. The freshness stamp is the most recent scrape
    // among the segments that actually contributed days, so an empty segment never drags the stamp
    // to a scrape time whose data is not in the result.
    private static ScrapedForecast Compose(ScrapedForecast today, ScrapedForecast extended)
    {
        var contributing = new[] { today, extended }.Where(f => f.Days.Count > 0).ToList();

        return new ScrapedForecast
        {
            ScrapedAt = contributing.Count > 0 ? contributing.Max(f => f.ScrapedAt) : default,
            Days = [.. contributing.SelectMany(f => f.Days).OrderBy(d => d.Date)]
        };
    }

    // Cache key is derived from the provider name, the canonical location spelling and the segment,
    // so every surface that touches the cache (scrape, serve, seed) agrees on the same entries.
    private string CacheKeyFor(string normalizedLocation, string segment)
        => $"{ProviderName.ToLowerInvariant()}:{normalizedLocation}:{segment}";

    private async Task<ScrapedForecast> ScrapeStampedAsync(
        string normalizedLocation, int fromDayOffset, int toDayOffset, CancellationToken ct)
    {
        // Stamp the scrape time and measure the scrape with the injected clock so both are
        // deterministic under a fake TimeProvider. A throwing scrape is counted as a failure
        // (with its elapsed time) and the exception is rethrown so callers still see it.
        var scrapedAt = TimeProvider.GetUtcNow();
        var start = TimeProvider.GetTimestamp();

        List<DayWeather> days;
        try
        {
            days = (await ScrapeAsync(normalizedLocation, fromDayOffset, toDayOffset, ct)).ToList();
        }
        catch
        {
            _metrics.RecordScrapeFailure(
                ProviderName, TimeProvider.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }

        // An empty result is a successful scrape that found nothing (handled by the negative
        // cache), not a failure — only a thrown exception counts as a failure.
        _metrics.RecordScrapeSuccess(
            ProviderName, TimeProvider.GetElapsedTime(start).TotalMilliseconds);

        return new ScrapedForecast { ScrapedAt = scrapedAt, Days = days };
    }

    private static HybridCacheEntryOptions ExpiringAfter(TimeSpan ttl) => new()
    {
        Expiration = ttl,
        LocalCacheExpiration = ttl
    };
}
