using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.DTOs;

public class WeekForecastResponse
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
}

public class DayForecastEntry
{
    public DateOnly Date { get; set; }
    /// <summary>
    /// Accuracy (0-100%)
    /// </summary>
    public int ReliabilityPerc { get; set; } = 100;
    public IEnumerable<HoursWeatherDetails> HoursDetails { get; set; } = [];

    /// <summary>Projects a domain <see cref="DayWeather"/> onto the wire entry.</summary>
    public static DayForecastEntry From(DayWeather day) => new()
    {
        Date = day.Date,
        ReliabilityPerc = day.ReliabilityPerc,
        HoursDetails = day.HoursDetails
    };
}
