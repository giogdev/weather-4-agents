using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Infrastructure.Scrapers;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// Boots the API in-memory with the real pipeline (routing, controllers, DI, config)
/// but no outbound side effects: the real scraper is replaced by
/// <see cref="FakeWeatherProviderScraper"/>, background jobs are removed,
/// and the clock is pinned to <see cref="InitialTime"/> via <see cref="Clock"/>.
/// </summary>
public class Weather4AgentsApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Pinned test clock: 2026-05-14 08:00 UTC.</summary>
    public static readonly DateTimeOffset InitialTime =
        new(2026, 5, 14, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The fake scraper the API is wired to. Resolved from the host's container because it
    /// shares the application's <c>HybridCache</c>; accessing it builds the host if needed.
    /// </summary>
    public FakeWeatherProviderScraper Scraper =>
        Services.GetRequiredService<FakeWeatherProviderScraper>();

    public FakeTimeProvider Clock { get; } = new(InitialTime);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The resolver picks the default provider from configuration; startup validation
        // additionally requires it to be one of the enabled providers, so set both to the fake.
        builder.UseSetting("WeatherScraping:DefaultProvider", FakeWeatherProviderScraper.Name);
        builder.UseSetting("WeatherScraping:EnabledProviders:0", FakeWeatherProviderScraper.Name);

        builder.ConfigureTestServices(services =>
        {
            // No scraping/file-storage background jobs in tests.
            services.RemoveAll<IHostedService>();

            // Swap the real scraper (and its typed HttpClient) for the fake, built by the
            // container so it uses the same HybridCache as the rest of the app.
            services.RemoveAll<IWeatherProviderScraper>();
            services.RemoveAll<Meteo3bScraper>();
            services.AddSingleton<FakeWeatherProviderScraper>();
            services.AddSingleton<IWeatherProviderScraper>(
                sp => sp.GetRequiredService<FakeWeatherProviderScraper>());

            // Pin the clock.
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
