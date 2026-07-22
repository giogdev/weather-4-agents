using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Infrastructure.Diagnostics;
using Weather4Agents.Infrastructure.Scrapers.Base;

namespace Weather4Agents.Test.Scrapers;

/// <summary>
/// Caching policy of <see cref="BaseWeatherScraper"/>: empty scrapes are negative-cached with a
/// short TTL so they are retried within minutes, while a real forecast keeps the standard 24h TTL.
///
/// Two complementary lenses are used because HybridCache's in-memory expiry is clock-driven, not
/// count-driven: a controllable cache clock proves the expiry → re-scrape behaviour end to end,
/// and a capturing L2 store proves the exact TTL value handed to the cache.
/// </summary>
public class BaseWeatherScraperCachingTests
{
    private sealed class CountingScraper : BaseWeatherScraper
    {
        private readonly Func<IEnumerable<DayWeather>> _factory;

        public CountingScraper(HybridCache cache, Func<IEnumerable<DayWeather>> factory)
            : base(new HttpClient(), cache, TimeProvider.System, new WeatherMetrics(), NullLogger<CountingScraper>.Instance)
            => _factory = factory;

        public int ScrapeCount { get; private set; }

        public string? LastScrapedLocation { get; private set; }

        public override string ProviderName => "Counting";

        public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

        protected override Task<IEnumerable<DayWeather>> ScrapeAsync(string location, CancellationToken ct)
        {
            ScrapeCount++;
            LastScrapedLocation = location;
            return Task.FromResult(_factory());
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
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24),
                LocalCacheExpiration = TimeSpan.FromHours(24)
            };
        });
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
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24),
                LocalCacheExpiration = TimeSpan.FromHours(24)
            };
        });
        return (services.BuildServiceProvider().GetRequiredService<HybridCache>(), l2);
    }

    private static DayWeather AnyDay() => new()
    {
        Date = new DateOnly(2026, 5, 14),
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
        var scraper = new CountingScraper(BuildClockDrivenCache(clock), FreshSource(out var source));

        var first = await scraper.GetForecastAsync("somewhere");
        Assert.Empty(first.Days);
        Assert.Equal(1, scraper.ScrapeCount);

        // Within the ~5-minute negative-cache window the empty result is served without re-scraping.
        clock.Advance(TimeSpan.FromMinutes(4));
        await scraper.GetForecastAsync("somewhere");
        Assert.Equal(1, scraper.ScrapeCount);

        // Past the window the location is scraped again — and now returns data.
        clock.Advance(TimeSpan.FromMinutes(2));
        source.Add(AnyDay());
        var afterExpiry = await scraper.GetForecastAsync("somewhere");

        Assert.Equal(2, scraper.ScrapeCount);
        Assert.NotEmpty(afterExpiry.Days);
    }

    [Fact]
    public async Task GetForecastAsync_WhenScrapeHasData_IsStillServedFromCacheAfterTheNegativeWindow()
    {
        var clock = new MutableClock();
        var scraper = new CountingScraper(BuildClockDrivenCache(clock), () => new[] { AnyDay() });

        var first = await scraper.GetForecastAsync("bergamo");
        Assert.NotEmpty(first.Days);
        Assert.Equal(1, scraper.ScrapeCount);

        // Long past the 5-minute negative window a real forecast is still cached (24h TTL): no re-scrape.
        clock.Advance(TimeSpan.FromMinutes(30));
        var later = await scraper.GetForecastAsync("bergamo");

        Assert.Equal(1, scraper.ScrapeCount);
        Assert.NotEmpty(later.Days);
    }

    // ── Location normalization ───────────────────────────────────────────────────

    [Fact]
    public async Task GetForecastAsync_SpacedAndHyphenatedSpellings_ShareOneScrapeAndOneCacheEntry()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(cache, () => new[] { AnyDay() });

        await scraper.GetForecastAsync("San Pellegrino Terme");
        await scraper.GetForecastAsync("san-pellegrino-terme");

        Assert.Equal(1, scraper.ScrapeCount);
        Assert.Single(l2.LastExpiration);
        // The provider is handed the same canonical spelling used for the cache key,
        // so the scraped URL and the cache entry can never diverge.
        Assert.Equal("san-pellegrino-terme", scraper.LastScrapedLocation);
    }

    // ── Exact TTL handed to the cache ────────────────────────────────────────────

    [Fact]
    public async Task GetForecastAsync_WhenScrapeIsEmpty_IsNegativeCachedWithFiveMinuteTtl()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(cache, Array.Empty<DayWeather>);

        await scraper.GetForecastAsync("somewhere");

        var ttl = Assert.Single(l2.LastExpiration).Value;
        Assert.Equal(TimeSpan.FromMinutes(5), ttl);
    }

    [Fact]
    public async Task GetForecastAsync_WhenScrapeHasData_IsCachedWithTheStandardDayTtl()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(cache, () => new[] { AnyDay() });

        await scraper.GetForecastAsync("bergamo");

        // A real forecast is promoted to the standard 24h TTL, not the short negative-cache TTL.
        var ttl = Assert.Single(l2.LastExpiration).Value;
        Assert.Equal(TimeSpan.FromHours(24), ttl);
    }

    [Fact]
    public async Task GetForecastAsync_WithForceRefreshOnEmptyScrape_UsesTheFiveMinuteTtl()
    {
        var (cache, l2) = BuildL2CapturingCache();
        var scraper = new CountingScraper(cache, Array.Empty<DayWeather>);

        await scraper.GetForecastAsync("somewhere", forceRefresh: true);

        var ttl = Assert.Single(l2.LastExpiration).Value;
        Assert.Equal(TimeSpan.FromMinutes(5), ttl);
    }

    private static Func<IEnumerable<DayWeather>> FreshSource(out List<DayWeather> source)
    {
        var list = new List<DayWeather>();
        source = list;
        return () => list;
    }
}
