using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.Settings;
using Weather4Agents.Application.UseCases.GetWeatherForecast;
using Weather4Agents.Infrastructure.Models;

namespace Weather4Agents.Infrastructure.Jobs;

/// <summary>
/// Background service that periodically saves weather forecast data to the file system.
/// Each day's forecast is written to <c>{OutputPath}/{location}/{yyyy-MM-dd}.json</c>.
/// Files are overwritten on every cycle so they always reflect the latest data.
/// </summary>
public class WeatherFileStorageJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WeatherFileStorageSettings _storageSettings;
    private readonly WeatherScrapingSettings _scrapingSettings;
    private readonly ILogger<WeatherFileStorageJob> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public WeatherFileStorageJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WeatherFileStorageSettings> storageOptions,
        IOptions<WeatherScrapingSettings> scrapingOptions,
        ILogger<WeatherFileStorageJob> logger)
    {
        _scopeFactory = scopeFactory;
        _storageSettings = storageOptions.Value;
        _scrapingSettings = scrapingOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_storageSettings.Enabled)
        {
            _logger.LogInformation("WeatherFileStorageJob is disabled. Set WeatherFileStorage:Enabled=true to activate.");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            await RunStorageCycleAsync(ct);

            await Task.Delay(
                TimeSpan.FromMinutes(_storageSettings.JobIntervalMinutes), ct);
        }
    }

    private async Task RunStorageCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "Weather file storage cycle started at {Time}. Output path: {OutputPath}",
            DateTimeOffset.UtcNow,
            _storageSettings.OutputPath);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        foreach (var location in _scrapingSettings.Locations)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Use default provider; forceRefresh=false reuses cached data when available.
                var forecast = await dispatcher.SendAsync(
                    new GetWeatherForecastQuery(location, ProviderName: null), ct);

                var days = forecast.ToList();

                if (days.Count == 0)
                {
                    _logger.LogWarning(
                        "No forecast data returned for {Location}. Skipping file write.", location);
                    continue;
                }

                var locationDir = Path.Combine(_storageSettings.OutputPath, location);
                Directory.CreateDirectory(locationDir);

                var updatedAt = DateTimeOffset.UtcNow;

                foreach (var day in days)
                {
                    ct.ThrowIfCancellationRequested();

                    var filePath = Path.Combine(locationDir, $"{day.Date:yyyy-MM-dd}.json");

                    var record = new DayWeatherFileRecord
                    {
                        LastUpdatedAt = updatedAt,
                        Weather = day
                    };

                    var json = JsonSerializer.Serialize(record, JsonOptions);
                    await File.WriteAllTextAsync(filePath, json, ct);

                    _logger.LogDebug("Written {FilePath}", filePath);
                }

                _logger.LogInformation(
                    "Saved {Count} file(s) for location '{Location}'", days.Count, location);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Failed to save weather files for location '{Location}'", location);
            }
        }

        _logger.LogInformation(
            "Weather file storage cycle completed at {Time}", DateTimeOffset.UtcNow);

        CleanupOldFiles();
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

        // Files dated before this cutoff are deleted (strictly more than 1 day old).
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

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
