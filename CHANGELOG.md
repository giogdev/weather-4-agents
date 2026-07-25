# Changelog
## Unreleased
- Differentiated cache lifetimes: the current day is cached briefly (`WeatherScraping__TodayCacheMinutes`, default 30 min) while the following days are cached longer (`WeatherScraping__ExtendedCacheHours`, default 6 h). A cold request scrapes the whole week once; when only the current-day entry expires, just that page is re-scraped. The empty-scrape negative cache is now configurable (`WeatherScraping__NegativeCacheMinutes`, default 5 min)
- Documentation: added an API contract page ([docs/api.md](docs/api.md)) and a responsible-scraping note ([docs/scraping.md](docs/scraping.md)); fixed README links and Docker instructions; refreshed the background-jobs docs for the single merged scraping/storage cycle
- Repository hygiene: added `.editorconfig` and `Directory.Build.props` with the recommended analyzer set; CI now verifies formatting (`dotnet format`) and publishes a coverage report
## v1.2.0
- New endpoint to get the next 24 hours forecast as hourly slots (`{location}/forecast/next-24h`)
## v1.1.0
- Moved forecast reliability percentage from hourly slot level to day level (`DayWeather.ReliabilityPerc` / `DayForecastEntry.ReliabilityPerc`)
- Added per-slot precipitation probability (`HoursWeatherDetails.PrecipitationProbabilityPerc`), exposed when the provider supplies it
- Updated 3bMeteo scraper to support v3 website layout (new `table-previsioni-ora` table structure and today-page detection)
- Fixed temperature parsing to avoid false match with `unit-tempo` CSS class
## v1.0.5
- Updated 3bmeteo web scraper (3bmeteo website was updated)
## v1.0.4
- Updated 3bmeteo web scraper (3bmeteo website was refactored)
## v1.0.3
- OpenApi definitions in `<endpoint>/openapi/v1.json`
- Refactoring of api endpoints definitions
## v1.0.2
- Fixed bug with mapping of word "velature sparse" (3bmeteo)
- Custom integration for Home Assistant (Integrations/HomeAssistant)
## v1.0.1
- Removed Mediatr dependency and implemented a base service to handle CQRS pattern
- Improved appsettings loading for development mode
- Docker image for arm64 (raspberry) and amd64
- New endpoint to get 7 days forecast
## v1.0.0
- Initial release
