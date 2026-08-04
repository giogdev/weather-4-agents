using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Weather4Agents.API.Settings;
using Weather4Agents.Infrastructure.Diagnostics;

namespace Weather4Agents.API.HealthChecks;

/// <summary>
/// Custom liveness signal: reports whether the background scraping job has completed a
/// successful cycle recently enough. Mapping <c>/health</c> also gives a plain liveness check
/// (the app answers HTTP); this check adds the "is it actually producing fresh data?" dimension
/// that a bare liveness probe cannot see.
/// </summary>
public sealed class ScrapeFreshnessHealthCheck : IHealthCheck
{
    private readonly ScrapeCycleTracker _tracker;
    private readonly TimeProvider _timeProvider;
    private readonly HealthCheckSettings _settings;

    public ScrapeFreshnessHealthCheck(
        ScrapeCycleTracker tracker,
        TimeProvider timeProvider,
        IOptions<HealthCheckSettings> options)
    {
        _tracker = tracker;
        _timeProvider = timeProvider;
        _settings = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var lastSuccess = _tracker.LastSuccessfulCycleAt;

        // No cycle has completed yet (fresh start): degraded rather than unhealthy so a slow
        // first scrape does not fail the container's health probe during its start period.
        if (lastSuccess is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "No scraping cycle has completed successfully yet."));
        }

        var age = _timeProvider.GetUtcNow() - lastSuccess.Value;
        var maxAge = TimeSpan.FromMinutes(_settings.MaxScrapeAgeMinutes);

        var data = new Dictionary<string, object>
        {
            ["lastSuccessfulCycleAt"] = lastSuccess.Value,
            ["ageSeconds"] = Math.Round(age.TotalSeconds),
            ["maxScrapeAgeMinutes"] = _settings.MaxScrapeAgeMinutes
        };

        return Task.FromResult(age <= maxAge
            ? HealthCheckResult.Healthy(
                $"Last successful scrape cycle {Math.Round(age.TotalMinutes)} minute(s) ago.", data)
            : HealthCheckResult.Unhealthy(
                $"Last successful scrape cycle was {Math.Round(age.TotalMinutes)} minute(s) ago, " +
                $"older than the {_settings.MaxScrapeAgeMinutes}-minute limit.", data: data));
    }
}
