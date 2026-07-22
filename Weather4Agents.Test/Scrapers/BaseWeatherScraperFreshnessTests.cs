using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Infrastructure.Diagnostics;
using Weather4Agents.Infrastructure.Scrapers.Base;

namespace Weather4Agents.Test.Scrapers;

/// <summary>
/// Freshness contract of <see cref="BaseWeatherScraper"/> (ticket 10): the cache stores the
/// forecast together with the moment it was scraped, so a cache hit reports the original
/// scrape time — however much wall-clock time has passed — and only an actual re-scrape
/// (e.g. a forced refresh) moves the timestamp forward.
/// </summary>
public class BaseWeatherScraperFreshnessTests
{
    private static readonly DateTimeOffset ScrapeTime =
        new(2026, 5, 14, 8, 0, 0, TimeSpan.Zero);

    private sealed class StubScraper : BaseWeatherScraper
    {
        public StubScraper(HybridCache cache, TimeProvider timeProvider)
            : base(new HttpClient(), cache, timeProvider, new WeatherMetrics(), NullLogger<StubScraper>.Instance)
        {
        }

        public int ScrapeCount { get; private set; }

        public override string ProviderName => "Stub";

        public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

        protected override Task<IEnumerable<DayWeather>> ScrapeAsync(string location, CancellationToken ct)
        {
            ScrapeCount++;
            return Task.FromResult<IEnumerable<DayWeather>>([AnyDay()]);
        }
    }

    private static HybridCache BuildCache()
    {
        var services = new ServiceCollection();
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

    private static DayWeather AnyDay() => new()
    {
        Date = new DateOnly(2026, 5, 14),
        Provider = new WeatherProvider("Stub"),
        HoursDetails =
        [
            new HoursWeatherDetails { TimeFrom = new TimeOnly(8, 0), TimeTo = new TimeOnly(9, 0) }
        ]
    };

    [Fact]
    public async Task GetForecastAsync_ServedFromCache_ReportsTheOriginalScrapeTimeNotNow()
    {
        var clock = new FakeTimeProvider(ScrapeTime);
        var scraper = new StubScraper(BuildCache(), clock);

        var first = await scraper.GetForecastAsync("bergamo");
        Assert.Equal(ScrapeTime, first.ScrapedAt);

        // Ten hours later the same entry is still cached: the reported timestamp must be the
        // moment the data was actually scraped, not the moment this request was served.
        clock.Advance(TimeSpan.FromHours(10));
        var cached = await scraper.GetForecastAsync("bergamo");

        Assert.Equal(1, scraper.ScrapeCount);
        Assert.Equal(ScrapeTime, cached.ScrapedAt);
    }

    [Fact]
    public async Task GetForecastAsync_ForcedRefresh_ReStampsTheScrapeTime()
    {
        var clock = new FakeTimeProvider(ScrapeTime);
        var scraper = new StubScraper(BuildCache(), clock);

        await scraper.GetForecastAsync("bergamo");
        clock.Advance(TimeSpan.FromHours(10));

        var refreshed = await scraper.GetForecastAsync("bergamo", forceRefresh: true);

        Assert.Equal(2, scraper.ScrapeCount);
        Assert.Equal(ScrapeTime.AddHours(10), refreshed.ScrapedAt);

        // The re-stamped time is what later cache hits report.
        var cached = await scraper.GetForecastAsync("bergamo");
        Assert.Equal(ScrapeTime.AddHours(10), cached.ScrapedAt);
    }
}
