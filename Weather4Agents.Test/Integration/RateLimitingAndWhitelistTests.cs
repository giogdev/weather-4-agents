using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// Ticket 12 — the weather endpoints are protected by a per-IP fixed-window rate limiter
/// (exceeding the limit → <c>429</c>) and by an opt-in location whitelist that rejects
/// non-configured locations without scraping while leaving the default (open) behaviour
/// unchanged.
/// </summary>
public class RateLimitingAndWhitelistTests
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

    private static FakeWeatherProviderScraper ScraperOf(WebApplicationFactory<Program> factory)
        => factory.Services.GetRequiredService<FakeWeatherProviderScraper>();

    // ── Rate limiting ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExceedingTheRequestLimit_Returns429()
    {
        await using var baseFactory = new Weather4AgentsApiFactory();
        var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Enabled", "true");
            b.UseSetting("RateLimiting:PermitLimit", "1");
            b.UseSetting("RateLimiting:WindowSeconds", "60");
        });
        ScraperOf(factory).SetForecast("bergamo", Day(PinnedToday));
        using var client = factory.CreateClient();

        var first = await client.GetAsync("/api/weather/bergamo/forecast/days/1");
        var second = await client.GetAsync("/api/weather/bergamo/forecast/days/1");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task WithinTheRequestLimit_RequestsSucceed()
    {
        await using var baseFactory = new Weather4AgentsApiFactory();
        var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Enabled", "true");
            b.UseSetting("RateLimiting:PermitLimit", "5");
            b.UseSetting("RateLimiting:WindowSeconds", "60");
        });
        ScraperOf(factory).SetForecast("bergamo", Day(PinnedToday));
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/api/weather/bergamo/forecast/days/1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // ── Whitelist enabled ───────────────────────────────────────────────────────

    [Fact]
    public async Task WithWhitelistEnabled_NonConfiguredLocation_IsRejectedWithoutScraping()
    {
        await using var baseFactory = new Weather4AgentsApiFactory();
        var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("WeatherScraping:AllowUnconfiguredLocations", "false");
            b.UseSetting("WeatherScraping:Locations:0", "Bergamo");
        });
        var scraper = ScraperOf(factory);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/milano/forecast/days/3");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, scraper.ScrapeCount);
    }

    [Fact]
    public async Task WithWhitelistEnabled_ConfiguredLocation_IsServed()
    {
        await using var baseFactory = new Weather4AgentsApiFactory();
        var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("WeatherScraping:AllowUnconfiguredLocations", "false");
            b.UseSetting("WeatherScraping:Locations:0", "Bergamo");
        });
        ScraperOf(factory).SetForecast("bergamo", Day(PinnedToday));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/days/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("san-pellegrino-terme")]
    [InlineData("San%20Pellegrino%20Terme")]
    public async Task WithWhitelistEnabled_MatchesOnNormalizedLocation(string requestedLocation)
    {
        await using var baseFactory = new Weather4AgentsApiFactory();
        var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("WeatherScraping:AllowUnconfiguredLocations", "false");
            // Whitelist configured with the spaced spelling; both hyphenated and spaced
            // request spellings must match after normalization.
            b.UseSetting("WeatherScraping:Locations:0", "San Pellegrino Terme");
        });
        ScraperOf(factory).SetForecast("San Pellegrino Terme", Day(PinnedToday));
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/weather/{requestedLocation}/forecast/days/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Whitelist disabled (default) ─────────────────────────────────────────────

    [Fact]
    public async Task WithWhitelistDisabledByDefault_UnconfiguredLocation_IsStillServed()
    {
        // No AllowUnconfiguredLocations override → default open → the whitelist is not enforced
        // and a location outside WeatherScraping:Locations is scraped and served as before.
        await using var factory = new Weather4AgentsApiFactory();
        factory.Scraper.SetForecast("milano", Day(PinnedToday));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/milano/forecast/days/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
