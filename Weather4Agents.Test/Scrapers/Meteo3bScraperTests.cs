using System.Reflection;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Infrastructure.Scrapers;

namespace Weather4Agents.Test.Scrapers;

public class Meteo3bScraperTests
{
    private static Meteo3bScraper CreateScraper()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        var hybridCache = provider.GetRequiredService<HybridCache>();
        return new Meteo3bScraper(new HttpClient(), hybridCache);
    }

    private static DayWeather InvokeParseDayPage(Meteo3bScraper scraper, string html, DateOnly date)
    {
        var method = typeof(Meteo3bScraper).GetMethod(
            "ParseDayPage",
            BindingFlags.NonPublic | BindingFlags.Instance);

        return (DayWeather)method!.Invoke(scraper, [html, date])!;
    }

    [Fact]
    public void ParseDayPage_WithSampleHtml_Returns13HourlyForecasts()
    {
        // Arrange
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-13.html"));
        var date = new DateOnly(2026, 2, 26);

        // Act
        var result = InvokeParseDayPage(scraper, html, date);

        // Assert
        Assert.Equal(13, result.HoursDetails.Count);
    }


    [Fact]
    public void ParseDayPage_WithSampleHtml_Returns24HourlyForecasts()
    {
        // Arrange
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-24.html"));
        var date = new DateOnly(2026, 2, 26);

        // Act
        var result = InvokeParseDayPage(scraper, html, date);

        // Assert
        Assert.Equal(24, result.HoursDetails.Count);
    }
}
