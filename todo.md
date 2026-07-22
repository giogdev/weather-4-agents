# Ticket 15 — Background jobs coordination & cache bootstrap (plan items L3, L4)

## Goal
- **Option A (per spec):** file storage becomes a *step at the end of each scraping cycle* —
  one job, one interval, no startup race, no self-triggered scrape.
- **Cache bootstrap:** on startup, seed the cache from JSON files already on disk so agents get
  data immediately after a redeploy while fresh scraping proceeds in the background.
- Bootstrap tolerates missing/corrupt files silently (logged, not fatal).

## Design
1. **New `WeatherFileStore` service** (`Infrastructure/Storage/`) — owns all file IO + cache
   seeding. Depends only on the two settings, `IWeatherProviderResolver`, `TimeProvider`, logger.
   - `Enabled` — mirrors `WeatherFileStorageSettings.Enabled`.
   - `PersistForecastsAsync(ct)` — for each configured location, read the default provider's
     forecast (from cache, freshly scraped) via the resolver and write one JSON file per day
     (atomic write, freshness = `ScrapedAt`), then run cleanup. Moved from the old job.
   - `BootstrapCacheAsync(ct)` — enumerate `{OutputPath}/{location}` dirs, read `*.json` into a
     `ScrapedForecast` (skipping corrupt/unreadable files), seed the default scraper's cache.
2. **`IWeatherProviderScraper.SeedAsync(location, forecast, ct)`** — primes the cache from an
   external source (disk) using the scraper's own key, keeping the key format encapsulated.
   Empty forecasts are never seeded (that is the negative-cache case). Implemented in
   `BaseWeatherScraper`; cache-key building extracted into a private `CacheKeyFor` helper.
3. **`WeatherScrapingJob`** — the single coordinator:
   - Before the loop: best-effort `BootstrapCacheAsync` (only when storage enabled).
   - Each cycle: scrape all (location, provider), then — if storage enabled — `PersistForecastsAsync`.
4. **Remove** the separate `WeatherFileStorageJob` hosted service and the now-dead
   `WeatherFileStorageSettings.JobIntervalMinutes` (one schedule = the scraping interval).
   Clean up `appsettings*.json` and the settings-validation test cases that named it.
5. **DI**: drop `AddHostedService<WeatherFileStorageJob>()`, add `AddTransient<WeatherFileStore>()`.

## Tests
- Rework `WeatherFileStorageJobTests` → `WeatherFileStoreTests`: keep write / stamp / overwrite /
  cleanup coverage against the store, driving forecasts through a fake default scraper.
- Add bootstrap tests: happy path (files on disk → cache seeded, served with original scrape
  time) and corrupt-file scenario (bad file skipped, good files still seeded, no throw).
- Full suite green at the end. Nothing committed until reviewed.

## Checklist
- [ ] `WeatherFileStore` service (persist + bootstrap)
- [ ] `SeedAsync` on scraper + `CacheKeyFor` helper
- [ ] `WeatherScrapingJob` coordinates bootstrap + persist; separate job removed
- [ ] Dead `JobIntervalMinutes` removed; appsettings + settings tests cleaned
- [ ] Store tests reworked + bootstrap happy-path/corrupt-file tests
- [ ] Full suite green
- [ ] Code review
- [ ] Commit
