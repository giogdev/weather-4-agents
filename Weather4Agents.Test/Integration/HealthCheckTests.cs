using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Infrastructure.Diagnostics;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// End-to-end proof of the ticket-16 health endpoint: <c>/health</c> reflects whether the
/// background scraping job has completed a successful cycle recently. The scraping job is not
/// running in tests, so the shared <see cref="ScrapeCycleTracker"/> is driven directly and the
/// pinned clock is advanced to cross the staleness window.
/// </summary>
public sealed class HealthCheckTests
{
    [Fact]
    public async Task Health_WithRecentSuccessfulCycle_ReturnsHealthy()
    {
        await using var factory = new Weather4AgentsApiFactory();
        MarkCycleSucceeded(factory);

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_WhenNoCycleHasCompletedYet_ReturnsDegradedButOk()
    {
        await using var factory = new Weather4AgentsApiFactory();

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        // Degraded (starting up) still answers 200 so a container's start-period probe passes
        // while the first scrape is in flight.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_WhenLastCycleIsOlderThanTheWindow_ReturnsUnhealthy()
    {
        await using var factory = new Weather4AgentsApiFactory();
        MarkCycleSucceeded(factory);

        // Default window is 120 minutes; jump past it so the last success is stale.
        factory.Clock.Advance(TimeSpan.FromMinutes(121));

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    private static void MarkCycleSucceeded(Weather4AgentsApiFactory factory)
        => factory.Services.GetRequiredService<ScrapeCycleTracker>().MarkCycleSucceeded();
}
