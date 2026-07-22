# Ticket 17 — API conveniences: caching headers & today shortcut

## Goal
- Forecast responses carry `Cache-Control` + `ETag` (derived from the data's scrape timestamp)
  so polling agents can revalidate with `If-None-Match` and get `304 Not Modified` on unchanged data.
- A `today` shortcut endpoint returns the current day's weather without the caller computing the
  date, honouring the provider timezone (ticket 08).

## Acceptance criteria
- [ ] `If-None-Match` matching the current ETag → `304` with no body.
- [ ] ETag changes when the underlying data is re-scraped.
- [ ] `today` returns the same payload as `date/{today}`, honouring the provider timezone.
- [ ] Integration tests cover 304, ETag rotation, and the shortcut.
- [ ] All tests green; nothing committed until reviewed.

## Design
1. **Shared freshness contract** — `IFreshnessStamped { DateTimeOffset LastUpdatedAt }` in
   `Application/DTOs`, implemented by `ForecastResponse`, `WeekForecastResponse`,
   `Next24HoursForecastResponse`, `DayWeatherResponse`. Gives the controller a single hook for
   ETag/Cache-Control across all forecast responses.
2. **Conditional-request helper** on `WeatherController`: `CacheableOk(IFreshnessStamped)`:
   - ETag = strong, quoted hex of `LastUpdatedAt.UtcTicks` (changes iff the scrape time changes).
   - Sets `ETag` + `Cache-Control: private, no-cache` (store-but-revalidate; pairs with ETag/304).
   - If `If-None-Match` contains the ETag (or `*`) → `304` (weak comparison, per RFC 9110).
   - ETag is scoped per-URL by clients, so deriving it purely from the scrape timestamp is safe.
   - Wire it into every forecast GET endpoint (days / week / next-24h / date / today).
3. **Today shortcut** — `GetTodayWeatherQuery(Location, ProviderName?) : IQuery<DayWeatherResponse?>`
   + `GetTodayWeatherHandler`: resolves the scraper, computes `GetLocalToday(_timeProvider)` in the
   provider timezone, then maps via `DayWeatherResponse.From(scraped, today)` — identical mapping to
   the explicit `date/{today}` endpoint, so the payloads match. `null` → 404, mirroring the date endpoint.
   - New route `GET api/weather/{location}/forecast/today` (literal segment, no route ambiguity).
   - Register the handler in `Application/DependencyInjection`.

## Tests (Integration, new file ApiConveniencesTests.cs)
- 304 on matching `If-None-Match` (no body) for the today endpoint.
- ETag rotates after a forced re-scrape (via `ScrapeAndCacheCommand`, as DataFreshnessTests does).
- `today` payload equals `date/{italian-today}` payload; timezone honoured (clock pinned just past
  Italian midnight so the Italian civil date differs from UTC).
- `Cache-Control` present on a 200.

## Checklist
- [x] `IFreshnessStamped` + implemented on the four response DTOs
- [x] `CacheableOk` helper + wired into all forecast endpoints
- [x] Today query + handler + route + DI registration
- [x] Integration tests (304, ETag rotation, shortcut, Cache-Control)
- [x] Full suite green (205 tests)
- [x] Code review (two-axis: standards + spec — no blocking findings; applied typed
      CacheControlHeaderValue for header-style consistency)
- [x] Commit
