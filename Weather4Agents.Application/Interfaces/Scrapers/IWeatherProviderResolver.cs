namespace Weather4Agents.Application.Interfaces.Scrapers;

public interface IWeatherProviderResolver
{
    IWeatherProviderScraper GetDefault();
    IWeatherProviderScraper GetByName(string providerName);
    IEnumerable<string> GetAvailableProviders();
}
