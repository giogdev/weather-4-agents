using System.ComponentModel.DataAnnotations;

namespace Weather4Agents.Application.Settings;

public class WeatherFileStorageSettings
{
    public const string SectionName = "WeatherFileStorage";

    /// <summary>
    /// Enables or disables file storage entirely. When enabled, forecasts are persisted to disk
    /// as the final step of each scraping cycle (there is no separate storage schedule).
    /// Can be overridden via environment variable <c>WeatherFileStorage__Enabled</c>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Root directory where weather JSON files are written.
    /// Can be overridden via environment variable <c>WeatherFileStorage__OutputPath</c>.
    /// </summary>
    [Required(AllowEmptyStrings = false,
        ErrorMessage = "WeatherFileStorage:OutputPath is required.")]
    public string OutputPath { get; set; } = "weather-data";

    /// <summary>
    /// When <c>true</c>, JSON files whose date is more than one day in the past are deleted
    /// at the end of each storage cycle.
    /// Can be overridden via environment variable <c>WeatherFileStorage__CleanupEnabled</c>.
    /// </summary>
    public bool CleanupEnabled { get; set; }
}
