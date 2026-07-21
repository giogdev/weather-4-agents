using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// In-memory <see cref="IWeatherProviderScraper"/> for integration tests.
/// Each test configures the forecasts it needs via <see cref="SetForecast"/>;
/// any unconfigured location yields an empty result (simulating an empty scrape).
/// </summary>
public class FakeWeatherProviderScraper : IWeatherProviderScraper
{
    public const string Name = "FakeProvider";

    private readonly Dictionary<string, IReadOnlyList<DayWeather>> _forecasts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _failing =
        new(StringComparer.OrdinalIgnoreCase);

    public string ProviderName => Name;

    // Same timezone as the real provider so timezone-sensitive tests exercise the same math.
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public void SetForecast(string location, params DayWeather[] days)
        => _forecasts[location] = days;

    /// <summary>
    /// Makes <see cref="GetForecastAsync"/> throw an unexpected exception for the given
    /// location, simulating an unhandled failure deep in the pipeline.
    /// </summary>
    public void FailFor(string location)
        => _failing.Add(location);

    public Task<IEnumerable<DayWeather>> GetForecastAsync(
        string location,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (_failing.Contains(location))
            throw new InvalidOperationException($"Simulated scraper failure for '{location}'.");

        return Task.FromResult<IEnumerable<DayWeather>>(
            _forecasts.TryGetValue(location, out var days) ? days : []);
    }
}
