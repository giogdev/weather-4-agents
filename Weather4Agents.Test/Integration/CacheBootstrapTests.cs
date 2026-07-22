using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;
using Weather4Agents.Infrastructure.Models;
using Weather4Agents.Infrastructure.Storage;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// End-to-end proof of the ticket-15 cache bootstrap: with JSON files on disk, a freshly started
/// service seeds the cache from them and serves forecasts immediately — reporting the original
/// scrape time and without ever hitting the provider. Corrupt files on disk do not break startup.
/// </summary>
public sealed class CacheBootstrapTests : IDisposable
{
    private readonly string _outputPath;

    public CacheBootstrapTests()
    {
        _outputPath = Path.Combine(
            Path.GetTempPath(), "w4a-bootstrap-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_outputPath))
                Directory.Delete(_outputPath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of the temp directory.
        }
    }

    [Fact]
    public async Task Startup_WithFilesOnDisk_ServesFromCacheWithoutScraping()
    {
        // Data scraped six hours before this "restart".
        var scrapedAt = Weather4AgentsApiFactory.InitialTime.AddHours(-6);
        WriteFileOnDisk("bergamo", scrapedAt, Day(new DateOnly(2026, 5, 14)));

        await using var factory = CreateFactory();

        // Simulate the scraping job's startup step: seed the shared cache from disk.
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<WeatherFileStore>();
            await store.BootstrapCacheAsync(CancellationToken.None);
        }

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/weather/bergamo/forecast/week");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        // Served straight from the seeded cache: the original scrape time, and no scrape happened.
        Assert.Equal(scrapedAt, (DateTimeOffset)body["lastUpdatedAt"]!);
        Assert.Equal(0, Scraper(factory).ScrapeCount);
    }

    [Fact]
    public async Task Startup_WithCorruptFileOnDisk_DoesNotThrowAndSeedsTheValidLocation()
    {
        var scrapedAt = Weather4AgentsApiFactory.InitialTime.AddHours(-6);
        WriteFileOnDisk("bergamo", scrapedAt, Day(new DateOnly(2026, 5, 14)));

        // A malformed file for another location must not derail the whole bootstrap.
        var brokenDir = Path.Combine(_outputPath, "brokentown");
        Directory.CreateDirectory(brokenDir);
        await File.WriteAllTextAsync(Path.Combine(brokenDir, "2026-05-14.json"), "{ broken");

        await using var factory = CreateFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<WeatherFileStore>();
            await store.BootstrapCacheAsync(CancellationToken.None);
        }

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/weather/bergamo/forecast/week");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, Scraper(factory).ScrapeCount);
    }

    private static FakeWeatherProviderScraper Scraper(WebApplicationFactory<Program> factory)
        => factory.Services.GetRequiredService<FakeWeatherProviderScraper>();

    private WebApplicationFactory<Program> CreateFactory() =>
        new Weather4AgentsApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("WeatherFileStorage:Enabled", "true");
            builder.UseSetting("WeatherFileStorage:OutputPath", _outputPath);
        });

    private void WriteFileOnDisk(string location, DateTimeOffset scrapedAt, DayWeather day)
    {
        var locationDir = Path.Combine(_outputPath, location);
        Directory.CreateDirectory(locationDir);

        var record = new DayWeatherFileRecord { LastUpdatedAt = scrapedAt, Weather = day };
        var json = JsonSerializer.Serialize(record, JsonWriteOptions);
        File.WriteAllText(Path.Combine(locationDir, $"{day.Date:yyyy-MM-dd}.json"), json);
    }

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

    // Mirrors the on-disk contract (camelCase) so bootstrap reads what a real cycle wrote.
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
