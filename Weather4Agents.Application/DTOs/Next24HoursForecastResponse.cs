using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.DTOs;

public class Next24HoursForecastResponse : IFreshnessStamped
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
    public IEnumerable<HourlyForecastEntry> Hours { get; set; } = [];
}

public class HourlyForecastEntry
{
    /// <summary>
    /// Day the hourly slot belongs to.
    /// </summary>
    public DateOnly Date { get; set; }
    /// <summary>
    /// Forecast reliability percentage (0-100) of the day this slot belongs to.
    /// </summary>
    public int ReliabilityPerc { get; set; } = 100;
    /// <summary>
    /// Hourly weather details for the slot.
    /// </summary>
    public HoursWeatherDetails Details { get; set; } = new();
}
