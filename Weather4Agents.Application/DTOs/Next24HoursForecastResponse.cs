using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.DTOs;

public class Next24HoursForecastResponse
{
    /// <summary>
    /// UTC Format
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; set; }
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
