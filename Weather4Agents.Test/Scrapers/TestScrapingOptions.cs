using Microsoft.Extensions.Options;
using Weather4Agents.Application.Settings;

namespace Weather4Agents.Test.Scrapers;

/// <summary>
/// Cache-TTL settings for scraper unit tests. Defaults match production
/// (<see cref="WeatherScrapingSettings"/>): current day 30 min, following days 12 h, empty 5 min.
/// </summary>
internal static class TestScrapingOptions
{
    public static IOptions<WeatherScrapingSettings> Default { get; } =
        Options.Create(new WeatherScrapingSettings());

    public static IOptions<WeatherScrapingSettings> With(
        int todayCacheMinutes = 30, int extendedCacheHours = 12, int negativeCacheMinutes = 5)
        => Options.Create(new WeatherScrapingSettings
        {
            TodayCacheMinutes = todayCacheMinutes,
            ExtendedCacheHours = extendedCacheHours,
            NegativeCacheMinutes = negativeCacheMinutes
        });
}
