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
    public static async Task<List<DayWeather>> GetForecastOrNotFoundAsync(
        this IWeatherProviderScraper scraper,
        string location,
        CancellationToken ct)
    {
        var forecast = (await scraper.GetForecastAsync(location, forceRefresh: false, ct)).ToList();
        if (forecast.Count == 0)
            throw new LocationNotFoundException(location, scraper.ProviderName);

        return forecast;
    }
}
