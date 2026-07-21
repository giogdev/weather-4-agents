using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.DTOs;

/// <summary>
/// Shared mapping for the freshness/timezone envelope common to the forecast response DTOs, so
/// every endpoint reports the same fields the same way.
/// </summary>
internal static class ForecastEnvelope
{
    /// <summary>
    /// The IANA timezone the data was scraped in. It travels with the data on each day's provider
    /// (the scraper stamps <see cref="WeatherProvider.TimeZoneId"/> with its own timezone), so a
    /// forecast served from cache reports the timezone it was scraped in. Empty only when there is
    /// no data to describe — unreachable through the endpoints, which return 404 for an empty
    /// forecast before any mapping happens.
    /// </summary>
    public static string Timezone(ScrapedForecast scraped) =>
        scraped.Days.FirstOrDefault()?.Provider.TimeZoneId ?? string.Empty;
}
