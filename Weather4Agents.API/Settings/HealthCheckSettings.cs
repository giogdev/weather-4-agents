using System.ComponentModel.DataAnnotations;

namespace Weather4Agents.API.Settings;

/// <summary>
/// Options for the custom scrape-freshness health check. The check reports the instance
/// unhealthy once the last successful scraping cycle is older than <see cref="MaxScrapeAgeMinutes"/>,
/// letting Docker and orchestrators restart or drain an instance whose data has gone stale.
/// </summary>
public class HealthCheckSettings
{
    public const string SectionName = "HealthCheck";

    /// <summary>Maximum accepted staleness window, in minutes (24 hours).</summary>
    public const int MaxAgeMinutes = 1440;

    /// <summary>
    /// How old the last successful scraping cycle may be before the instance is reported
    /// unhealthy. Must comfortably exceed <c>WeatherScraping:JobIntervalMinutes</c> so a healthy
    /// instance is not flagged in the normal gap between cycles; the default (120) allows for a
    /// missed cycle at the default 60-minute interval.
    /// </summary>
    [Range(1, MaxAgeMinutes,
        ErrorMessage = "HealthCheck:MaxScrapeAgeMinutes must be between {1} and {2} minutes.")]
    public int MaxScrapeAgeMinutes { get; set; } = 120;
}
