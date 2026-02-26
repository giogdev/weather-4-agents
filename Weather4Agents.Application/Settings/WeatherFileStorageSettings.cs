namespace Weather4Agents.Application.Settings;

public class WeatherFileStorageSettings
{
    public const string SectionName = "WeatherFileStorage";

    /// <summary>
    /// Enables or disables the file storage job entirely.
    /// Can be overridden via environment variable <c>WeatherFileStorage__Enabled</c>.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Root directory where weather JSON files are written.
    /// Can be overridden via environment variable <c>WeatherFileStorage__OutputPath</c>.
    /// </summary>
    public string OutputPath { get; set; } = "weather-data";

    /// <summary>
    /// How often the file storage job runs, in minutes.
    /// Can be overridden via environment variable <c>WeatherFileStorage__JobIntervalMinutes</c>.
    /// </summary>
    public int JobIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// When <c>true</c>, JSON files whose date is more than one day in the past are deleted
    /// at the end of each storage cycle.
    /// Can be overridden via environment variable <c>WeatherFileStorage__CleanupEnabled</c>.
    /// </summary>
    public bool CleanupEnabled { get; set; } = false;
}
