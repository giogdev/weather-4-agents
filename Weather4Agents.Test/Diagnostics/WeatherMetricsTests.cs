using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Weather4Agents.Infrastructure.Diagnostics;
using Weather4Agents.Infrastructure.Scrapers;
using Weather4Agents.Test.Integration;

namespace Weather4Agents.Test.Diagnostics;

/// <summary>
/// Proves the ticket-16 meters actually fire: an unmapped weather description bumps the
/// unknown-slot counter, and a scrape records success/failure plus a duration sample. Each test
/// scopes its <see cref="MetricCollector{T}"/> to its own <see cref="IMeterFactory"/> so parallel
/// test classes sharing the meter name do not cross-contaminate.
/// </summary>
public class WeatherMetricsTests
{
    [Fact]
    public void Mapper_UnknownDescription_RecordsAnUnknownSlot()
    {
        var (metrics, factory) = BuildMetrics();
        using var unknown = new MetricCollector<long>(
            factory, WeatherMetrics.MeterName, "weather.mapping.unknown");

        var mapper = new Meteo3bWeatherTypeMapper(metrics, NullLogger<Meteo3bWeatherTypeMapper>.Instance);
        mapper.Map("Tempesta di sabbia");

        Assert.Equal(1, unknown.GetMeasurementSnapshot().Sum(m => m.Value));
    }

    [Fact]
    public void Mapper_KnownDescription_RecordsNoUnknownSlot()
    {
        var (metrics, factory) = BuildMetrics();
        using var unknown = new MetricCollector<long>(
            factory, WeatherMetrics.MeterName, "weather.mapping.unknown");

        var mapper = new Meteo3bWeatherTypeMapper(metrics, NullLogger<Meteo3bWeatherTypeMapper>.Instance);
        mapper.Map("Sereno");

        Assert.Empty(unknown.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task Scrape_ThatCompletes_RecordsSuccessAndADurationSample()
    {
        var (metrics, factory) = BuildMetrics();
        using var success = new MetricCollector<long>(
            factory, WeatherMetrics.MeterName, "weather.scrape.success");
        using var duration = new MetricCollector<double>(
            factory, WeatherMetrics.MeterName, "weather.scrape.duration");

        var scraper = new FakeWeatherProviderScraper(BuildCache(), TimeProvider.System, metrics);

        // An unconfigured location scrapes to an empty result, which still counts as a completed
        // scrape (emptiness is the negative-cache concern, not a failure).
        await scraper.GetForecastAsync("milano", forceRefresh: true);

        Assert.Equal(1, success.GetMeasurementSnapshot().Sum(m => m.Value));
        Assert.Single(duration.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task Scrape_ThatThrows_RecordsFailure()
    {
        var (metrics, factory) = BuildMetrics();
        using var failure = new MetricCollector<long>(
            factory, WeatherMetrics.MeterName, "weather.scrape.failure");

        var scraper = new FakeWeatherProviderScraper(BuildCache(), TimeProvider.System, metrics);
        scraper.FailFor("milano");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scraper.GetForecastAsync("milano", forceRefresh: true));

        Assert.Equal(1, failure.GetMeasurementSnapshot().Sum(m => m.Value));
    }

    private static (WeatherMetrics Metrics, IMeterFactory Factory) BuildMetrics()
    {
        var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        return (new WeatherMetrics(factory), factory);
    }

    private static HybridCache BuildCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
