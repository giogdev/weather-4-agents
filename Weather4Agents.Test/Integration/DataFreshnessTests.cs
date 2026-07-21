using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.UseCases.ScrapeAndCache;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// Freshness at the HTTP seam (ticket 10): <c>lastUpdatedAt</c> reflects when the data was
/// scraped, not when the response was produced. A response served from cache keeps reporting
/// the original scrape time however much wall-clock time has passed; only an actual re-scrape
/// (a forced refresh, the scraping job's path) moves it forward.
/// </summary>
public class DataFreshnessTests
{
    private static DayWeather Day(DateOnly date) => new()
    {
        Date = date,
        Provider = new WeatherProvider(FakeWeatherProviderScraper.Name),
        ReliabilityPerc = 90,
        HoursDetails =
        [
            new HoursWeatherDetails
            {
                TimeFrom = new TimeOnly(8, 0),
                TimeTo = new TimeOnly(9, 0),
                WeatherType = WeatherType.Sunny,
                TemperatureC = 22
            }
        ]
    };

    private static async Task<DateTimeOffset> ReadLastUpdatedAt(HttpClient client)
    {
        var response = await client.GetAsync("/api/weather/bergamo/forecast/week");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return (DateTimeOffset)body["lastUpdatedAt"]!;
    }

    [Fact]
    public async Task WeekForecast_ServedFromCache_ReportsTheScrapeTimeNotTheRequestTime()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("bergamo", Day(new DateOnly(2026, 5, 14)));
        using var client = factory.CreateClient();

        // First request scrapes at the pinned clock: that is the reported freshness.
        var firstReported = await ReadLastUpdatedAt(client);
        Assert.Equal(Weather4AgentsApiFactory.InitialTime, firstReported);

        // Ten hours later the entry is still cached — no re-scrape — so the timestamp is unchanged.
        factory.Clock.Advance(TimeSpan.FromHours(10));
        var cachedReported = await ReadLastUpdatedAt(client);

        Assert.Equal(1, factory.Scraper.ScrapeCount);
        Assert.Equal(Weather4AgentsApiFactory.InitialTime, cachedReported);
    }

    [Fact]
    public async Task WeekForecast_AfterAForcedRefresh_ReportsTheNewScrapeTime()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("bergamo", Day(new DateOnly(2026, 5, 14)));
        using var client = factory.CreateClient();

        await ReadLastUpdatedAt(client);

        // The scraping job re-scrapes via ScrapeAndCacheCommand (forceRefresh) — ten hours on,
        // that stamps a new scrape time which subsequent responses must report.
        factory.Clock.Advance(TimeSpan.FromHours(10));
        using (var scope = factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.SendAsync(
                new ScrapeAndCacheCommand("bergamo", FakeWeatherProviderScraper.Name));
        }

        var refreshedReported = await ReadLastUpdatedAt(client);

        Assert.Equal(2, factory.Scraper.ScrapeCount);
        Assert.Equal(Weather4AgentsApiFactory.InitialTime.AddHours(10), refreshedReported);
    }
}
