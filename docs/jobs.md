# Background jobs

Scraping and file storage are handled by a **single** background service, `WeatherScrapingJob`, a
`BackgroundService` registered via `AddHostedService` in
`Weather4Agents.Infrastructure/DependencyInjection.cs`. File storage is a step of the scraping
cycle rather than an independent job, so there is one schedule, no startup race between two timers,
and no separate storage interval to configure.

---

## WeatherScrapingJob

**File:** `Weather4Agents.Infrastructure/Jobs/WeatherScrapingJob.cs`

**Lifecycle:**

1. **Bootstrap from disk (once, at startup).** If file storage is enabled, any JSON files left on
   disk from a previous run seed the cache, so forecasts can be served immediately after a restart
   without waiting for the first scrape. Each seeded forecast keeps its original scrape time, so
   freshness (`LastUpdatedAt`) still reflects when the data was really scraped. Failures here are
   logged and ignored — they never stop the loop from starting.
2. **Scraping cycle (immediately, then on the interval).** For every configured location × enabled
   provider, the job scrapes and stores the result in the `HybridCache`. A per-location failure is
   logged and does not abort the cycle.
3. **Persist to disk (final step of each cycle).** If file storage is enabled, the freshly cached
   forecasts are written to JSON files. Because the data is already cached, persisting reads it
   back without triggering another scrape.

The loop then waits `WeatherScraping:JobIntervalMinutes` minutes and repeats from step 2.

### Configuration

**Scraping** — section `WeatherScraping`:

| Key | Default | Description |
|-----|---------|-------------|
| `DefaultProvider` | `3bMeteo` | Provider used when a request omits one; must be in `EnabledProviders` |
| `EnabledProviders` | `[3bMeteo]` | Providers scraped on the schedule |
| `Locations` | `[Bergamo]` | Locations scraped on the schedule |
| `AllowUnconfiguredLocations` | `true` | If `false`, only `Locations` are servable via the API |
| `JobIntervalMinutes` | `60` | Minutes between scraping cycles (1–1440) |
| `HttpTimeoutSeconds` | `15` | Per-attempt HTTP timeout for provider fetches (1–60) |

**File storage** — section `WeatherFileStorage`:

| Key | Default | Description |
|-----|---------|-------------|
| `Enabled` | `false` | Master switch — when false, no bootstrap and no persistence happen |
| `OutputPath` | `weather-data` | Root directory for JSON files |
| `CleanupEnabled` | `false` | Delete JSON files whose date is more than one day in the past, each cycle |

> ℹ️ There is no `WeatherFileStorage:JobIntervalMinutes`: storage runs at the end of each scraping
> cycle, so it is governed by `WeatherScraping:JobIntervalMinutes`.

### Output structure

Files are written atomically (temporary file + rename) so a reader never sees a half-written file:

```
{OutputPath}/
  {location}/
    2025-06-01.json
    2025-06-02.json
    ...
```
