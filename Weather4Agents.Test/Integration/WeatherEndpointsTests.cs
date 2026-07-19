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
        Provider = new WeatherProvider(FakeWeatherProviderScraper.Name),
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
        var days = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        Assert.Equal(2, days.Count);
        Assert.Equal("2026-05-14", (string?)days[0]!["date"]);
        Assert.Equal(FakeWeatherProviderScraper.Name, (string?)days[0]!["provider"]!["providerName"]);
        Assert.Equal(85, (int?)days[0]!["reliabilityPerc"]);
        Assert.Equal(18.5, (double?)days[0]!["hoursDetails"]![0]!["temperatureC"]);
        Assert.Equal("2026-05-15", (string?)days[1]!["date"]);
    }

    [Fact]
    public async Task GetForecastByDays_WhenScrapeIsEmpty_ReturnsEmptyJsonArray()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/nowhere/forecast/days/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        Assert.Empty(days);
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
    public async Task GetForecastByDays_WithUnknownProvider_Returns404Problem()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/days/3?provider=nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Contains("nope", (string?)problem["detail"]);
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
