using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Application.Settings;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Infrastructure.Models;

namespace Weather4Agents.Infrastructure.Storage;

/// <summary>
/// Reads and writes the on-disk weather JSON files and seeds the cache from them on startup.
/// Each day's forecast is stored at <c>{OutputPath}/{location}/{yyyy-MM-dd}.json</c>. Persisting
/// runs as a step at the end of every scraping cycle (see <see cref="Jobs.WeatherScrapingJob"/>),
/// so there is a single schedule and no independent timer that could self-trigger a scrape.
/// </summary>
public sealed class WeatherFileStore
{
    private readonly WeatherFileStorageSettings _storageSettings;
    private readonly WeatherScrapingSettings _scrapingSettings;
    private readonly IWeatherProviderResolver _resolver;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WeatherFileStore> _logger;

    // Single options instance for both directions. Reads are case-insensitive so a camelCased file
    // still binds to the PascalCase properties. No enum converter is registered on purpose: one
    // with a camelCase naming policy would outrank WeatherType's own [JsonConverter] and lowercase
    // those strings, breaking the on-disk contract that agents read.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public WeatherFileStore(
        IOptions<WeatherFileStorageSettings> storageOptions,
        IOptions<WeatherScrapingSettings> scrapingOptions,
        IWeatherProviderResolver resolver,
        TimeProvider timeProvider,
        ILogger<WeatherFileStore> logger)
    {
        _storageSettings = storageOptions.Value;
        _scrapingSettings = scrapingOptions.Value;
        _resolver = resolver;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Whether file storage is switched on (<c>WeatherFileStorage:Enabled</c>).</summary>
    public bool Enabled => _storageSettings.Enabled;

    // -------------------------------------------------------------------------
    // Persist (end-of-scrape step)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes the current default-provider forecast for every configured location to disk (one
    /// file per day), then cleans up stale files. Reads from the cache the scraping cycle just
    /// populated, so no additional scrape is triggered for a location already scraped this cycle.
    /// </summary>
    public async Task PersistForecastsAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "Weather file storage step started at {Time}. Output path: {OutputPath}",
            _timeProvider.GetUtcNow(),
            _storageSettings.OutputPath);

        var scraper = _resolver.GetDefault();

        foreach (var location in _scrapingSettings.Locations)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // forceRefresh=false reuses the data the scraping cycle already cached.
                var forecast = await scraper.GetForecastAsync(location, forceRefresh: false, ct);
                await WriteForecastAsync(location, forecast, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Failed to save weather files for location '{Location}'", location);
            }
        }

        _logger.LogInformation(
            "Weather file storage step completed at {Time}", _timeProvider.GetUtcNow());

        CleanupOldFiles();
    }

    private async Task WriteForecastAsync(string location, ScrapedForecast forecast, CancellationToken ct)
    {
        if (forecast.Days.Count == 0)
        {
            _logger.LogWarning(
                "No forecast data available for {Location}. Skipping file write.", location);
            return;
        }

        var locationDir = Path.Combine(_storageSettings.OutputPath, location);
        Directory.CreateDirectory(locationDir);

        // The freshness stamp is the moment the data was scraped, not the moment this file is
        // written — a file materialised from hours-old cached data says so.
        var updatedAt = forecast.ScrapedAt;

        foreach (var day in forecast.Days)
        {
            ct.ThrowIfCancellationRequested();

            var filePath = Path.Combine(locationDir, $"{day.Date:yyyy-MM-dd}.json");

            var record = new DayWeatherFileRecord
            {
                LastUpdatedAt = updatedAt,
                Weather = day
            };

            var json = JsonSerializer.Serialize(record, JsonOptions);
            await WriteFileAtomicAsync(filePath, json, ct);

            _logger.LogDebug("Written {FilePath}", filePath);
        }

        _logger.LogInformation(
            "Saved {Count} file(s) for location '{Location}'", forecast.Days.Count, location);
    }

    // -------------------------------------------------------------------------
    // Bootstrap (startup)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Seeds the default provider's cache from JSON files already on disk so forecasts are served
    /// immediately after a restart, without waiting for a scrape. Best-effort: a missing directory
    /// or an unreadable/corrupt file is logged and skipped, never fatal. Seeded forecasts keep
    /// their original scrape timestamps; fresh scraping proceeds in the background regardless.
    /// </summary>
    public async Task BootstrapCacheAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_storageSettings.OutputPath))
        {
            _logger.LogInformation(
                "No weather-data directory at {OutputPath}; nothing to bootstrap.",
                _storageSettings.OutputPath);
            return;
        }

        var scraper = _resolver.GetDefault();
        var seeded = 0;

        foreach (var locationDir in Directory.EnumerateDirectories(_storageSettings.OutputPath))
        {
            ct.ThrowIfCancellationRequested();

            var location = Path.GetFileName(locationDir);
            var forecast = await ReadForecastFromDiskAsync(locationDir, ct);

            if (forecast.Days.Count == 0)
                continue;

            try
            {
                await scraper.SeedAsync(location, forecast, ct);
                seeded++;
                _logger.LogInformation(
                    "Seeded cache for '{Location}' from {Count} file(s) on disk (scraped {ScrapedAt}).",
                    location, forecast.Days.Count, forecast.ScrapedAt);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to seed cache for '{Location}' from disk.", location);
            }
        }

        _logger.LogInformation(
            "Cache bootstrap complete: {Count} location(s) seeded from disk.", seeded);
    }

    private async Task<ScrapedForecast> ReadForecastFromDiskAsync(string locationDir, CancellationToken ct)
    {
        var days = new List<DayWeather>();
        var scrapedAt = default(DateTimeOffset);

        foreach (var filePath in Directory.EnumerateFiles(locationDir, "*.json"))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await using var stream = File.OpenRead(filePath);
                var record = await JsonSerializer.DeserializeAsync<DayWeatherFileRecord>(
                    stream, JsonOptions, ct);

                if (record?.Weather is null)
                {
                    _logger.LogWarning(
                        "Skipping weather file {FilePath}: no weather payload.", filePath);
                    continue;
                }

                days.Add(record.Weather);

                // The forecast's scrape time is the most recent stamp across its day files.
                if (record.LastUpdatedAt > scrapedAt)
                    scrapedAt = record.LastUpdatedAt;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex,
                    "Skipping corrupt or unreadable weather file {FilePath}.", filePath);
            }
        }

        return new ScrapedForecast
        {
            ScrapedAt = scrapedAt,
            Days = [.. days.OrderBy(d => d.Date)]
        };
    }

    // -------------------------------------------------------------------------
    // File helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes <paramref name="json"/> to <paramref name="filePath"/> atomically: the content is
    /// first written to a temporary file in the same directory and then renamed over the
    /// destination. Because rename is atomic on the same filesystem, a concurrent reader (e.g. an
    /// agent consuming the JSON) always observes either the previous complete file or the new
    /// complete file — never a truncated document.
    /// </summary>
    private static async Task WriteFileAtomicAsync(string filePath, string json, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        // Leading dot + .tmp keeps the temp file out of the "*.json" cleanup glob and makes it
        // easy to spot; the GUID avoids collisions between overlapping writes.
        var tmpPath = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(tmpPath, json, ct);
            File.Move(tmpPath, filePath, overwrite: true);
        }
        catch
        {
            TryDeleteTempFile(tmpPath);
            throw;
        }
    }

    private static void TryDeleteTempFile(string tmpPath)
    {
        try
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
        }
        catch
        {
            // Best-effort cleanup of the temp file; the next cycle overwrites it anyway.
        }
    }

    /// <summary>
    /// Deletes JSON files whose date (encoded in the filename as <c>yyyy-MM-dd</c>) is strictly
    /// older than one day. Runs only when <see cref="WeatherFileStorageSettings.CleanupEnabled"/>
    /// is <c>true</c>.
    /// </summary>
    private void CleanupOldFiles()
    {
        if (!_storageSettings.CleanupEnabled)
            return;

        if (!Directory.Exists(_storageSettings.OutputPath))
            return;

        // Files dated before this cutoff are deleted (strictly more than 1 day old). Filenames
        // carry provider-local dates while the cutoff is UTC-based; the one-day slack means a
        // UTC/provider timezone mismatch around midnight only delays a deletion, never loses a
        // current file.
        var cutoff = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(-1);

        _logger.LogInformation(
            "Cleaning up weather files older than {CutoffDate} (UTC).", cutoff);

        foreach (var locationDir in Directory.EnumerateDirectories(_storageSettings.OutputPath))
        {
            foreach (var filePath in Directory.EnumerateFiles(locationDir, "*.json"))
            {
                var stem = Path.GetFileNameWithoutExtension(filePath);

                if (!DateOnly.TryParseExact(stem, "yyyy-MM-dd", out var fileDate) || fileDate >= cutoff)
                    continue;

                try
                {
                    File.Delete(filePath);
                    _logger.LogDebug("Deleted old weather file {FilePath}.", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old weather file {FilePath}.", filePath);
                }
            }
        }
    }
}
