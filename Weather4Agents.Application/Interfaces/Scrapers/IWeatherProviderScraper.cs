using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.Interfaces.Scrapers;

public interface IWeatherProviderScraper
{
    string ProviderName { get; }

    Task<IEnumerable<DayWeather>> GetForecastAsync(
        string location,
        bool forceRefresh = false,
        CancellationToken ct = default);
}
