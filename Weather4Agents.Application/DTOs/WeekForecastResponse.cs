using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.DTOs;

public class WeekForecastResponse
{
    /// <summary>
    /// UTC Format
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; set; }
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
}
