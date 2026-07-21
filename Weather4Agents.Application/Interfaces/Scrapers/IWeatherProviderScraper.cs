using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.Interfaces.Scrapers;

public interface IWeatherProviderScraper
{
    string ProviderName { get; }

    /// <summary>
    /// Timezone the provider publishes its forecasts in. All dates and times produced by the
    /// scraper are local to this timezone, and every "now/today" computation over its data must
    /// be performed in it — never in the host timezone, which is arbitrary (e.g. UTC containers).
    /// </summary>
    TimeZoneInfo TimeZone { get; }

    /// <summary>
    /// Forecast for <paramref name="location"/>, wrapped with the moment it was scraped.
    /// A cached result keeps its original <see cref="ScrapedForecast.ScrapedAt"/>; only an
    /// actual re-scrape (cache miss or <paramref name="forceRefresh"/>) moves it forward.
    /// </summary>
    Task<ScrapedForecast> GetForecastAsync(
        string location,
        bool forceRefresh = false,
        CancellationToken ct = default);
}
