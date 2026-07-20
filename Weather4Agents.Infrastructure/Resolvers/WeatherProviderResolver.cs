using Microsoft.Extensions.Options;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Application.Settings;
using Weather4Agents.Domain.Exceptions;

namespace Weather4Agents.Infrastructure.Resolvers;

/// <summary>
/// Provides functionality to resolve and retrieve weather provider scrapers based on their names.
/// </summary>
public class WeatherProviderResolver : IWeatherProviderResolver
{
    private readonly IReadOnlyDictionary<string, IWeatherProviderScraper> _scrapers;
    private readonly WeatherScrapingSettings _settings;

    public WeatherProviderResolver(
        IEnumerable<IWeatherProviderScraper> scrapers,
        IOptions<WeatherScrapingSettings> options)
    {
        _scrapers = scrapers.ToDictionary(
            s => s.ProviderName,
            s => s,
            StringComparer.OrdinalIgnoreCase);

        _settings = options.Value;
    }

    public IWeatherProviderScraper GetDefault()
        => GetByName(_settings.DefaultProvider);

    public IWeatherProviderScraper GetByName(string providerName)
        => _scrapers.TryGetValue(providerName, out var scraper)
            ? scraper
            : throw new ProviderNotFoundException(providerName, _scrapers.Keys.ToArray());

    public IEnumerable<string> GetAvailableProviders()
        => _scrapers.Keys;
}
