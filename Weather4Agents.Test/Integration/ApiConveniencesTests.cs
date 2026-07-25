using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.UseCases.ScrapeAndCache;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// API conveniences (ticket 17): responses carry a scrape-timestamp <c>ETag</c> and a
/// revalidation <c>Cache-Control</c> so a conditional request returns <c>304 Not Modified</c>
/// while nothing changed; the ETag rotates when the data is re-scraped; and a <c>today</c>
/// shortcut returns the current day's weather (in the provider timezone) identically to the
/// explicit date endpoint.
/// </summary>
public class ApiConveniencesTests
{
    private static DayWeather Day(DateOnly date, double temperatureC = 20) => new()
    {
        Date = date,
        Provider = new WeatherProvider(FakeWeatherProviderScraper.Name) { TimeZoneId = "Europe/Rome" },
        ReliabilityPerc = 85,
        HoursDetails =
        [
            new HoursWeatherDetails
            {
                TimeFrom = new TimeOnly(8, 0),
                TimeTo = new TimeOnly(9, 0),
                WeatherType = WeatherType.Sunny,
                WeatherTypeDescription = "Sereno",
                TemperatureC = temperatureC,
                HumidityPerc = 50,
                PressionMbar = 1015,
                WindKmh = 5,
                WindDirection = "N"
            }
        ]
    };

    // 2026-05-14 22:30 UTC = 2026-05-15 00:30 in Italy (CEST): the Italian civil date is already
    // May 15, while a UTC-based "today" would still say May 14.
    private static readonly DateTimeOffset JustPastItalianMidnight =
        new(2026, 5, 14, 22, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly ItalianToday = new(2026, 5, 15);

    [Fact]
    public async Task Forecast_MatchingIfNoneMatch_Returns304WithNoBody()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("bergamo", Day(new DateOnly(2026, 5, 14)));
        using var client = factory.CreateClient();

        var first = await client.GetAsync("/api/weather/bergamo/forecast/week");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);

        // Revalidate with the ETag we were given: nothing has been re-scraped, so 304 + empty body.
        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/api/weather/bergamo/forecast/week");
        conditional.Headers.IfNoneMatch.Add(etag!);
        var second = await client.SendAsync(conditional);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync());
        // The validator is echoed on the 304 so the cache can keep revalidating against it.
        Assert.Equal(etag, second.Headers.ETag);
        // Only one scrape happened across both requests — the 304 served nothing new.
        Assert.Equal(1, factory.Scraper.ScrapeCount);
    }

    [Fact]
    public async Task Forecast_CarriesCacheControlAndStrongETag()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("bergamo", Day(new DateOnly(2026, 5, 14)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/week");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        Assert.False(response.Headers.ETag!.IsWeak);
        Assert.Contains("no-cache", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Forecast_ETagRotates_WhenTheDataIsRescraped()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("bergamo", Day(new DateOnly(2026, 5, 14)));
        using var client = factory.CreateClient();

        var before = (await client.GetAsync("/api/weather/bergamo/forecast/week")).Headers.ETag;
        Assert.NotNull(before);

        // A forced re-scrape ten hours on stamps a new scrape time; the ETag is derived from it.
        factory.Clock.Advance(TimeSpan.FromHours(10));
        using (var scope = factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.SendAsync(
                new ScrapeAndCacheCommand("bergamo", FakeWeatherProviderScraper.Name));
        }

        var after = (await client.GetAsync("/api/weather/bergamo/forecast/week")).Headers.ETag;
        Assert.NotNull(after);
        Assert.NotEqual(before, after);

        // The now-stale validator no longer matches: a full 200 (not a 304) is served.
        using var stale = new HttpRequestMessage(HttpMethod.Get, "/api/weather/bergamo/forecast/week");
        stale.Headers.IfNoneMatch.Add(before!);
        var response = await client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TodayShortcut_ReturnsSamePayloadAsExplicitDate_HonouringProviderTimezone()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Clock.SetUtcNow(JustPastItalianMidnight);
        // Both the past Italian day and today are available; the shortcut must pick the Italian
        // "today" (May 15), not the UTC date (May 14).
        factory.Scraper.SetForecast(
            "bergamo",
            Day(new DateOnly(2026, 5, 14), 10),
            Day(ItalianToday, 25));
        using var client = factory.CreateClient();

        var todayResponse = await client.GetAsync("/api/weather/bergamo/forecast/today");
        var explicitResponse = await client.GetAsync($"/api/weather/bergamo/forecast/date/{ItalianToday:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, todayResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, explicitResponse.StatusCode);

        var todayBody = await todayResponse.Content.ReadAsStringAsync();
        var explicitBody = await explicitResponse.Content.ReadAsStringAsync();

        // Same payload as requesting today's date explicitly.
        Assert.Equal(explicitBody, todayBody);

        var body = JsonNode.Parse(todayBody)!;
        Assert.Equal("2026-05-15", (string?)body["day"]!["date"]);
        Assert.Equal("Europe/Rome", (string?)body["timezone"]);
    }

    [Fact]
    public async Task TodayShortcut_WhenTodayIsMissingFromTheForecast_Returns404()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Clock.SetUtcNow(JustPastItalianMidnight);
        // Only a past day is available — there is no entry for the Italian "today".
        factory.Scraper.SetForecast("bergamo", Day(new DateOnly(2026, 5, 14)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/today");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
