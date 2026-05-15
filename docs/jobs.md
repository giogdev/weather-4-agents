# Background jobs

Both jobs are `BackgroundService` implementations registered via `AddHostedService` in `Weather4Agents.Infrastructure/DependencyInjection.cs`.

---

## WeatherScrapingJob

**File:** `Weather4Agents.Infrastructure/Jobs/WeatherScrapingJob.cs`

Scrapes weather data from the configured providers and stores it in the HybridCache.

**Loop:** runs immediately at startup, then repeats every `WeatherScraping:JobIntervalMinutes` minutes (default: 60).

**Config section:** `WeatherScraping`

| Key | Default | Description |
|-----|---------|-------------|
| `EnabledProviders` | — | List of provider names to scrape |
| `Locations` | — | List of locations |
| `JobIntervalMinutes` | 60 | Minutes between cycles |

---

## WeatherFileStorageJob

Reads cached forecasts and persists them to the file system as JSON files, one file per day per location.

**Loop:** runs immediately at startup (if enabled), then repeats every `WeatherFileStorage:JobIntervalMinutes` minutes (default: 60).

**Config section:** `WeatherFileStorage`

| Key | Default | Description |
|-----|---------|-------------|
| `Enabled` | `false` | Master switch — job exits immediately if false |
| `OutputPath` | `weather-data` | Root directory for JSON files |
| `JobIntervalMinutes` | 60 | Minutes between cycles |
| `CleanupEnabled` | `false` | Delete files older than 1 day |

**Output structure:**
```
{OutputPath}/
  {location}/
    2025-06-01.json
    2025-06-02.json
    ...
```
