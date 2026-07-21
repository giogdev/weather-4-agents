namespace Weather4Agents.Domain.Entities;

/// <summary>
/// A provider forecast together with the moment it was observed. Carrying the scrape time
/// alongside the days lets every surface that serves the data (API responses, JSON files on
/// disk) report an honest "last updated" — the moment of the scrape, not the moment the data
/// happened to be served.
/// </summary>
public class ScrapedForecast
{
    /// <summary>UTC date and time when the provider was actually scraped.</summary>
    public DateTimeOffset ScrapedAt { get; set; }

    /// <summary>Forecast days produced by that scrape.</summary>
    public List<DayWeather> Days { get; set; } = [];
}
