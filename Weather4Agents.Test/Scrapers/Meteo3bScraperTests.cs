using System.Reflection;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;
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

    private static string Complete1Html =>
        File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-v3-complete1.html"));

    // v3 HTML examples
    private static string V3Complete1Html =>
        File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-v3-complete1.html"));

    private static string V3Complete4Html =>
        File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-v3-complete4.html"));

    private static string V3TodayHtml =>
        File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-v3-today1.html"));

    // -------------------------------------------------------------------------
    // Hourly page — slot count and ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithCompleteHtml_Returns24HourlyForecasts()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));

        Assert.Equal(24, result.HoursDetails.Count);
    }

    [Fact]
    public void ParseDayPage_WithCompleteHtml_SlotsAreOrderedByTimeFrom()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));

        var times = result.HoursDetails.Select(h => h.TimeFrom).ToList();
        Assert.Equal([.. times.OrderBy(t => t)], times);
    }

    [Fact]
    public void ParseDayPage_WithCompleteHtml_DoesNotReturnFourSlots()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));

        Assert.NotEqual(4, result.HoursDetails.Count);
    }

    // -------------------------------------------------------------------------
    // Reliability
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithCompleteHtml1_ReliabilityIs90()
    {
        // 3bmeteo-complete1.html footer shows "90%"
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));

        Assert.NotEmpty(result.HoursDetails);
        Assert.Equal(90, result.ReliabilityPerc);
    }

    [Fact]
    public void ParseDayPage_WithNoReliabilityFooter_DefaultsTo100()
    {
        var scraper = CreateScraper();
        var html = """
            <html><body>
              <input type="radio" id="tab-orario" checked>
              <div id="content-orario" class="content-panel active">
                <ul class="fc-accordion-list">
                  <li class="fc-accordion-item" id="10">
                    <button>
                      <div class="fc-accordion-header">
                        <div class="to-left">
                          <span class="ds-body-medium ds-forecast-time">10</span>
                          <div class="fc-accordion-condition">
                            <span class="ds-body-medium">sereno</span>
                          </div>
                        </div>
                        <div class="to-right">
                          <span class="ds-heading-medium unit-temp" data-temp-c="18">18°</span>
                          <span class="ds-body-small unit-wind" data-wind-kmh="10" data-wind-dir="N">10 km/h N</span>
                        </div>
                      </div>
                    </button>
                    <div class="dropdown fc-accordion-details">
                      <div class="fc-accordion-summary-mobile ds-label-medium">Sereno</div>
                      <ul class="fc-accordion-grid">
                        <li data-param="precipitazioni"><span class="ds-body-small ds-text-secondary">Acc.</span><span class="ds-label-medium">0 mm</span></li>
                        <li data-param="umidita"><span class="ds-body-small ds-text-secondary">Umidità</span><span class="ds-label-medium">50%</span></li>
                        <li data-param="pressione"><span class="ds-body-small ds-text-secondary">Pressione</span><span class="ds-label-medium">1015 mb</span></li>
                      </ul>
                    </div>
                  </li>
                </ul>
              </div>
            </body></html>
            """;

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 14));

        Assert.NotEmpty(result.HoursDetails);
        Assert.Equal(100, result.ReliabilityPerc);
    }

    // -------------------------------------------------------------------------
    // Data correctness — 3bmeteo-v3-complete1.html (table layout) hour 00
    //   temp=10.7, precip=0mm, wind=6km/h NNE, humidity=75%, pressure=1024mb
    // -------------------------------------------------------------------------

    private static HoursWeatherDetails GetHourSlot(DayWeather day, int hour) =>
        day.HoursDetails.Single(h => h.TimeFrom.Hour == hour);

    [Fact]
    public void ParseDayPage_WithCompleteHtml_Hour00_TemperatureIs10point7()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));

        Assert.Equal(10.7, GetHourSlot(result, 0).TemperatureC);
    }

    [Fact]
    public void ParseDayPage_WithCompleteHtml_Hour00_PrecipitationIsZero()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));

        Assert.Equal(0.0, GetHourSlot(result, 0).PrecipitationMm);
    }

    [Fact]
    public void ParseDayPage_WithCompleteHtml_Hour00_WindIs6KmhNNE()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));
        var slot = GetHourSlot(result, 0);

        Assert.Equal(6, slot.WindKmh);
        Assert.Equal("NNE", slot.WindDirection);
    }

    [Fact]
    public void ParseDayPage_WithCompleteHtml_Hour00_HumidityIs75()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));

        Assert.Equal(75, GetHourSlot(result, 0).HumidityPerc);
    }

    [Fact]
    public void ParseDayPage_WithCompleteHtml_Hour00_PressureIs1024()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, Complete1Html, new DateOnly(2026, 5, 14));

        Assert.Equal(1024, GetHourSlot(result, 0).PressionMbar);
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithComplicatedHtmlFewerThanFourSlots_ReturnsEmpty()
    {
        var scraper = CreateScraper();
        var html = """
            <html><body>
              <input type="radio" id="tab-orario" disabled>
              <input type="radio" id="tab-esaorario" checked>
              <div id="content-esaorario" class="content-panel active">
                <ul class="fc-accordion-list">
                  <li class="fc-accordion-item" id="esa-0"></li>
                  <li class="fc-accordion-item" id="esa-1"></li>
                </ul>
              </div>
            </body></html>
            """;

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 16));

        Assert.Empty(result.HoursDetails);
    }

    [Fact]
    public void ParseDayPage_WithMissingContentOrario_ReturnsEmpty()
    {
        var scraper = CreateScraper();
        var html = "<html><body><input type=\"radio\" id=\"tab-orario\" checked></body></html>";

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 14));

        Assert.Empty(result.HoursDetails);
    }

    // -------------------------------------------------------------------------
    // MapWeatherType — singular/plural forms
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_DescriptionWithNubiSparseConPossibiliPiogge_MapsToLightRain()
    {
        var scraper = CreateScraper();
        var html = BuildMinimalHourlyHtml("10", "Nubi sparse con possibili piogge");

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 14));

        Assert.Equal(WeatherType.LightRain, result.HoursDetails[0].WeatherType);
    }

    [Fact]
    public void ParseDayPage_DescriptionWithPossibiliPioggeAlone_MapsToProbablyRainy()
    {
        var scraper = CreateScraper();
        var html = BuildMinimalHourlyHtml("10", "Possibili piogge");

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 14));

        Assert.Equal(WeatherType.ProbablyRainy, result.HoursDetails[0].WeatherType);
    }

    [Fact]
    public void ParseDayPage_DescriptionWithSerenoOPocoNuvoloso_MapsToSunny()
    {
        var scraper = CreateScraper();
        var html = BuildMinimalHourlyHtml("10", "Sereno o poco nuvoloso");

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 14));

        Assert.Equal(WeatherType.Sunny, result.HoursDetails[0].WeatherType);
    }

    [Fact]
    public void ParseDayPage_DescriptionWithPocoNuvolosoAlone_MapsToPartlyCloudy()
    {
        var scraper = CreateScraper();
        var html = BuildMinimalHourlyHtml("10", "Poco nuvoloso");

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 14));

        Assert.Equal(WeatherType.PartlyCloudy, result.HoursDetails[0].WeatherType);
    }

    [Fact]
    public void ParseDayPage_DescriptionWithTemporali_MapsToThunderstorm()
    {
        var scraper = CreateScraper();
        var html = BuildMinimalHourlyHtml("10", "Molto nuvoloso con piogge e temporali");

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 14));

        Assert.Equal(WeatherType.Thunderstorm, result.HoursDetails[0].WeatherType);
    }

    [Fact]
    public void ParseDayPage_DescriptionWithTemporale_MapsToThunderstorm()
    {
        var scraper = CreateScraper();
        var html = BuildMinimalHourlyHtml("10", "Temporale");

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 5, 14));

        Assert.Equal(WeatherType.Thunderstorm, result.HoursDetails[0].WeatherType);
    }

    // -------------------------------------------------------------------------
    // Precipitation probability
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_V3AccordionLayout_Hour00_PrecipitationProbabilityIsZero()
    {
        // 3bmeteo-v3-complete4.html: hour 00 has data-param="probabilita" = 0%
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, V3Complete4Html, new DateOnly(2026, 5, 14));

        Assert.Equal(0, GetHourSlot(result, 0).PrecipitationProbabilityPerc);
    }

    [Fact]
    public void ParseDayPage_TableLayout_SlotsHaveNullPrecipitationProbability()
    {
        // Table layout does not expose per-slot precipitation probability
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, V3Complete1Html, new DateOnly(2026, 5, 14));

        Assert.NotEmpty(result.HoursDetails);
        Assert.All(result.HoursDetails, h => Assert.Null(h.PrecipitationProbabilityPerc));
    }

    // -------------------------------------------------------------------------
    // v3 HTML — table layout (complete1) and accordion layout (complete4)
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_V3TableLayout_Returns24HourlyForecasts()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, V3Complete1Html, new DateOnly(2026, 5, 14));

        Assert.Equal(24, result.HoursDetails.Count);
    }

    [Fact]
    public void ParseDayPage_V3TableLayout_Hour00_TemperatureIsNonZero()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, V3Complete1Html, new DateOnly(2026, 5, 14));

        Assert.NotEqual(0, GetHourSlot(result, 0).TemperatureC);
    }

    [Fact]
    public void ParseDayPage_V3AccordionLayout_ReturnsHourlyForecasts()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, V3Complete4Html, new DateOnly(2026, 5, 14));

        Assert.NotEmpty(result.HoursDetails);
    }

    [Fact]
    public void ParseDayPage_V3AccordionLayout_Hour00_TemperatureIsNonZero()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, V3Complete4Html, new DateOnly(2026, 5, 14));

        Assert.NotEqual(0, GetHourSlot(result, 0).TemperatureC);
    }

    [Fact]
    public void ParseDayPage_V3TodayPage_ReturnsHourlyForecasts()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, V3TodayHtml, new DateOnly(2026, 5, 14));

        Assert.NotEmpty(result.HoursDetails);
    }

    private static string BuildMinimalHourlyHtml(string hour, string summaryDescription) => $"""
        <html><body>
          <input type="radio" id="tab-orario" checked>
          <div id="content-orario" class="content-panel active">
            <ul class="fc-accordion-list">
              <li class="fc-accordion-item" id="{hour}">
                <button>
                  <div class="fc-accordion-header">
                    <div class="to-left">
                      <span class="ds-body-medium ds-forecast-time">{hour}</span>
                      <div class="fc-accordion-condition">
                        <span class="ds-body-medium">desc</span>
                      </div>
                    </div>
                    <div class="to-right">
                      <span class="ds-heading-medium unit-temp" data-temp-c="15">15°</span>
                      <span class="ds-body-small unit-wind" data-wind-kmh="5" data-wind-dir="N">5 km/h N</span>
                    </div>
                  </div>
                </button>
                <div class="dropdown fc-accordion-details">
                  <div class="fc-accordion-summary-mobile ds-label-medium">{summaryDescription}</div>
                  <ul class="fc-accordion-grid">
                    <li data-param="precipitazioni"><span class="ds-body-small">Acc.</span><span class="ds-label-medium">0 mm</span></li>
                    <li data-param="umidita"><span class="ds-body-small">Umidità</span><span class="ds-label-medium">60%</span></li>
                    <li data-param="pressione"><span class="ds-body-small">Pressione</span><span class="ds-label-medium">1010 mb</span></li>
                  </ul>
                </div>
              </li>
            </ul>
          </div>
        </body></html>
        """;
}
