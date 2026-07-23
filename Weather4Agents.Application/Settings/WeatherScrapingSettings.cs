using System.ComponentModel.DataAnnotations;

namespace Weather4Agents.Application.Settings;

public class WeatherScrapingSettings : IValidatableObject
{
    public const string SectionName = "WeatherScraping";

    /// <summary>Maximum accepted job interval, in minutes (24 hours).</summary>
    public const int MaxJobIntervalMinutes = 1440;

    /// <summary>Maximum accepted per-request HTTP timeout, in seconds.</summary>
    public const int MaxHttpTimeoutSeconds = 60;

    /// <summary>Maximum accepted cache TTL for the "extended" (future days) segment, in hours.</summary>
    public const int MaxExtendedCacheHours = 168;

    /// <summary>Maximum accepted negative-cache TTL, in minutes.</summary>
    public const int MaxNegativeCacheMinutes = 60;

    [Required(AllowEmptyStrings = false,
        ErrorMessage = "WeatherScraping:DefaultProvider is required.")]
    public string DefaultProvider { get; set; } = string.Empty;

    [MinLength(1,
        ErrorMessage = "WeatherScraping:EnabledProviders must list at least one provider.")]
    public List<string> EnabledProviders { get; set; } = [];

    public List<string> Locations { get; set; } = [];

    /// <summary>
    /// When <c>true</c> (the default) any well-formed location is servable, preserving the
    /// original open behaviour. When <c>false</c> only the locations listed in
    /// <see cref="Locations"/> are served; a request for any other location is rejected at the
    /// API boundary — before a scrape is triggered — so a stranger cannot make the instance
    /// bombard the provider with arbitrary locations. Matching is done on the normalized spelling
    /// (see <c>LocationName.Normalize</c>), so spaced and hyphenated spellings both match.
    /// </summary>
    public bool AllowUnconfiguredLocations { get; set; } = true;

    [Range(1, MaxJobIntervalMinutes,
        ErrorMessage = "WeatherScraping:JobIntervalMinutes must be between {1} and {2} minutes.")]
    public int JobIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Per-attempt HTTP timeout for provider page fetches, in seconds. Bounds how long a single
    /// hanging request is awaited before the resilience handler abandons the attempt, so one slow
    /// page cannot stall a whole scraping cycle. Applied as the resilience handler's attempt
    /// timeout rather than <c>HttpClient.Timeout</c>, which would otherwise preempt retries.
    /// </summary>
    [Range(1, MaxHttpTimeoutSeconds,
        ErrorMessage = "WeatherScraping:HttpTimeoutSeconds must be between {1} and {2} seconds.")]
    public int HttpTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Cache TTL for the current day's forecast, in minutes. The current day is cached separately
    /// and with a much shorter lifetime than the following days because providers refresh today's
    /// data frequently (~30 min). On expiry only today's page is re-scraped, not the whole week.
    /// </summary>
    [Range(1, MaxJobIntervalMinutes,
        ErrorMessage = "WeatherScraping:TodayCacheMinutes must be between {1} and {2} minutes.")]
    public int TodayCacheMinutes { get; set; } = 30;

    /// <summary>
    /// Cache TTL for the following days' forecast (day 1 onward), in hours. Future days change
    /// slowly, so they are cached far longer than the current day to keep provider load low.
    /// </summary>
    [Range(1, MaxExtendedCacheHours,
        ErrorMessage = "WeatherScraping:ExtendedCacheHours must be between {1} and {2} hours.")]
    public int ExtendedCacheHours { get; set; } = 6;

    /// <summary>
    /// TTL for an empty scrape (provider unreachable or unknown location), in minutes. Short by
    /// design so a location that becomes valid is re-scraped within minutes instead of being
    /// masked by an empty result for the full positive TTL.
    /// </summary>
    [Range(1, MaxNegativeCacheMinutes,
        ErrorMessage = "WeatherScraping:NegativeCacheMinutes must be between {1} and {2} minutes.")]
    public int NegativeCacheMinutes { get; set; } = 5;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Cross-field rule: the default provider must be one of the enabled providers,
        // otherwise the configuration is internally inconsistent (a provider serving every
        // request while sitting outside the "enabled" set).
        if (!string.IsNullOrWhiteSpace(DefaultProvider)
            && EnabledProviders.Count > 0
            && !EnabledProviders.Contains(DefaultProvider, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"WeatherScraping:DefaultProvider '{DefaultProvider}' must be one of the enabled providers: {string.Join(", ", EnabledProviders)}.",
                [nameof(DefaultProvider)]);
        }
    }
}
