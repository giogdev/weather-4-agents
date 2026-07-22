using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.DTOs;

/// <summary>
/// Response for the single-day forecast endpoint. Wraps the day entry in the same freshness +
/// timezone envelope as the other forecast endpoints, so no domain entity is exposed on the wire.
/// </summary>
public class DayWeatherResponse : IFreshnessStamped
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
    /// <summary>The forecast for the requested day.</summary>
    public DayForecastEntry Day { get; set; } = new();

    /// <summary>
    /// Extracts the requested <paramref name="date"/> from a domain <see cref="ScrapedForecast"/>,
    /// returning <c>null</c> when the forecast has no such day (the endpoint maps that to a 404).
    /// Freshness and timezone come from <see cref="ForecastEnvelope"/>, shared with
    /// <see cref="ForecastResponse"/>.
    /// </summary>
    public static DayWeatherResponse? From(ScrapedForecast scraped, DateOnly date)
    {
        var day = scraped.Days.FirstOrDefault(d => d.Date == date);
        if (day is null)
            return null;

        return new DayWeatherResponse
        {
            LastUpdatedAt = scraped.ScrapedAt,
            Timezone = ForecastEnvelope.Timezone(scraped),
            Day = DayForecastEntry.From(day)
        };
    }
}
