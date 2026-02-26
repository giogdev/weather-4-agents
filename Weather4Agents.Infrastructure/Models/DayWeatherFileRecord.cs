using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Infrastructure.Models;

/// <summary>
/// JSON envelope written to disk for each forecast day.
/// Wraps <see cref="DayWeather"/> and adds a <see cref="LastUpdatedAt"/> timestamp.
/// </summary>
public class DayWeatherFileRecord
{
    /// <summary>UTC date and time when this file was last written.</summary>
    public DateTimeOffset LastUpdatedAt { get; set; }

    /// <summary>Full weather data for the day.</summary>
    public DayWeather Weather { get; set; } = null!;
}
