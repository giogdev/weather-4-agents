using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Exceptions;

namespace Weather4Agents.Application.Interfaces.Scrapers;

public static class WeatherProviderScraperExtensions
{
    /// <summary>
    /// Retrieves and materialises the forecast for <paramref name="location"/>, raising
    /// <see cref="LocationNotFoundException"/> when the provider has no data. This is the single
    /// place the "an empty forecast means the location is unknown → 404" policy lives, so the
    /// query handlers don't each re-implement it.
    /// </summary>
    public static async Task<ScrapedForecast> GetForecastOrNotFoundAsync(
        this IWeatherProviderScraper scraper,
        string location,
        CancellationToken ct)
    {
        var forecast = await scraper.GetForecastAsync(location, forceRefresh: false, ct);
        if (forecast.Days.Count == 0)
            throw new LocationNotFoundException(location, scraper.ProviderName);

        return forecast;
    }

    /// <summary>
    /// Current wall-clock time in the provider's timezone (see
    /// <see cref="IWeatherProviderScraper.TimeZone"/>), taken from the injected clock so tests
    /// can pin it. This is the only correct "now" to compare against the provider's local
    /// forecast dates and times.
    /// </summary>
    public static DateTime GetLocalNow(this IWeatherProviderScraper scraper, TimeProvider timeProvider)
        => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), scraper.TimeZone).DateTime;

    /// <summary>
    /// Current civil date in the provider's timezone. Around midnight this differs from the
    /// host-timezone date, which would shift forecasts by a day.
    /// </summary>
    public static DateOnly GetLocalToday(this IWeatherProviderScraper scraper, TimeProvider timeProvider)
        => DateOnly.FromDateTime(scraper.GetLocalNow(timeProvider));
}
