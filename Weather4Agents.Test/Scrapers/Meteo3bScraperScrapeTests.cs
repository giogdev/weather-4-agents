using System.Net;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Weather4Agents.Infrastructure.Scrapers;

namespace Weather4Agents.Test.Scrapers;

/// <summary>
/// End-to-end scrape behaviour of <see cref="Meteo3bScraper"/> over a stubbed HTTP transport:
/// a failed or timed-out day page is skipped (not fatal) and produces a warning log with the URL.
/// </summary>
public class Meteo3bScraperScrapeTests
{
    private static string CompleteDayHtml =>
        File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-v3-complete1.html"));

    /// <summary>
    /// Routes requests by the trailing path segment: <c>/1</c> times out, <c>/2</c> fails with an
    /// <see cref="HttpRequestException"/>, everything else returns a full hourly day page.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _dayHtml;
        public StubHandler(string dayHtml) => _dayHtml = dayHtml;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var lastSegment = request.RequestUri!.Segments[^1].Trim('/');
            return lastSegment switch
            {
                "1" => throw new TaskCanceledException("Simulated HTTP timeout."),
                "2" => throw new HttpRequestException("Simulated connection failure."),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_dayHtml)
                })
            };
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private static HybridCache BuildCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task ScrapeAsync_WhenSomeDaysTimeoutOrFail_SkipsThemAndScrapesTheRest()
    {
        var logger = new CapturingLogger<Meteo3bScraper>();
        var mapper = new Meteo3bWeatherTypeMapper(NullLogger<Meteo3bWeatherTypeMapper>.Instance);
        var httpClient = new HttpClient(new StubHandler(CompleteDayHtml));
        var scraper = new Meteo3bScraper(httpClient, BuildCache(), mapper, TimeProvider.System, logger);

        var result = (await scraper.GetForecastAsync("milano", forceRefresh: true)).Days;

        // Days 0 and 3..7 succeed (6 days); day 1 (timeout) and day 2 (HTTP error) are skipped,
        // proving a timeout on one day does not abort the whole scrape.
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public async Task ScrapeAsync_WhenADayTimesOut_LogsAWarningWithTheUrl()
    {
        var logger = new CapturingLogger<Meteo3bScraper>();
        var mapper = new Meteo3bWeatherTypeMapper(NullLogger<Meteo3bWeatherTypeMapper>.Instance);
        var httpClient = new HttpClient(new StubHandler(CompleteDayHtml));
        var scraper = new Meteo3bScraper(httpClient, BuildCache(), mapper, TimeProvider.System, logger);

        await scraper.GetForecastAsync("milano", forceRefresh: true);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        // One warning for the timed-out day and one for the failed day.
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Message.Contains("Timed out") && w.Message.Contains("/milano/1"));
        Assert.Contains(warnings, w => w.Message.Contains("Failed to fetch") && w.Message.Contains("/milano/2"));
        Assert.All(warnings, w => Assert.NotNull(w.Exception));
    }

    [Fact]
    public async Task ScrapeAsync_JustPastItalianMidnightOnAUtcHost_DatesDaysFromTheItalianToday()
    {
        // 2026-05-14 22:30 UTC = 2026-05-15 00:30 in Italy (CEST): day 0 of the scrape is the
        // Italian May 15, not the UTC May 14 the host clock would suggest.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 14, 22, 30, 0, TimeSpan.Zero));
        var mapper = new Meteo3bWeatherTypeMapper(NullLogger<Meteo3bWeatherTypeMapper>.Instance);
        var httpClient = new HttpClient(new StubHandler(CompleteDayHtml));
        var scraper = new Meteo3bScraper(
            httpClient, BuildCache(), mapper, clock, NullLogger<Meteo3bScraper>.Instance);

        var result = (await scraper.GetForecastAsync("milano", forceRefresh: true)).Days;

        Assert.Equal(new DateOnly(2026, 5, 15), result.Min(d => d.Date));
        // The timezone rides along on each day so raw DayWeather responses stay interpretable.
        Assert.All(result, d => Assert.Equal("Europe/Rome", d.Provider.TimeZoneId));
    }
}
