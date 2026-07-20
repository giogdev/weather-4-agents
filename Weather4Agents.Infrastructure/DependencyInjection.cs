using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Application.Settings;
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
        // Settings are validated with DataAnnotations and checked at startup so that an
        // invalid configuration (e.g. a non-positive job interval, a missing default provider,
        // or a default provider absent from the enabled list) fails fast with a clear message
        // instead of crashing the host mid-run or hammering the provider.
        services.AddOptions<WeatherScrapingSettings>()
            .Bind(configuration.GetSection(WeatherScrapingSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<Meteo3bWeatherTypeMapper>();

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

        services.AddOptions<WeatherFileStorageSettings>()
            .Bind(configuration.GetSection(WeatherFileStorageSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<WeatherScrapingJob>();
        services.AddHostedService<WeatherFileStorageJob>();

        return services;
    }
}
