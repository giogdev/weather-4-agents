using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Infrastructure;
using Weather4Agents.Infrastructure.Scrapers;

namespace Weather4Agents.Test.Scrapers;

/// <summary>
/// The standard resilience handler configured on the <see cref="Meteo3bScraper"/> typed client
/// is exercised through the real DI pipeline (<see cref="DependencyInjection.AddInfrastructure"/>):
/// transient failures are retried, and a request that hangs past the configured per-attempt
/// timeout is abandoned well within the hang duration instead of blocking for the full time.
/// </summary>
public class HttpResilienceTests
{
    /// <summary>
    /// A primary handler with scriptable behaviour: it counts calls and, per call index,
    /// either returns a status code or delays (to simulate a hang) before returning 200.
    /// </summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpStatusCode> _statusPerCall;
        private readonly TimeSpan _delay;

        public ScriptedHandler(Func<int, HttpStatusCode> statusPerCall, TimeSpan? delay = null)
        {
            _statusPerCall = statusPerCall;
            _delay = delay ?? TimeSpan.Zero;
        }

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = CallCount++;
            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(_statusPerCall(index))
            {
                Content = new StringContent("<html></html>")
            };
        }
    }

    private static ServiceProvider BuildProvider(ScriptedHandler handler, int httpTimeoutSeconds = 15)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WeatherScraping:DefaultProvider"] = "3bMeteo",
                ["WeatherScraping:EnabledProviders:0"] = "3bMeteo",
                ["WeatherScraping:JobIntervalMinutes"] = "60",
                ["WeatherScraping:HttpTimeoutSeconds"] = httpTimeoutSeconds.ToString(),
                ["WeatherFileStorage:Enabled"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        // Swap in the scripted primary handler while keeping the resilience handler that
        // AddInfrastructure attached to the same named client.
        services.AddHttpClient<Meteo3bScraper>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private static HttpClient ScraperClient(ServiceProvider provider)
        => provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(Meteo3bScraper));

    [Fact]
    public async Task TransientFailure_IsRetried_AndTheRequestUltimatelySucceeds()
    {
        // Fail the first attempt with a 503, then succeed. The standard retry strategy should
        // retry the transient failure and surface the eventual 200.
        var handler = new ScriptedHandler(call => call == 0 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        await using var provider = BuildProvider(handler);
        var client = ScraperClient(provider);

        var response = await client.GetAsync("https://www.3bmeteo.com/meteo/milano");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(handler.CallCount >= 2, $"Expected a retry after the 503, saw {handler.CallCount} call(s).");
    }

    [Fact]
    public async Task HangingRequest_IsAbandonedWithinTheConfiguredTimeout()
    {
        // A 1-second per-attempt timeout against a handler that hangs for 10 seconds: the
        // pipeline must give up (per-attempt timeout, capped by the total request budget) in a
        // small multiple of the timeout — far short of the 10-second hang.
        var handler = new ScriptedHandler(_ => HttpStatusCode.OK, delay: TimeSpan.FromSeconds(10));
        await using var provider = BuildProvider(handler, httpTimeoutSeconds: 1);
        var client = ScraperClient(provider);

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(
            () => client.GetAsync("https://www.3bmeteo.com/meteo/milano"));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8),
            $"Request should have been abandoned well within the 10s hang, took {sw.Elapsed.TotalSeconds:F1}s.");
    }
}
