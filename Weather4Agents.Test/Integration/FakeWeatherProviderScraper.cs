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

    public string ProviderName => Name;

    public void SetForecast(string location, params DayWeather[] days)
        => _forecasts[location] = days;

    public Task<IEnumerable<DayWeather>> GetForecastAsync(
        string location,
        bool forceRefresh = false,
        CancellationToken ct = default)
        => Task.FromResult<IEnumerable<DayWeather>>(
            _forecasts.TryGetValue(location, out var days) ? days : []);
}
