using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Application.Settings;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.ValueObjects;
using Weather4Agents.Infrastructure.Diagnostics;
using Weather4Agents.Infrastructure.Scrapers.Base;

namespace Weather4Agents.Test.Scrapers;

/// <summary>
/// Caching policy of <see cref="BaseWeatherScraper"/>: the forecast is cached as two segments with
/// independent lifetimes — the current day (short TTL, refreshed often) and the following days
/// (long TTL). A cold request scrapes the whole week once and populates both; once the current-day
/// entry expires on its own, only that single page is re-scraped. Empty scrapes are negative-cached
/// with a short TTL so they are retried within minutes.
///
/// Two complementary lenses are used because HybridCache's in-memory expiry is clock-driven, not
/// count-driven: a controllable cache clock proves the expiry → re-scrape behaviour end to end,
/// and a capturing L2 store proves the exact TTL value handed to the cache.
/// </summary>
public class BaseWeatherScraperCachingTests
{
    private sealed class CountingScraper : BaseWeatherScraper
    {
        private readonly Func<bool> _hasData;

        public CountingScraper(HybridCache cache, Func<bool> hasData, IOptions<WeatherScrapingSettings>? options = null)
            : base(new HttpClient(), cache, TimeProvider.System, new WeatherMetrics(),
                options ?? TestScrapingOptions.Default, NullLogger<CountingScraper>.Instance)
            => _hasData = hasData;

        public int ScrapeCount { get; private set; }

        public List<(int From, int To)> ScrapedRanges { get; } = [];

        public string? LastScrapedLocation { get; private set; }

        public override string ProviderName => "Counting";

        public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

        protected override Task<IEnumerable<DayWeather>> ScrapeAsync(
            string location, int fromDayOffset, int toDayOffset, CancellationToken ct)
        {
            ScrapeCount++;
            ScrapedRanges.Add((fromDayOffset, toDayOffset));
            LastScrapedLocation = location;

            if (!_hasData())
                return Task.FromResult<IEnumerable<DayWeather>>([]);

            // One day per requested offset, dated from the provider-local today, like the real scraper.
            var today = this.GetLocalToday(TimeProvider);
            var days = Enumerable.Range(fromDayOffset, toDayOffset - fromDayOffset + 1)
                .Select(offset => DayAt(today.AddDays(offset)))
                .ToList();
            return Task.FromResult<IEnumerable<DayWeather>>(days);
        }
    }

#pragma warning disable CS0618 // ISystemClock is obsolete, but it is the only clock hook MemoryCache exposes for pinning L1 expiry deterministically in tests.
    private sealed class MutableClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 5, 14, 8, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }

    /// <summary>An L1-only cache whose expiry is driven by a clock the test controls.</summary>
    private static HybridCache BuildClockDrivenCache(MutableClock clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions { Clock = clock }));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
#pragma warning restore CS0618

    /// <summary>Minimal in-memory L2 that records the expiration used for the last write of each key.</summary>
    private sealed class CapturingDistributedCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _store = new();
        public ConcurrentDictionary<string, TimeSpan?> LastExpiration { get; } = new();

        public byte[]? Get(string key) => _store.TryGetValue(key, out var v) ? v : null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _store[key] = value;
            LastExpiration[key] = options.AbsoluteExpirationRelativeToNow;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.TryRemove(key, out _);
        public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
    }

    private static (HybridCache cache, CapturingDistributedCache l2) BuildL2CapturingCache()
    {
        var l2 = new CapturingDistributedCache();
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache>(l2);
        services.AddHybridCache();
        return (services.BuildServiceProvider().GetRequiredService<HybridCache>(), l2);
    }

    private static DayWeather DayAt(DateOnly date) => new()
    {
        Date = date,
        Provider = new WeatherProvider("Counting"),
        HoursDetails =
        [
            new HoursWeatherDetails { TimeFrom = new TimeOnly(8, 0), TimeTo = new TimeOnly(9, 0) }
        ]
    };

    // ── Behaviour: expiry → re-scrape ───────────────────────────────────────────

    [Fact]
    public async Task GetForecastAsync_AfterAnEmptyScrape_ReScrapesOnceTheShortTtlHasElapsed()
    {
        var clock = new MutableClock();
        var hasData = false;
        var scraper = new CountingScraper(BuildClockDrivenCache(clock), () => hasData);

        var first = await scraper.GetForecastAsync("somewhere");
        Assert.Empty(first.Days);
        Assert.Equal(1, scraper.ScrapeCount);

        // Within the ~5-minute negative-cache window the empty result is served without re-scraping.
        clock.Advance(TimeSpan.FromMinutes(4));
        await scraper.GetForecastAsync("somewhere");
        Assert.Equal(1, scraper.ScrapeCount);

        // Past the window the location is scraped again — and now returns data.
        clock.Advance(TimeSpan.FromMinutes(2));
        hasData = true;
        var afterExpiry = await scraper.GetForecastAsync("somewhere");

        Assert.Equal(2, scraper.ScrapeCount);
        Assert.NotEmpty(afterExpiry.Days);
    }

    [Fact]
    public async Task GetForecastAsync_WhenScrapeHasData_IsStillServedFromCacheAfterTheNegativeWindow()
    {
        var clock = new MutableClock();
        var scraper = new CountingScraper(BuildClockDrivenCache(clock), () => true);

        var first = await scraper.GetForecastAsync("bergamo");
        Assert.NotEmpty(first.Days);
        Assert.Equal(1, scraper.ScrapeCount);

        // Past the 5-minute negative window but within both positive TTLs: still cached, no re-scrape.
        clock.Advance(TimeSpan.FromMinutes(20));
        var later = await scraper.GetForecastAsync("bergamo");

        Assert.Equal(1, scraper.ScrapeCount);
        Assert.NotEmpty(later.Days);
    }

    [Fact]
    public async Task GetForecastAsync_WhenCurrentDayTtlElapses_ReScrapesOnlyTheCurrentDay()
    {
        var clock = new MutableClock();
        var scraper = new CountingScraper(BuildClockDrivenCache(clock), () => true);

        await scraper.GetForecastAsync("bergamo");
        Assert.Equal(1, scraper.ScrapeCount);
        // Cold request scrapes the whole week in one pass.
        Assert.Equal((0, ForecastLimits.MaxDays - 1), scraper.ScrapedRanges[0]);

        // Past the 30-minute current-day TTL but within the 6-hour following-days TTL.
        clock.Advance(TimeSpan.FromMinutes(31));
        await scraper.GetForecastAsync("bergamo");

        Assert.Equal(2, scraper.ScrapeCount);
        // Only the current day (offset 0) is re-scraped; the following days are served from cache.
        Assert.Equal((0, 0), scraper.ScrapedRanges[1]);
    }

    [Fact]
    public async Task GetForecastAsync_WhenFollowingDaysTtlElapses_ReScrapesTheWholeWeek()
    {
        var clock = new MutableClock();
        var scraper = new CountingScraper(BuildClockDrivenCache(clock), () => true);

        await scraper.GetForecastAsync("bergamo");
        Assert.Equal(1, scraper.ScrapeCount);

        // Past the 6-hour following-days TTL: the anchor segment misses, driving a whole-week scrape.
        clock.Advance(TimeSpan.FromHours(6) + TimeSpan.FromMinutes(1));
        await scraper.GetForecastAsync("bergamo");

        Assert.Equal(2, scraper.ScrapeCount);
        Assert.Equal((0, ForecastLimits.MaxDays - 1), scraper.ScrapedRanges[1]);
    }

    // ── Location normalization ───────────────────────────────────────────────────

    [Fact]
    public async Task GetForecastAsync_SpacedAndHyphenatedSpellings_ShareOneScrapeAndTwoSegmentEntries()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(cache, () => true);

        await scraper.GetForecastAsync("San Pellegrino Terme");
        await scraper.GetForecastAsync("san-pellegrino-terme");

        // The second spelling is a pure cache hit: the provider is scraped exactly once.
        Assert.Equal(1, scraper.ScrapeCount);
        // Both spellings resolve to the same normalized location, so only its two segment entries
        // (current day + following days) are written.
        Assert.Equal(2, l2.LastExpiration.Count);
        // The provider is handed the same canonical spelling used for the cache key,
        // so the scraped URL and the cache entries can never diverge.
        Assert.Equal("san-pellegrino-terme", scraper.LastScrapedLocation);
    }

    // ── Exact TTL handed to the cache ────────────────────────────────────────────

    [Fact]
    public async Task GetForecastAsync_WhenScrapeIsEmpty_BothSegmentsAreNegativeCachedWithFiveMinuteTtl()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(cache, () => false);

        await scraper.GetForecastAsync("somewhere");

        Assert.Equal(2, l2.LastExpiration.Count);
        Assert.All(l2.LastExpiration.Values, ttl => Assert.Equal(TimeSpan.FromMinutes(5), ttl));
    }

    [Fact]
    public async Task GetForecastAsync_WhenScrapeHasData_CachesCurrentDayShortAndFollowingDaysLong()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(cache, () => true);

        await scraper.GetForecastAsync("bergamo");

        // Two segment entries: the current day at the short TTL, the following days at the long one.
        var ttls = l2.LastExpiration.Values.ToList();
        Assert.Equal(2, ttls.Count);
        Assert.Contains(TimeSpan.FromMinutes(30), ttls);
        Assert.Contains(TimeSpan.FromHours(6), ttls);
    }

    [Fact]
    public async Task GetForecastAsync_WithForceRefreshOnEmptyScrape_UsesTheFiveMinuteTtl()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(cache, () => false);

        await scraper.GetForecastAsync("somewhere", forceRefresh: true);

        Assert.Equal(2, l2.LastExpiration.Count);
        Assert.All(l2.LastExpiration.Values, ttl => Assert.Equal(TimeSpan.FromMinutes(5), ttl));
    }

    [Fact]
    public async Task GetForecastAsync_HonoursConfiguredTtls()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(
            cache, () => true, TestScrapingOptions.With(todayCacheMinutes: 15, extendedCacheHours: 6));

        await scraper.GetForecastAsync("bergamo");

        var ttls = l2.LastExpiration.Values.ToList();
        Assert.Contains(TimeSpan.FromMinutes(15), ttls);
        Assert.Contains(TimeSpan.FromHours(6), ttls);
    }
}
