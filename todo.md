# Ticket 14 — DTOs at the API boundary & WeatherType enum (plan items Q2, Q3, part of Q4)

## Goal
Decouple the wire contract from the domain model without changing what existing consumers
(Home Assistant, agents) parse:
- the two endpoints that still returned domain entities (multi-day forecast, single-day weather)
  get response DTOs consistent with the week/next-24h responses (freshness + timezone envelope),
- `WeatherType` becomes a real enum serialized to the exact same strings as before,
- the `WeatherProvider` value object stops exposing public mutable setters.

Property renames that would break consumers (e.g. the `PressionMbar` typo) stay deferred per spec.

## Design
- **WeatherType enum**: `WeatherTypeEnums.cs` becomes a real `enum` with a type-level
  `[JsonConverter(typeof(JsonStringEnumConverter<WeatherType>))]`, so every value serializes as
  its member name verbatim (PascalCase) regardless of the options at the call site. `Unknown` is
  first so `default(WeatherType)` keeps meaning "Unknown". `HoursWeatherDetails.WeatherType` and
  `Meteo3bWeatherTypeMapper.Map` switch from `string` to the enum.
  - The file-storage job dropped its `JsonStringEnumConverter(camelCase)`: with a real enum that
    converter would have lowercased the strings (options converters outrank the type attribute).
    The type attribute now owns the format for both API and on-disk JSON.
- **Response DTOs**: new `ForecastResponse` (multi-day) and `DayWeatherResponse` (single-day),
  each with `LastUpdatedAt` + `Timezone` + the day entries, mirroring `WeekForecastResponse`.
  Both are built via static `From(...)` factories; freshness/timezone come from a shared
  `ForecastEnvelope` so the two entity-derived endpoints report them identically. `DayForecastEntry`
  gains a `From(DayWeather)` projection (also reused by the week handler).
  - `GetWeatherForecastQuery` still returns the domain `ScrapedForecast` (the file-storage job
    needs the full `DayWeather`); the controller maps it to `ForecastResponse`.
  - `GetDayWeatherQuery`/handler now return `DayWeatherResponse?`.
  - The controller's `ProducesResponseType` for the week endpoint was corrected from
    `IEnumerable<DayWeather>` (a domain type in the signature) to `WeekForecastResponse`.
- **WeatherProvider**: `ProviderName` → `{ get; }`, `TimeZoneId` → `{ get; init; }`. Constructor +
  object-initializer usages are unaffected; System.Text.Json still round-trips via the constructor.

## Tests
- `WeatherTypeSerializationTests` (new): snapshot of every enum value → its historical string, under
  API web defaults and under camelCase property naming, plus a round-trip and the `Unknown` default.
- `WeatherEndpointsTests`: multi-day updated to the new envelope (`forecast[]`, `lastUpdatedAt`,
  `timezone`, `weatherType` == "Sunny"); new single-day success test asserting the day envelope.

## Checklist
- [x] Snapshot test locks byte-identical weather-type strings
- [x] `WeatherType` is a real enum (invalid values no longer constructible in code)
- [x] Response DTOs for the multi-day and single-day endpoints; no domain type in a controller
      response signature
- [x] `WeatherProvider` has no public mutable setters
- [x] Home Assistant integration unaffected (it consumes only `/forecast/week`, whose shape and
      `weatherType` strings are unchanged)
- [x] Full suite green (187 passed)
- [x] Code review (two-axis: standards + spec)
      - Standards: no documented-standard violations. Timezone divergence + envelope duplication
        flagged and resolved via the shared `ForecastEnvelope` + `From` factories. Nested
        `HoursWeatherDetails` on the wire is pre-existing and in-scope-consistent (matches the
        existing week/next-24h DTOs; property renames deferred per spec).
      - Spec: all acceptance criteria met; the flagged multi-day/sibling timezone inconsistency is
        resolved (single source of truth).
- [ ] Commit
