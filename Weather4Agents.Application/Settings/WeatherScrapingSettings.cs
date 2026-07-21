using System.ComponentModel.DataAnnotations;

namespace Weather4Agents.Application.Settings;

public class WeatherScrapingSettings : IValidatableObject
{
    public const string SectionName = "WeatherScraping";

    /// <summary>Maximum accepted job interval, in minutes (24 hours).</summary>
    public const int MaxJobIntervalMinutes = 1440;

    /// <summary>Maximum accepted per-request HTTP timeout, in seconds.</summary>
    public const int MaxHttpTimeoutSeconds = 60;

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
