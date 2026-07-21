using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.Settings;
using Weather4Agents.Application.UseCases.GetWeatherForecast;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Infrastructure.Jobs;

namespace Weather4Agents.Test.Jobs;

/// <summary>
/// Drives <see cref="WeatherFileStorageJob"/> end-to-end for a single cycle against a real
/// temporary directory, with a fake dispatcher supplying forecasts. Covers the file-storage
/// behaviour required by ticket 05: write, overwrite, and cleanup cutoff — plus the guarantee
/// that no partial/temporary file is left behind by the atomic write.
/// </summary>
public sealed class WeatherFileStorageJobTests : IDisposable
{
    private const string Location = "Bergamo";

    private readonly string _outputPath;

    public WeatherFileStorageJobTests()
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
    // Write
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Cycle_WritesOneFilePerDay_WithRoundTrippableContent()
    {
        var date = new DateOnly(2026, 7, 20);
        var dispatcher = new FakeDispatcher();
        dispatcher.SetForecast(Location, Day(date, temperature: 21.5));

        var job = CreateJob(dispatcher);

        await job.RunStorageCycleAsync(CancellationToken.None);

        var filePath = Path.Combine(_outputPath, Location, "2026-07-20.json");
        Assert.True(File.Exists(filePath));

        var record = ReadRecord(filePath);
        Assert.Equal(date, record.Weather.Date);
        Assert.Equal(21.5, record.Weather.HoursDetails.Single().TemperatureC);

        // The atomic write must not leave temporary artefacts behind.
        Assert.Empty(TempFilesIn(Path.Combine(_outputPath, Location)));
    }

    // -------------------------------------------------------------------------
    // Overwrite
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Cycle_RunTwice_OverwritesFileWithLatestData()
    {
        var date = new DateOnly(2026, 7, 20);
        var dispatcher = new FakeDispatcher();
        var job = CreateJob(dispatcher);

        dispatcher.SetForecast(Location, Day(date, temperature: 10.0));
        await job.RunStorageCycleAsync(CancellationToken.None);

        dispatcher.SetForecast(Location, Day(date, temperature: 30.0));
        await job.RunStorageCycleAsync(CancellationToken.None);

        var locationDir = Path.Combine(_outputPath, Location);
        var jsonFiles = Directory.GetFiles(locationDir, "*.json");
        Assert.Single(jsonFiles);

        var record = ReadRecord(jsonFiles[0]);
        Assert.Equal(30.0, record.Weather.HoursDetails.Single().TemperatureC);
        Assert.Empty(TempFilesIn(locationDir));
    }

    // -------------------------------------------------------------------------
    // Cleanup cutoff
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Cycle_WithCleanupEnabled_DeletesOnlyFilesOlderThanCutoff()
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

        var dispatcher = new FakeDispatcher();
        dispatcher.SetForecast(Location, Day(today, temperature: 15.0));

        var job = CreateJob(dispatcher, cleanupEnabled: true);

        await job.RunStorageCycleAsync(CancellationToken.None);

        // Today's fresh file was written and kept (today >= cutoff).
        Assert.True(File.Exists(Path.Combine(locationDir, $"{today:yyyy-MM-dd}.json")));
        // The stale file is gone; the non-date file survives.
        Assert.False(File.Exists(staleFile));
        Assert.True(File.Exists(notesFile));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private WeatherFileStorageJob CreateJob(FakeDispatcher dispatcher, bool cleanupEnabled = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDispatcher>(dispatcher);
        var scopeFactory = services.BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        var storageSettings = new WeatherFileStorageSettings
        {
            Enabled = true,
            OutputPath = _outputPath,
            JobIntervalMinutes = 60,
            CleanupEnabled = cleanupEnabled
        };

        var scrapingSettings = new WeatherScrapingSettings
        {
            DefaultProvider = "Fake",
            EnabledProviders = ["Fake"],
            Locations = [Location]
        };

        return new WeatherFileStorageJob(
            scopeFactory,
            Options.Create(storageSettings),
            Options.Create(scrapingSettings),
            TimeProvider.System,
            NullLogger<WeatherFileStorageJob>.Instance);
    }

    private static DayWeather Day(DateOnly date, double temperature) => new()
    {
        Date = date,
        Provider = new WeatherProvider("Fake"),
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

/// <summary>Minimal <see cref="IDispatcher"/> returning per-location forecasts for tests.</summary>
internal sealed class FakeDispatcher : IDispatcher
{
    private readonly Dictionary<string, IEnumerable<DayWeather>> _forecasts =
        new(StringComparer.OrdinalIgnoreCase);

    public void SetForecast(string location, params DayWeather[] days)
        => _forecasts[location] = days;

    public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        if (query is GetWeatherForecastQuery forecastQuery)
        {
            IEnumerable<DayWeather> days = _forecasts.TryGetValue(forecastQuery.Location, out var d)
                ? d
                : [];
            return Task.FromResult((TResult)(object)days);
        }

        throw new NotSupportedException($"Unexpected query type {query.GetType().Name}.");
    }

    public Task SendAsync(ICommand command, CancellationToken ct = default)
        => throw new NotSupportedException();
}
