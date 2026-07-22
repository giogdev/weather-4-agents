using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.ValueObjects;
using Weather4Agents.Infrastructure.Diagnostics;
using Weather4Agents.Infrastructure.Scrapers.Base;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// In-memory scraper for integration tests. It extends <see cref="BaseWeatherScraper"/> so
/// requests flow through the real caching and location-normalization pipeline; only the
/// outbound HTTP scrape is replaced. Each test configures the forecasts it needs via
/// <see cref="SetForecast"/>; any unconfigured location yields an empty result (simulating an
/// empty scrape). <see cref="ScrapeCount"/> exposes how many scrapes actually happened, letting
/// tests distinguish cache hits from fresh scrapes.
/// </summary>
public class FakeWeatherProviderScraper : BaseWeatherScraper
{
    public const string Name = "FakeProvider";

    // Same timezone as the real provider so timezone-sensitive tests exercise the same math.
    private static readonly TimeZoneInfo ItalianTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    private readonly Dictionary<string, IReadOnlyList<DayWeather>> _forecasts = new();

    private readonly HashSet<string> _failing = new();

    public FakeWeatherProviderScraper(HybridCache hybridCache, TimeProvider timeProvider, WeatherMetrics metrics)
        : base(new HttpClient(), hybridCache, timeProvider, metrics, NullLogger<FakeWeatherProviderScraper>.Instance)
    {
    }

    public int ScrapeCount { get; private set; }

    public override string ProviderName => Name;

    public override TimeZoneInfo TimeZone => ItalianTimeZone;

    // Keys are normalized like incoming scrape requests, so tests can configure a location
    // with any spelling ("San Pellegrino Terme" or "san-pellegrino-terme").
    public void SetForecast(string location, params DayWeather[] days)
        => _forecasts[LocationName.Normalize(location)] = days;

    /// <summary>
    /// Makes the scrape throw an unexpected exception for the given location, simulating an
    /// unhandled failure deep in the pipeline.
    /// </summary>
    public void FailFor(string location)
        => _failing.Add(LocationName.Normalize(location));

    protected override Task<IEnumerable<DayWeather>> ScrapeAsync(string location, CancellationToken ct)
    {
        ScrapeCount++;

        if (_failing.Contains(location))
            throw new InvalidOperationException($"Simulated scraper failure for '{location}'.");

        return Task.FromResult<IEnumerable<DayWeather>>(
            _forecasts.TryGetValue(location, out var days) ? days : []);
    }
}
