using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.DTOs;

public class WeekForecastResponse
{
    // UTC Format
    public DateTimeOffset LastUpdatedAt { get; set; }
    public IEnumerable<DayForecastEntry> Forecast { get; set; } = [];
}

public class DayForecastEntry
{
    public DateOnly Date { get; set; }
    public IEnumerable<HoursWeatherDetails> HoursDetails { get; set; } = [];
}
