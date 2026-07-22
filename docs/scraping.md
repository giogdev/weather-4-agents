# Responsible scraping

Weather4Agents obtains forecasts by scraping the public web pages of the configured provider
(currently [3bMeteo](https://www.3bmeteo.com)). Scraping a third-party site is a shared resource;
this page documents how the project tries to be a polite consumer and what you should check before
deploying.

## User-Agent

The scraper sends a browser-like `User-Agent` header (a recent desktop Chrome string, set in
`Meteo3bScraper`). Provider pages render differently — or reject requests outright — when the
client does not look like a browser, so this is required for the HTML to be parseable.

This means the requests are **not** anonymous crawler traffic: identify yourself honestly if a
provider asks, and do not use the tool to disguise abusive volume.

## Request volume and the minimum interval

One scraping cycle fetches up to **8 pages per location** (today plus the next seven days), for
every configured location × enabled provider. To keep that load low:

- The result of each cycle is cached for 24 hours, so API requests are served from cache and do
  **not** hit the provider on every call.
- A location that yields nothing is negative-cached for a short period rather than re-fetched
  immediately.
- Per-attempt HTTP timeouts and a resilience handler (retry with backoff + circuit breaker) prevent
  a struggling provider from being hammered.

**Recommended minimum `WeatherScraping:JobIntervalMinutes`: 60 (one hour).** Weather forecasts do
not change faster than that in practice, so a shorter interval adds load without adding value. Keep
the number of configured locations to what you actually consume. The setting accepts 1–1440
minutes, but values below ~30 minutes are discouraged.

## Terms of service and robots.txt

You are responsible for using this tool in line with the provider's terms:

- Review the provider's Terms of Service and `robots.txt` before scraping, and respect any stated
  limits.
- This project is intended for personal / self-hosted use (e.g. feeding a Home Assistant instance
  or a personal agent), not for redistributing the provider's data or running it at commercial
  scale.
- If a provider offers an official API, prefer it — a dedicated provider implementation can be
  added instead of scraping.
