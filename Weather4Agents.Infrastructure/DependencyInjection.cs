using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Application.Settings;
using Weather4Agents.Infrastructure.Jobs;
using Weather4Agents.Infrastructure.Resolvers;
using Weather4Agents.Infrastructure.Scrapers;
using Weather4Agents.Infrastructure.Storage;

namespace Weather4Agents.Infrastructure;

public static class DependencyInjection
{
    // The retry sequence needs a total budget larger than a single attempt, and the circuit
    // breaker's sampling window must be at least twice the attempt timeout (a handler validation
    // rule). Both are expressed as multiples of the configured per-attempt timeout.
    private const int TotalRequestTimeoutMultiplier = 3;
    private const int CircuitBreakerSamplingMultiplier = 2;

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

        // Typed HTTP clients. The per-request bound on a hanging page lives in the resilience
        // handler's attempt timeout, not HttpClient.Timeout: a low client timeout wraps the whole
        // pipeline and would preempt the retries below. The timeout value is read straight from the
        // same configuration section that WeatherScrapingSettings binds — an out-of-range value is
        // rejected by that settings type's ValidateOnStart before the host ever starts. (Coupling
        // this to IOptions<WeatherScrapingSettings> is deliberately avoided: it would make the two
        // validated options cross-trigger and turn a clean startup error into an AggregateException.)
        var httpTimeout = TimeSpan.FromSeconds(
            configuration
                .GetSection(WeatherScrapingSettings.SectionName)
                .GetValue(nameof(WeatherScrapingSettings.HttpTimeoutSeconds),
                    new WeatherScrapingSettings().HttpTimeoutSeconds));

        services.AddHttpClient<Meteo3bScraper>()
            .AddStandardResilienceHandler(options =>
            {
                // Abandon a hanging request within the configured timeout, per attempt.
                options.AttemptTimeout.Timeout = httpTimeout;
                // The total budget caps the whole retry sequence so a persistently slow page
                // cannot stall the cycle for minutes.
                options.TotalRequestTimeout.Timeout = httpTimeout * TotalRequestTimeoutMultiplier;
                options.CircuitBreaker.SamplingDuration = httpTimeout * CircuitBreakerSamplingMultiplier;
            });

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

        // File storage is a step of the scraping job (Option A: one schedule), not a separate
        // hosted service. The store is resolved per scope from within that job.
        services.AddTransient<WeatherFileStore>();

        services.AddHostedService<WeatherScrapingJob>();

        return services;
    }
}
