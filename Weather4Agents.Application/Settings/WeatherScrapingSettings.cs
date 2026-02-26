namespace Weather4Agents.Application.Settings;

public class WeatherScrapingSettings
{
    public const string SectionName = "WeatherScraping";

    public string DefaultProvider { get; set; } = string.Empty;
    public List<string> EnabledProviders { get; set; } = [];
    public List<string> Locations { get; set; } = [];
    public int JobIntervalMinutes { get; set; } = 60;
}
