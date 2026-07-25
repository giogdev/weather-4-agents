using System.ComponentModel.DataAnnotations;
using Weather4Agents.Application.Settings;

namespace Weather4Agents.Test.Settings;

/// <summary>
/// Exercises the DataAnnotations + <see cref="IValidatableObject"/> rules on the settings
/// classes directly. These are the same rules enforced at startup by
/// <c>ValidateDataAnnotations().ValidateOnStart()</c>.
/// </summary>
public class SettingsValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(object settings)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            settings,
            new ValidationContext(settings),
            results,
            validateAllProperties: true);
        return results;
    }

    private static WeatherScrapingSettings ValidScraping() => new()
    {
        DefaultProvider = "3bMeteo",
        EnabledProviders = ["3bMeteo"],
        Locations = ["Bergamo"],
        JobIntervalMinutes = 60,
    };

    [Fact]
    public void ScrapingSettings_WithValidConfiguration_ProducesNoErrors()
    {
        Assert.Empty(Validate(ValidScraping()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(WeatherScrapingSettings.MaxJobIntervalMinutes + 1)]
    public void ScrapingSettings_WithOutOfRangeInterval_FailsNamingTheSetting(int interval)
    {
        var settings = ValidScraping();
        settings.JobIntervalMinutes = interval;

        var errors = Validate(settings);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("WeatherScraping:JobIntervalMinutes"));
    }

    [Fact]
    public void ScrapingSettings_HttpTimeoutSeconds_DefaultsToFifteen()
    {
        Assert.Equal(15, new WeatherScrapingSettings().HttpTimeoutSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(WeatherScrapingSettings.MaxHttpTimeoutSeconds + 1)]
    public void ScrapingSettings_WithOutOfRangeHttpTimeout_FailsNamingTheSetting(int seconds)
    {
        var settings = ValidScraping();
        settings.HttpTimeoutSeconds = seconds;

        var errors = Validate(settings);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("WeatherScraping:HttpTimeoutSeconds"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ScrapingSettings_WithMissingDefaultProvider_Fails(string provider)
    {
        var settings = ValidScraping();
        settings.DefaultProvider = provider;

        var errors = Validate(settings);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("WeatherScraping:DefaultProvider"));
    }

    [Fact]
    public void ScrapingSettings_WhenDefaultProviderNotInEnabledList_Fails()
    {
        var settings = ValidScraping();
        settings.DefaultProvider = "someOtherProvider";
        settings.EnabledProviders = ["3bMeteo"];

        var errors = Validate(settings);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("someOtherProvider"));
    }

    [Fact]
    public void ScrapingSettings_DefaultProviderInEnabledList_IsCaseInsensitive()
    {
        var settings = ValidScraping();
        settings.DefaultProvider = "3BMETEO";
        settings.EnabledProviders = ["3bmeteo"];

        Assert.Empty(Validate(settings));
    }

    [Fact]
    public void ScrapingSettings_WithNoEnabledProviders_Fails()
    {
        var settings = ValidScraping();
        settings.EnabledProviders = [];

        var errors = Validate(settings);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("WeatherScraping:EnabledProviders"));
    }

    [Fact]
    public void FileStorageSettings_WithDefaults_ProducesNoErrors()
    {
        Assert.Empty(Validate(new WeatherFileStorageSettings()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FileStorageSettings_WithMissingOutputPath_FailsNamingTheSetting(string outputPath)
    {
        var settings = new WeatherFileStorageSettings { OutputPath = outputPath };

        var errors = Validate(settings);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("WeatherFileStorage:OutputPath"));
    }
}
