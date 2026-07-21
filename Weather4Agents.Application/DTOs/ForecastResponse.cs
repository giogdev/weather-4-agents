using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.DTOs;

/// <summary>
/// Response for the multi-day forecast endpoint. Mirrors <see cref="WeekForecastResponse"/> so the
/// two endpoints that used to return the <see cref="DayWeather"/> domain entity directly now expose
/// the same envelope (freshness + timezone + day entries) as the week/next-24h endpoints.
/// </summary>
public class ForecastResponse
{
    /// <summary>
    /// When the underlying data was scraped from the provider (UTC), not when this response was
    /// produced: a response served from cache reports the original scrape time.
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; set; }
    /// <summary>
    /// IANA identifier of the provider's timezone (e.g. "Europe/Rome").
    /// All dates and times in the forecast are local to this timezone.
    /// </summary>
    public string Timezone { get; set; } = string.Empty;
    public IEnumerable<DayForecastEntry> Forecast { get; set; } = [];

    /// <summary>
    /// Maps a domain <see cref="ScrapedForecast"/> onto the wire contract. Freshness and timezone
    /// come from <see cref="ForecastEnvelope"/>, shared with <see cref="DayWeatherResponse"/> so the
    /// two entity-derived endpoints report them identically.
    /// </summary>
    public static ForecastResponse From(ScrapedForecast scraped) => new()
    {
        LastUpdatedAt = scraped.ScrapedAt,
        Timezone = ForecastEnvelope.Timezone(scraped),
        Forecast = scraped.Days.Select(DayForecastEntry.From).ToList()
    };
}
