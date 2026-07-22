# Ticket 16 — Health checks & basic metrics (plan items L2, L6)

## Goal
- **/health** endpoint: liveness + a custom check verifying the last scraping cycle
  succeeded within a configurable window.
- **Docker HEALTHCHECK** in the Dockerfile hitting `/health`.
- **Basic metrics** via `System.Diagnostics.Metrics`: scrape successes/failures, scrape
  duration, and slots mapped to `Unknown`. Full OpenTelemetry is out of scope.

## Design
1. **`WeatherMetrics`** (`Infrastructure/Diagnostics/`) — singleton wrapping one `Meter`
   (`Weather4Agents`). Instruments:
   - `weather.scrape.success` (Counter<long>), `weather.scrape.failure` (Counter<long>)
   - `weather.scrape.duration` (Histogram<double>, ms)
   - `weather.mapping.unknown` (Counter<long>)
   Recording seams (truest boundaries, both already exercised by tests):
   - `BaseWeatherScraper.ScrapeStampedAsync` times the actual scrape (cache miss only) and
     records success/duration, or failure/duration on exception (rethrown). Empty results
     count as success — emptiness is the negative-cache concern, not a scrape failure.
   - `Meteo3bWeatherTypeMapper.Map` records one unknown slot when it falls through to
     `WeatherType.Unknown`.
   Duration uses the injected `TimeProvider` (`GetTimestamp`/`GetElapsedTime`) so it is
   deterministic under the fake clock.
2. **`ScrapeCycleTracker`** (`Infrastructure/Diagnostics/`) — singleton holding
   `LastSuccessfulCycleAt` (nullable). `MarkCycleSucceeded()` stamps it via `TimeProvider`.
   `WeatherScrapingJob` marks it after any cycle with ≥1 successful scrape.
3. **`HealthCheckSettings`** (`API/Settings/`, like `RateLimitingSettings`) —
   `MaxScrapeAgeMinutes` (default 120, range 1–1440), validated + `ValidateOnStart`.
   Documented to exceed `WeatherScraping:JobIntervalMinutes` so the instance is not flagged
   between normal cycles.
4. **`ScrapeFreshnessHealthCheck`** (`API/HealthChecks/`, `IHealthCheck`) reads the tracker,
   settings and `TimeProvider`:
   - no cycle yet → **Degraded** (starting up; maps to 200)
   - last success within window → **Healthy** (200)
   - older than window → **Unhealthy** (503)
5. **`Program.cs`**: `AddHealthChecks().AddCheck<ScrapeFreshnessHealthCheck>("scrape-freshness")`,
   bind+validate `HealthCheckSettings`, `MapHealthChecks("/health")`. Not rate-limited (no
   policy attached to the health endpoint).
6. **DI**: register `WeatherMetrics` + `ScrapeCycleTracker` singletons; inject metrics into the
   mapper and `BaseWeatherScraper` (Meteo3b + Fake ctors), tracker into the job.
7. **Dockerfile**: install `curl` in the base layer (as root, before `USER app`) and add a
   `HEALTHCHECK` against `http://localhost:8080/health`.
8. **appsettings.json**: add a `HealthCheck` section.

## Tests
- Integration (`HealthCheckTests`): healthy (mark cycle succeeded → 200 Healthy), unhealthy
  (mark succeeded, advance the fake clock past the window → 503 Unhealthy), and no-cycle-yet
  (→ 200 Degraded).
- Metrics unit test: mapper records an unknown slot via `MetricCollector`; scrape
  success/failure counters increment through the fake scraper.
- Full suite green at the end. Nothing committed until reviewed.

## Checklist
- [x] `WeatherMetrics` + recording in scraper & mapper
- [x] `ScrapeCycleTracker` + job marks successful cycles
- [x] `HealthCheckSettings` + `ScrapeFreshnessHealthCheck` + `/health` mapping
- [x] Dockerfile HEALTHCHECK (verified end-to-end: container reports healthy, /health → 200 Healthy)
- [x] appsettings HealthCheck section
- [x] Health integration tests + metrics test
- [x] Full suite green (200 tests)
- [x] Code review (two-axis: standards + spec — no blocking findings; applied IMeterFactory
      meter-ownership fix)
- [x] Commit
