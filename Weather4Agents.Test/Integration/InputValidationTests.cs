using System.Net;
using System.Text.Json.Nodes;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// Input validation and location normalization at the HTTP boundary (ticket 09):
/// out-of-range days and malformed locations return <c>400</c> ProblemDetails, while
/// different spellings of the same location converge on a single scrape and cache entry.
/// </summary>
public class InputValidationTests
{
    private static readonly DateOnly PinnedToday =
        DateOnly.FromDateTime(Weather4AgentsApiFactory.InitialTime.UtcDateTime);

    private static DayWeather Day(DateOnly date) => new()
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
                TemperatureC = 18.5,
                PrecipitationMm = 0,
                HumidityPerc = 50,
                PressionMbar = 1015,
                WindKmh = 5,
                WindDirection = "N"
            }
        ]
    };

    // ── Days range ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9)]
    public async Task GetForecastByDays_WithDaysOutOfRange_Returns400NamingTheValidRange(int days)
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/weather/bergamo/forecast/days/{days}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonNode.Parse(body)!;
        Assert.Equal(400, (int?)problem["status"]);
        // The ProblemDetails must name the valid range so agents can self-correct.
        Assert.Contains("between 1 and 8", body);
    }

    [Fact]
    public async Task GetForecastByDays_WithDaysOutOfRange_DoesNotScrape()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        await client.GetAsync("/api/weather/bergamo/forecast/days/0");

        Assert.Equal(0, factory.Scraper.ScrapeCount);
    }

    // ── Location shape ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("berg4mo")]      // digits
    [InlineData("milano%21")]    // "milano!"
    [InlineData("rome%3Bdrop")]  // "rome;drop"
    public async Task Endpoints_WithDisallowedLocationCharacters_Return400Problem(string encodedLocation)
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/weather/{encodedLocation}/forecast/week");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(400, (int?)problem["status"]);
    }

    [Fact]
    public async Task Endpoints_WithExcessivelyLongLocation_Return400Problem()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var overlong = new string('a', 101);
        var response = await client.GetAsync($"/api/weather/{overlong}/forecast/days/3");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AllWeatherEndpoints_ValidateTheLocation()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        string[] urls =
        [
            "/api/weather/berg4mo/forecast/days/3",
            "/api/weather/berg4mo/forecast/week",
            "/api/weather/berg4mo/forecast/next-24h",
            "/api/weather/berg4mo/forecast/date/2026-05-14",
        ];

        foreach (var url in urls)
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    // ── Normalization: one location, one scrape, one cache entry ─────────────

    [Fact]
    public async Task SpacedAndHyphenatedSpellings_ProduceOneScrapeAndOneCacheEntry()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("San Pellegrino Terme", Day(PinnedToday));
        using var client = factory.CreateClient();

        var spaced = await client.GetAsync("/api/weather/San%20Pellegrino%20Terme/forecast/days/1");
        var hyphenated = await client.GetAsync("/api/weather/san-pellegrino-terme/forecast/days/1");

        Assert.Equal(HttpStatusCode.OK, spaced.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hyphenated.StatusCode);
        // The second spelling is a cache hit: the provider is scraped exactly once.
        Assert.Equal(1, factory.Scraper.ScrapeCount);
    }

    // ── OpenAPI ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenApiDocument_Lists400OnTheWeatherEndpoints()
    {
        await using var factory = new Weather4AgentsApiFactory();
        using var client = factory.CreateClient();

        var doc = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        var paths = doc["paths"]!.AsObject();

        string[] expected =
        [
            "/api/weather/{location}/forecast/days/{numberOfDays}",
            "/api/weather/{location}/forecast/week",
            "/api/weather/{location}/forecast/next-24h",
            "/api/weather/{location}/forecast/date/{date}",
        ];

        foreach (var path in expected)
        {
            var responses = paths[path]?["get"]?["responses"]?.AsObject();
            Assert.NotNull(responses);
            Assert.True(responses.ContainsKey("400"), $"{path} does not document 400");
        }
    }
}
