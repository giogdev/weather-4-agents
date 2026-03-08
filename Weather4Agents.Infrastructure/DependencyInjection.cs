using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Application.Settings;
using Weather4Agents.Application.Settings.Integrations;
using Weather4Agents.Infrastructure.Integrations.HomeAssistant;
using Weather4Agents.Infrastructure.Jobs;
using Weather4Agents.Infrastructure.Resolvers;
using Weather4Agents.Infrastructure.Scrapers;

namespace Weather4Agents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WeatherScrapingSettings>(
            configuration.GetSection(WeatherScrapingSettings.SectionName));

        // Typed HTTP clients
        services.AddHttpClient<Meteo3bScraper>();

        // Register each scraper also as IWeatherProviderScraper for IEnumerable<> resolution
        services.AddTransient<IWeatherProviderScraper>(
            sp => sp.GetRequiredService<Meteo3bScraper>());

        services.AddTransient<IWeatherProviderResolver, WeatherProviderResolver>();

        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24),
                LocalCacheExpiration = TimeSpan.FromHours(24)
            };
        });

        services.Configure<WeatherFileStorageSettings>(
            configuration.GetSection(WeatherFileStorageSettings.SectionName));

        services.Configure<HomeAssistantIntegrationSettings>(
            configuration.GetSection(HomeAssistantIntegrationSettings.SectionName));

        services.AddTransient<IHomeAssistantIntegration, HomeAssistantIntegration>();

        services.AddHostedService<WeatherScrapingJob>();
        services.AddHostedService<WeatherFileStorageJob>();

        return services;
    }
}
