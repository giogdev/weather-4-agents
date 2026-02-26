# Jobs and automation
This page describes the scheduled jobs.

## _WeatherScrapingJob_: automatic scraping
**Can be disabled**: ❌

`WeatherScrapingJob` is an automatic job that runs at a configurable interval and updates the weather data cache. This way, all application features read pre-processed
data instead of fetching it from the web. This improve performance.

## _StorageJob_: saving forecast to file system
**Can be disabled**: ✅

`WeatherFileStorageJob` is an optional background service that periodically saves weather data as JSON files to the file system. It's designed for Docker environments where agents can read files directly, without having to call the API.

**📁 Directory structure:**
```
{OutputPath}/
└── {location}/
    ├── 2026-02-26.json
    ├── 2026-02-27.json
    └── ...
```
**📄File format:**

```json
{
  "lastUpdatedAt": "2026-02-26T09:00:00+00:00",
  "weather": {
    "date": "2026-02-26",
    "provider": { "providerName": "3bMeteo" },
    "hoursDetails": [
      {
        "timeFrom": "00:00:00",
        "timeTo": "01:00:00",
        "weatherType": "partlyCloudy",
        "weatherTypeDescription": "poco nuvoloso",
        "temperatureC": 8.5,
        "precipitationsPerc": 0,
        "humidityPerc": 66,
        "pressionMbar": 1012,
        "windKmh": 5.0,
        "windDirection": "NE"
      }
    ]
  }
}
```
The job overwrites existing files on each cycle. It uses the default provider and reads locations from the `WeatherScraping:Locations` section.


## StorageJob: automatic cleanup of old files
**Can be disabled**: ✅

When `CleanupEnabled` is `true`, at the end of each cycle the job iterates through all subdirectories of `OutputPath` and deletes `.json` files whose name (formatted as `yyyy-MM-dd`) is **older than yesterday**. 

Files from today and yesterday are always retained.
Deletion errors are logged as `Warning` without interrupting the cycle.
> ⚠️ Files that don't follow the `yyyy-MM-dd` naming format are ignored.

