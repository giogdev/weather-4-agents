using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Application.Settings;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Infrastructure.Models;
using Weather4Agents.Infrastructure.Storage;

namespace Weather4Agents.Test.Storage;

/// <summary>
/// Drives <see cref="WeatherFileStore"/> against a real temporary directory. Covers the
/// file-storage behaviour (write, stamp, overwrite, cleanup — ticket 05) now that storage is a
/// step of the scraping cycle, plus the cache bootstrap that seeds forecasts from disk on startup
/// (ticket 15), including its tolerance of missing directories and corrupt files.
/// </summary>
public sealed class WeatherFileStoreTests : IDisposable
{
    private const string Location = "Bergamo";

    private readonly string _outputPath;

    public WeatherFileStoreTests()
    {
        // A unique temp directory per test instance; removed in Dispose.
        _outputPath = Path.Combine(
            Path.GetTempPath(), "w4a-storage-tests", Guid.NewGuid().ToString("N"));
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

    // -------------------------------------------------------------------------
    // Persist — write
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Persist_WritesOneFilePerDay_WithRoundTrippableContent()
    {
        var date = new DateOnly(2026, 7, 20);
        var scraper = new RecordingScraper();
        scraper.SetForecast(Location, Forecast(DateTimeOffset.UtcNow, Day(date, temperature: 21.5)));

        var store = CreateStore(scraper);

        await store.PersistForecastsAsync(CancellationToken.None);

        var filePath = Path.Combine(_outputPath, Location, "2026-07-20.json");
        Assert.True(File.Exists(filePath));

        var record = ReadRecord(filePath);
        Assert.Equal(date, record.Weather.Date);
        Assert.Equal(21.5, record.Weather.HoursDetails.Single().TemperatureC);

        // The atomic write must not leave temporary artefacts behind.
        Assert.Empty(TempFilesIn(Path.Combine(_outputPath, Location)));
    }

    [Fact]
    public async Task Persist_StampsFileWithScrapeTime_NotTheMomentTheFileWasWritten()
    {
        var date = new DateOnly(2026, 7, 20);
        // Data scraped ten hours before this cycle runs.
        var scrapedAt = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero);
        var cycleTime = scrapedAt.AddHours(10);

        var scraper = new RecordingScraper();
        scraper.SetForecast(Location, Forecast(scrapedAt, Day(date, temperature: 18.0)));

        var store = CreateStore(scraper, clock: new FakeTimeProvider(cycleTime));

        await store.PersistForecastsAsync(CancellationToken.None);

        var record = ReadRecord(Path.Combine(_outputPath, Location, "2026-07-20.json"));
        Assert.Equal(scrapedAt, record.LastUpdatedAt);
    }

    // -------------------------------------------------------------------------
    // Persist — overwrite
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Persist_RunTwice_OverwritesFileWithLatestData()
    {
        var date = new DateOnly(2026, 7, 20);
        var scraper = new RecordingScraper();
        var store = CreateStore(scraper);

        scraper.SetForecast(Location, Forecast(DateTimeOffset.UtcNow, Day(date, temperature: 10.0)));
        await store.PersistForecastsAsync(CancellationToken.None);

        scraper.SetForecast(Location, Forecast(DateTimeOffset.UtcNow, Day(date, temperature: 30.0)));
        await store.PersistForecastsAsync(CancellationToken.None);

        var locationDir = Path.Combine(_outputPath, Location);
        var jsonFiles = Directory.GetFiles(locationDir, "*.json");
        Assert.Single(jsonFiles);

        var record = ReadRecord(jsonFiles[0]);
        Assert.Equal(30.0, record.Weather.HoursDetails.Single().TemperatureC);
        Assert.Empty(TempFilesIn(locationDir));
    }

    // -------------------------------------------------------------------------
    // Persist — cleanup cutoff
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Persist_WithCleanupEnabled_DeletesOnlyFilesOlderThanCutoff()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var locationDir = Path.Combine(_outputPath, Location);
        Directory.CreateDirectory(locationDir);

        // Strictly older than the (today - 1) cutoff → must be deleted.
        var staleFile = Path.Combine(locationDir, $"{today.AddDays(-5):yyyy-MM-dd}.json");
        await File.WriteAllTextAsync(staleFile, "{}");

        // A file whose name is not a date must never be touched.
        var notesFile = Path.Combine(locationDir, "notes.json");
        await File.WriteAllTextAsync(notesFile, "{}");

        var scraper = new RecordingScraper();
        scraper.SetForecast(Location, Forecast(DateTimeOffset.UtcNow, Day(today, temperature: 15.0)));

        var store = CreateStore(scraper, cleanupEnabled: true);

        await store.PersistForecastsAsync(CancellationToken.None);

        // Today's fresh file was written and kept (today >= cutoff).
        Assert.True(File.Exists(Path.Combine(locationDir, $"{today:yyyy-MM-dd}.json")));
        // The stale file is gone; the non-date file survives.
        Assert.False(File.Exists(staleFile));
        Assert.True(File.Exists(notesFile));
    }

    [Fact]
    public async Task Persist_WithNoDataForLocation_WritesNoFile()
    {
        // The scraper returns an empty forecast (unknown location / failed scrape).
        var store = CreateStore(new RecordingScraper());

        await store.PersistForecastsAsync(CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(_outputPath, Location)));
    }

    // -------------------------------------------------------------------------
    // Bootstrap
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Bootstrap_SeedsCacheFromFilesOnDisk_WithOriginalScrapeTime()
    {
        var scrapedAt = new DateTimeOffset(2026, 7, 19, 6, 0, 0, TimeSpan.Zero);

        // Two day files written on a previous run.
        WriteFileOnDisk(Location, scrapedAt, Day(new DateOnly(2026, 7, 20), temperature: 20.0));
        WriteFileOnDisk(Location, scrapedAt, Day(new DateOnly(2026, 7, 21), temperature: 22.0));

        var scraper = new RecordingScraper();
        var store = CreateStore(scraper);

        await store.BootstrapCacheAsync(CancellationToken.None);

        var seeded = Assert.Single(scraper.Seeded);
        Assert.Equal(Location, seeded.Location);
        Assert.Equal(scrapedAt, seeded.Forecast.ScrapedAt);
        // Both days seeded, ordered by date.
        Assert.Equal(
            [new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 21)],
            seeded.Forecast.Days.Select(d => d.Date));
    }

    [Fact]
    public async Task Bootstrap_WithCorruptFile_SkipsItAndStillSeedsTheValidDays()
    {
        var scrapedAt = new DateTimeOffset(2026, 7, 19, 6, 0, 0, TimeSpan.Zero);

        WriteFileOnDisk(Location, scrapedAt, Day(new DateOnly(2026, 7, 20), temperature: 20.0));

        // A truncated / malformed JSON file in the same directory must not derail the bootstrap.
        var corruptPath = Path.Combine(_outputPath, Location, "2026-07-21.json");
        await File.WriteAllTextAsync(corruptPath, "{ this is not valid json");

        var scraper = new RecordingScraper();
        var store = CreateStore(scraper);

        // Must not throw.
        await store.BootstrapCacheAsync(CancellationToken.None);

        var seeded = Assert.Single(scraper.Seeded);
        var day = Assert.Single(seeded.Forecast.Days);
        Assert.Equal(new DateOnly(2026, 7, 20), day.Date);
    }

    [Fact]
    public async Task Bootstrap_WithAllFilesCorrupt_SeedsNothing()
    {
        var locationDir = Path.Combine(_outputPath, Location);
        Directory.CreateDirectory(locationDir);
        await File.WriteAllTextAsync(Path.Combine(locationDir, "2026-07-20.json"), "not json");

        var scraper = new RecordingScraper();
        var store = CreateStore(scraper);

        await store.BootstrapCacheAsync(CancellationToken.None);

        Assert.Empty(scraper.Seeded);
    }

    [Fact]
    public async Task Bootstrap_WithMissingDirectory_DoesNothing()
    {
        var scraper = new RecordingScraper();
        // Point the store at a directory that does not exist.
        var store = CreateStore(scraper, outputPath: Path.Combine(_outputPath, "does-not-exist"));

        await store.BootstrapCacheAsync(CancellationToken.None);

        Assert.Empty(scraper.Seeded);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private WeatherFileStore CreateStore(
        RecordingScraper scraper,
        bool cleanupEnabled = false,
        TimeProvider? clock = null,
        string? outputPath = null)
    {
        var storageSettings = new WeatherFileStorageSettings
        {
            Enabled = true,
            OutputPath = outputPath ?? _outputPath,
            CleanupEnabled = cleanupEnabled
        };

        var scrapingSettings = new WeatherScrapingSettings
        {
            DefaultProvider = RecordingScraper.Name,
            EnabledProviders = [RecordingScraper.Name],
            Locations = [Location]
        };

        return new WeatherFileStore(
            Options.Create(storageSettings),
            Options.Create(scrapingSettings),
            new StubResolver(scraper),
            clock ?? TimeProvider.System,
            NullLogger<WeatherFileStore>.Instance);
    }

    private void WriteFileOnDisk(string location, DateTimeOffset scrapedAt, DayWeather day)
    {
        var locationDir = Path.Combine(_outputPath, location);
        Directory.CreateDirectory(locationDir);

        var record = new DayWeatherFileRecord { LastUpdatedAt = scrapedAt, Weather = day };
        var json = JsonSerializer.Serialize(record, JsonWriteOptions);
        File.WriteAllText(Path.Combine(locationDir, $"{day.Date:yyyy-MM-dd}.json"), json);
    }

    private static ScrapedForecast Forecast(DateTimeOffset scrapedAt, params DayWeather[] days)
        => new() { ScrapedAt = scrapedAt, Days = [.. days] };

    private static DayWeather Day(DateOnly date, double temperature) => new()
    {
        Date = date,
        Provider = new WeatherProvider(RecordingScraper.Name),
        HoursDetails =
        [
            new HoursWeatherDetails
            {
                TimeFrom = new TimeOnly(12, 0),
                TimeTo = new TimeOnly(13, 0),
                TemperatureC = temperature
            }
        ]
    };

    private static DayWeatherFileRecordShape ReadRecord(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<DayWeatherFileRecordShape>(stream, JsonReadOptions)!;
    }

    private static IEnumerable<string> TempFilesIn(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory).Where(f => f.EndsWith(".tmp", StringComparison.Ordinal))
            : [];

    // Mirrors the on-disk contract (camelCase) so the test writes files the store then reads.
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Reader-side shape mirroring the on-disk envelope, kept independent of the
    /// Infrastructure record type so the test asserts against the JSON contract itself.
    /// </summary>
    private sealed class DayWeatherFileRecordShape
    {
        public DateTimeOffset LastUpdatedAt { get; set; }
        public DayWeather Weather { get; set; } = null!;
    }
}

/// <summary>
/// In-memory <see cref="IWeatherProviderScraper"/> for store tests: returns preconfigured
/// forecasts and records every <see cref="SeedAsync"/> call so bootstrap can be asserted.
/// </summary>
internal sealed class RecordingScraper : IWeatherProviderScraper
{
    public const string Name = "Fake";

    private readonly Dictionary<string, ScrapedForecast> _forecasts =
        new(StringComparer.OrdinalIgnoreCase);

    public List<(string Location, ScrapedForecast Forecast)> Seeded { get; } = [];

    public string ProviderName => Name;

    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public void SetForecast(string location, ScrapedForecast forecast)
        => _forecasts[location] = forecast;

    public Task<ScrapedForecast> GetForecastAsync(
        string location, bool forceRefresh = false, CancellationToken ct = default)
        => Task.FromResult(_forecasts.TryGetValue(location, out var f) ? f : new ScrapedForecast());

    public Task SeedAsync(string location, ScrapedForecast forecast, CancellationToken ct = default)
    {
        Seeded.Add((location, forecast));
        return Task.CompletedTask;
    }
}

/// <summary>Resolver stub whose default (and only) provider is the supplied scraper.</summary>
internal sealed class StubResolver : IWeatherProviderResolver
{
    private readonly IWeatherProviderScraper _scraper;

    public StubResolver(IWeatherProviderScraper scraper) => _scraper = scraper;

    public IWeatherProviderScraper GetDefault() => _scraper;

    public IWeatherProviderScraper GetByName(string providerName) => _scraper;

    public IEnumerable<string> GetAvailableProviders() => [_scraper.ProviderName];
}
