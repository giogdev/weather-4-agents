using System.ComponentModel.DataAnnotations;

namespace Weather4Agents.API.Settings;

/// <summary>
/// Fixed-window rate-limiting options for the weather endpoints. A stranger reaching the API
/// can otherwise exhaust memory (an unbounded cache entry per location) and make the instance
/// bombard the provider; the limiter caps how many requests a single client IP may make per
/// window.
/// </summary>
public class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Name of the rate-limiting policy applied to the weather endpoints.</summary>
    public const string PolicyName = "weather";

    /// <summary>Maximum accepted window length, in seconds (one hour).</summary>
    public const int MaxWindowSeconds = 3600;

    /// <summary>
    /// Turns the limiter on or off. On by default; set to <c>false</c> for a trusted LAN
    /// deployment where limiting is undesirable.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Requests allowed per client IP within each <see cref="WindowSeconds"/> window.</summary>
    [Range(1, int.MaxValue,
        ErrorMessage = "RateLimiting:PermitLimit must be at least 1.")]
    public int PermitLimit { get; set; } = 100;

    /// <summary>Length of the fixed window, in seconds.</summary>
    [Range(1, MaxWindowSeconds,
        ErrorMessage = "RateLimiting:WindowSeconds must be between {1} and {2} seconds.")]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Requests queued once the permit limit is reached (served as slots free up within the
    /// window). Zero means excess requests are rejected immediately with <c>429</c>.
    /// </summary>
    [Range(0, int.MaxValue,
        ErrorMessage = "RateLimiting:QueueLimit cannot be negative.")]
    public int QueueLimit { get; set; }
}
