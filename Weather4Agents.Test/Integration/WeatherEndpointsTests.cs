using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// HTTP-level smoke tests: boot the API through <see cref="Weather4AgentsApiFactory"/>,
/// call the weather endpoints over (in-memory) HTTP and assert status + JSON payload.
/// </summary>
public class WeatherEndpointsTests
{
    private static readonly DateOnly PinnedToday =
        DateOnly.FromDateTime(Weather4AgentsApiFactory.InitialTime.UtcDateTime);

    private static DayWeather Day(DateOnly date, double temperatureC) => new()
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
                PrecipitationMm = 0,
                HumidityPerc = 50,
                PressionMbar = 1015,
                WindKmh = 5,
                WindDirection = "N"
            }
        ]
    };

    [Fact]
    public async Task GetForecastByDays_WithConfiguredFakeScraper_ReturnsRequestedDaysAsJson()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast(
            "bergamo",
            Day(PinnedToday, 18.5),
            Day(PinnedToday.AddDays(1), 21.0),
            Day(PinnedToday.AddDays(2), 12.0));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/days/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        // The endpoint now returns the same envelope as week/next-24h: no bare domain array,
        // a freshness stamp, the provider timezone, and the day entries under "forecast".
        Assert.Equal(Weather4AgentsApiFactory.InitialTime, (DateTimeOffset?)body["lastUpdatedAt"]);
        Assert.Equal("Europe/Rome", (string?)body["timezone"]);
        var days = body["forecast"]!.AsArray();
        Assert.Equal(2, days.Count);
        Assert.Equal("2026-05-14", (string?)days[0]!["date"]);
        Assert.Equal(85, (int?)days[0]!["reliabilityPerc"]);
        Assert.Equal(18.5, (double?)days[0]!["hoursDetails"]![0]!["temperatureC"]);
        // The weather type keeps its historical string form (enum serialized as its name).
        Assert.Equal("Sunny", (string?)days[0]!["hoursDetails"]![0]!["weatherType"]);
        Assert.Equal("2026-05-15", (string?)days[1]!["date"]);
    }

    [Fact]
    public async Task GetForecastByDays_DoesNotShrinkTheCachedForecastForLaterCallers()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast(
            "bergamo",
            Day(PinnedToday, 18.5),
            Day(PinnedToday.AddDays(1), 21.0),
            Day(PinnedToday.AddDays(2), 12.0));
        using var client = factory.CreateClient();

        // A trimmed request must not mutate the shared cached forecast: a later full request
        // still sees every day (the single scrape feeds both).
        var trimmed = await client.GetAsync("/api/weather/bergamo/forecast/days/1");
        Assert.Single(JsonNode.Parse(await trimmed.Content.ReadAsStringAsync())!["forecast"]!.AsArray());

        var full = await client.GetAsync("/api/weather/bergamo/forecast/days/3");
        Assert.Equal(3, JsonNode.Parse(await full.Content.ReadAsStringAsync())!["forecast"]!.AsArray().Count);
        Assert.Equal(1, factory.Scraper.ScrapeCount);
    }

    [Fact]
    public async Task GetForecastByDays_WhenScrapeIsEmpty_Returns404Problem()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/nowhere/forecast/days/3");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(404, (int?)problem["status"]);
        Assert.Equal("Location not found", (string?)problem["title"]);
        Assert.Contains("nowhere", (string?)problem["detail"]);
    }

    [Fact]
    public async Task GetWeekForecast_WhenScrapeIsEmpty_Returns404Problem()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/nowhere/forecast/week");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNext24Hours_WhenScrapeIsEmpty_Returns404Problem()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/nowhere/forecast/next-24h");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDayWeather_WhenDateIsMissing_Returns404Problem()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("bergamo", Day(PinnedToday, 18.5));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/date/2026-06-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDayWeather_WhenDateExists_ReturnsDayEnvelope()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("bergamo", Day(PinnedToday, 18.5));
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/weather/bergamo/forecast/date/{PinnedToday:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        // Same envelope as the other endpoints: no bare domain entity, freshness + timezone,
        // and the day under "day".
        Assert.Equal(Weather4AgentsApiFactory.InitialTime, (DateTimeOffset?)body["lastUpdatedAt"]);
        Assert.Equal("Europe/Rome", (string?)body["timezone"]);
        var day = body["day"]!;
        Assert.Equal("2026-05-14", (string?)day["date"]);
        Assert.Equal(85, (int?)day["reliabilityPerc"]);
        Assert.Equal(18.5, (double?)day["hoursDetails"]![0]!["temperatureC"]);
        Assert.Equal("Sunny", (string?)day["hoursDetails"]![0]!["weatherType"]);
    }

    [Fact]
    public async Task GetForecastByDays_WithUnknownProvider_Returns400ProblemListingProviders()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/days/3?provider=nope");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Contains("nope", (string?)problem["detail"]);
        // The available providers are listed both in the human-readable detail and as a
        // machine-readable extension so agents can recover without parsing prose.
        Assert.Contains(FakeWeatherProviderScraper.Name, (string?)problem["detail"]);
        var available = problem["availableProviders"]!.AsArray()
            .Select(p => (string?)p);
        Assert.Contains(FakeWeatherProviderScraper.Name, available);
    }

    [Fact]
    public async Task GetForecastByDays_WhenScraperThrowsUnexpectedly_Returns500ProblemWithoutStackTrace()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.FailFor("bergamo");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/days/3");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonNode.Parse(body)!;
        Assert.Equal(500, (int?)problem["status"]);
        // Nothing about the internal failure must leak to the client.
        Assert.DoesNotContain("Simulated scraper failure", body);
        Assert.DoesNotContain("at Weather4Agents", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task Host_ResolvesThePinnedClock()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient(); // forces host startup

        var timeProvider = factory.Services.GetRequiredService<TimeProvider>();

        Assert.Same(factory.Clock, timeProvider);
        Assert.Equal(Weather4AgentsApiFactory.InitialTime, timeProvider.GetUtcNow());
    }
}
