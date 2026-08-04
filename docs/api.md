# API contract

The REST API returns weather forecasts scraped from the configured provider. The interactive
Scalar UI is served at `<endpoint>/scalar/v1` and the OpenAPI document at
`<endpoint>/openapi/v1.json`; this page documents the semantics that the schema alone does not
convey.

Base path: `api/weather`.

## Endpoints

| Method & path | Returns | Notes |
|---|---|---|
| `GET api/weather/{location}/forecast/days/{numberOfDays}` | multi-day forecast | `numberOfDays` ∈ [1, 8] |
| `GET api/weather/{location}/forecast/week` | 7-day forecast | today plus the next days, up to 7 |
| `GET api/weather/{location}/forecast/next-24h` | hourly slots for the next 24 hours | window is provider-local (see [Timezone](#timezone)) |
| `GET api/weather/{location}/forecast/date/{date}` | single day | `date` is `yyyy-MM-dd`; `404` if that day is not in the forecast |
| `GET api/weather/{location}/forecast/today` | single day (today) | same payload as `date/{today}`; the caller need not compute today's date |
| `GET api/configurations/providers` | list of available provider names | |

All forecast endpoints accept an optional `?provider=` query parameter. When omitted, the
configured default provider is used.

### The `location` parameter

`location` must be letters (any script, including accents), spaces, apostrophes and hyphens, with
at least one letter and at most 100 characters. Values that don't match get `400`. A location
containing spaces must be URL-encoded (e.g. `San%20Pellegrino%20Terme`).

Spelling is normalized before use — spaces and hyphens collapse to a single hyphen and the value is
lower-cased — so `San Pellegrino Terme` and `san-pellegrino-terme` resolve to the same forecast and
share a single scrape/cache entry.

## Timezone

Providers publish forecasts in their own local time. 3bMeteo publishes everything in Italian time
(`Europe/Rome`) regardless of the requested location.

- Every forecast response carries a `timezone` field with the IANA identifier the data was scraped
  in (e.g. `"Europe/Rome"`).
- **All dates and times in a response are local to that timezone** — the day `date`, and each
  hourly slot's `timeFrom`/`timeTo`. They are *not* UTC.
- "Today" (for the `today` endpoint) and the "next 24 hours" window are computed in the provider's
  timezone, so they stay correct even when the API runs on a UTC host (e.g. a Docker container).

The only UTC value in a response is `lastUpdatedAt` (see [Freshness](#freshness)).

## Reliability

Each day carries a `reliabilityPerc` (0–100): the provider's own confidence in that day's forecast.

- It is a **day-level** value, not per hourly slot.
- When the provider does not expose a reliability indicator, it defaults to `100`.
- Pages that only offer coarse six-hour slots (no hourly detail) are treated as low-confidence and
  reported as `20`, regardless of what the page claims.

## Freshness

`lastUpdatedAt` (UTC, ISO 8601) is **when the data was scraped from the provider**, not when the
response was produced. A response served from cache reports the original scrape time, so a value a
few hours old means the data really is that old — it is a freshness signal, not a response
timestamp.

The forecast is cached in two segments with different lifetimes, so how stale the data can be
depends on the day it covers:

- The **current day** is cached for a short time (default 30 minutes), because providers refresh
  today's data frequently. A response covering today is therefore at most that old.
- The **following days** are cached longer (default 6 hours), because they change slowly.

`lastUpdatedAt` for a multi-day response is the most recent scrape among the days it returns. As a
consequence, the current day's `ETag` rotates roughly every 30 minutes, while a request for a single
future day changes only every 6 hours.

### Caching & conditional requests

Forecast responses are built for polling agents:

- Each response has a strong `ETag` derived from `lastUpdatedAt`. The ETag changes only when the
  underlying data is re-scraped.
- Responses set `Cache-Control: private, no-cache` (store-but-revalidate).
- Send the previous `ETag` back in `If-None-Match`; if the data has not changed the API replies
  `304 Not Modified` with no body, so an unchanged forecast costs almost nothing to re-poll.

## Error behaviour

Errors are returned as [`ProblemDetails`](https://www.rfc-editor.org/rfc/rfc9457) (`application/problem+json`).

| Status | When |
|---|---|
| `400 Bad Request` | `location` fails validation, or `numberOfDays` is outside [1, 8] |
| `403 Forbidden` | the location whitelist is enabled (`AllowUnconfiguredLocations = false`) and the requested location is not configured |
| `404 Not Found` | the location is unknown / has no data, or the requested `date` is not in the forecast |
| `429 Too Many Requests` | the per-IP rate limit was exceeded; a `Retry-After` header indicates when to retry |

### Unknown locations and the `404` contract

A request for a location the provider does not know (or that returns nothing) yields `404`, not an
empty `200`. Internally an empty scrape is negative-cached for a short period (5 minutes) rather
than the normal forecast lifetimes, so a location that becomes valid is picked up within minutes
instead of being masked for hours.
