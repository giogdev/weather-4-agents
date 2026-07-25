using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace Weather4Agents.Infrastructure.Diagnostics;

/// <summary>
/// Basic scrape observability via <see cref="System.Diagnostics.Metrics"/>. A single
/// <see cref="Meter"/> named <see cref="MeterName"/> emits counters and a histogram that any
/// out-of-process listener (e.g. <c>dotnet-counters</c>) or an OpenTelemetry exporter can pick
/// up by meter name. Full OpenTelemetry wiring is intentionally out of scope; this is the
/// minimal instrumentation asked for by the milestone (L6).
/// </summary>
public sealed class WeatherMetrics : IDisposable
{
    /// <summary>Meter name listeners subscribe to.</summary>
    public const string MeterName = "Weather4Agents";

    private readonly Meter _meter;
    // When the meter comes from an IMeterFactory the factory owns its lifetime and disposes it;
    // only a meter this type created itself (the test fallback) should be disposed here.
    private readonly bool _ownsMeter;
    private readonly Counter<long> _scrapeSuccess;
    private readonly Counter<long> _scrapeFailure;
    private readonly Histogram<double> _scrapeDuration;
    private readonly Counter<long> _unknownWeatherSlots;

    /// <param name="meterFactory">
    /// The DI meter factory. Optional so tests that do not care about metrics can construct the
    /// type directly with a standalone meter; the host always supplies a factory (via
    /// <c>AddMetrics</c>), which scopes the meter for isolated collection.
    /// </param>
    public WeatherMetrics(IMeterFactory? meterFactory = null)
    {
        _ownsMeter = meterFactory is null;
        _meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

        _scrapeSuccess = _meter.CreateCounter<long>(
            "weather.scrape.success", unit: "{scrape}",
            description: "Number of scrapes that completed without throwing.");

        _scrapeFailure = _meter.CreateCounter<long>(
            "weather.scrape.failure", unit: "{scrape}",
            description: "Number of scrapes that threw before producing a result.");

        _scrapeDuration = _meter.CreateHistogram<double>(
            "weather.scrape.duration", unit: "ms",
            description: "Wall-clock duration of a scrape, whether it succeeded or failed.");

        _unknownWeatherSlots = _meter.CreateCounter<long>(
            "weather.mapping.unknown", unit: "{slot}",
            description: "Number of forecast slots whose description mapped to WeatherType.Unknown.");
    }

    /// <summary>Records a scrape that completed without throwing.</summary>
    public void RecordScrapeSuccess(string provider, double durationMs)
    {
        var tag = new KeyValuePair<string, object?>("provider", provider);
        _scrapeSuccess.Add(1, tag);
        _scrapeDuration.Record(durationMs, tag, new("outcome", "success"));
    }

    /// <summary>Records a scrape that threw before producing a result.</summary>
    public void RecordScrapeFailure(string provider, double durationMs)
    {
        var tag = new KeyValuePair<string, object?>("provider", provider);
        _scrapeFailure.Add(1, tag);
        _scrapeDuration.Record(durationMs, tag, new("outcome", "failure"));
    }

    /// <summary>Records a forecast slot that could not be mapped to a known weather type.</summary>
    public void RecordUnknownWeatherSlot(string provider)
        => _unknownWeatherSlots.Add(1, new KeyValuePair<string, object?>("provider", provider));

    public void Dispose()
    {
        if (_ownsMeter)
            _meter.Dispose();
    }
}
