using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// Verifies that <c>ValidateOnStart()</c> is actually wired up: an invalid configuration
/// prevents the host from starting instead of failing later at runtime.
/// </summary>
public class SettingsValidationStartupTests
{
    [Fact]
    public void Host_WithNonPositiveJobInterval_FailsToStart()
    {
        using var factory = new Weather4AgentsApiFactory()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("WeatherScraping:JobIntervalMinutes", "0"));

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("WeatherScraping:JobIntervalMinutes", ex.Message);
    }

    [Fact]
    public void Host_WithDefaultProviderNotEnabled_FailsToStart()
    {
        using var factory = new Weather4AgentsApiFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("WeatherScraping:DefaultProvider", "ghost");
                builder.UseSetting("WeatherScraping:EnabledProviders:0", "3bMeteo");
            });

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("ghost", ex.Message);
    }

    [Fact]
    public void Host_WithValidConfiguration_StartsSuccessfully()
    {
        using var factory = new Weather4AgentsApiFactory();

        // Does not throw: the happy path still boots exactly as before.
        using var client = factory.CreateClient();
        Assert.NotNull(client);
    }
}
