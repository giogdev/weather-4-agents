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

    private static string ProbabilisticHtml =>
        File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-probabilistic-12.html"));

    // -------------------------------------------------------------------------
    // Non-regression: existing hourly pages must continue to work
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithSampleHtml_Returns13HourlyForecasts()
    {
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-13.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));

        Assert.Equal(13, result.HoursDetails.Count);
    }

    [Fact]
    public void ParseDayPage_WithSampleHtml_Returns24HourlyForecasts()
    {
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-24.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));

        Assert.Equal(24, result.HoursDetails.Count);
    }

    [Fact]
    public void ParseDayPage_WithStandardHtml_DoesNotReturnFourSlots()
    {
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-13.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));

        Assert.NotEqual(4, result.HoursDetails.Count);
    }

    // -------------------------------------------------------------------------
    // Probabilistic page detection and slot parsing
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_ReturnsFourSlots()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.Equal(4, result.HoursDetails.Count);
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_SlotsHaveCorrectTimeRanges()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.Equal(new TimeOnly(0,  0), result.HoursDetails[0].TimeFrom);
        Assert.Equal(new TimeOnly(6,  0), result.HoursDetails[0].TimeTo);
        Assert.Equal(new TimeOnly(6,  0), result.HoursDetails[1].TimeFrom);
        Assert.Equal(new TimeOnly(12, 0), result.HoursDetails[1].TimeTo);
        Assert.Equal(new TimeOnly(12, 0), result.HoursDetails[2].TimeFrom);
        Assert.Equal(new TimeOnly(18, 0), result.HoursDetails[2].TimeTo);
        Assert.Equal(new TimeOnly(18, 0), result.HoursDetails[3].TimeFrom);
        Assert.Equal(new TimeOnly(0,  0), result.HoursDetails[3].TimeTo);
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_SlotsAreOrderedByTimeFrom()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        var times = result.HoursDetails.Select(h => h.TimeFrom).ToList();
        Assert.Equal([.. times.OrderBy(t => t)], times);
    }

    // -------------------------------------------------------------------------
    // ReliabilityPerc
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_AllSlotsHaveReliabilityTwenty()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.All(result.HoursDetails, h => Assert.Equal(20, h.ReliabilityPerc));
    }

    [Fact]
    public void ParseDayPage_WithStandardHtml_ReliabilityParsedFromAttendibilitaRange()
    {
        var scraper = CreateScraper();
        // Minimal HTML replicating the real div.perc > small structure used by 3bmeteo
        var html = """
            <html><body>
              <div class="perc pull-right">Attendibilità <small>90-95% - Molto alta</small></div>
              <div class="row-table noPad">
                <div class="row-table special_campaign">
                  <div class="big">10<span>:00</span></div>
                  <img alt="Sereno" />
                </div>
                <div class="col-sm-3-5">
                  <div class="big"><span class="switchcelsius">18</span></div>
                  <div class="altriDati-precipitazioni"><span class="gray">assenti</span></div>
                  <div class="altriDati-venti"><span class="switchkm">10 km/h</span> N</div>
                  <div class="altriDati-umidita">50%</div>
                  <div class="altriDati-pressione">1015mbar</div>
                </div>
              </div>
            </body></html>
            """;

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 3, 20));

        Assert.NotEmpty(result.HoursDetails);
        Assert.All(result.HoursDetails, h => Assert.Equal(92, h.ReliabilityPerc));
    }

    [Fact]
    public void ParseDayPage_WithStandardHtml_ReliabilityParsedFromAttendibilitaSingleValue()
    {
        var scraper = CreateScraper();
        var html = """
            <html><body>
              <div class="perc pull-right">Attendibilità <small>70% - Alta</small></div>
              <div class="row-table noPad">
                <div class="row-table special_campaign">
                  <div class="big">10<span>:00</span></div>
                  <img alt="Sereno" />
                </div>
                <div class="col-sm-3-5">
                  <div class="big"><span class="switchcelsius">18</span></div>
                  <div class="altriDati-precipitazioni"><span class="gray">assenti</span></div>
                  <div class="altriDati-venti"><span class="switchkm">10 km/h</span> N</div>
                  <div class="altriDati-umidita">50%</div>
                  <div class="altriDati-pressione">1015mbar</div>
                </div>
              </div>
            </body></html>
            """;

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 3, 20));

        Assert.NotEmpty(result.HoursDetails);
        Assert.All(result.HoursDetails, h => Assert.Equal(70, h.ReliabilityPerc));
    }

    [Fact]
    public void ParseDayPage_WithHtmlWithoutPercDiv_ReliabilityDefaultsTo100()
    {
        var scraper = CreateScraper();
        // Minimal hourly HTML with no div.perc element — reliability must default to 100
        var html = """
            <html><body>
              <div class="row-table noPad">
                <div class="row-table special_campaign">
                  <div class="big">10<span>:00</span></div>
                  <img alt="Sereno" />
                </div>
                <div class="col-sm-3-5">
                  <div class="big"><span class="switchcelsius">18</span></div>
                  <div class="altriDati-precipitazioni"><span class="gray">assenti</span></div>
                  <div class="altriDati-venti"><span class="switchkm">10 km/h</span> N</div>
                  <div class="altriDati-umidita">50%</div>
                  <div class="altriDati-pressione">1015mbar</div>
                </div>
              </div>
            </body></html>
            """;

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 3, 20));

        Assert.NotEmpty(result.HoursDetails);
        Assert.All(result.HoursDetails, h => Assert.Equal(100, h.ReliabilityPerc));
    }

    // -------------------------------------------------------------------------
    // ReliabilityPerc extracted from real HTML files
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithStandardHtml13_ReliabilityIs92()
    {
        // 3bmeteo-response-13.html has "90-95% - Molto alta" → average = 92
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-13.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));

        Assert.NotEmpty(result.HoursDetails);
        Assert.All(result.HoursDetails, h => Assert.Equal(92, h.ReliabilityPerc));
    }

    [Fact]
    public void ParseDayPage_WithStandardHtml24_ReliabilityIs92()
    {
        // 3bmeteo-response-24.html has "90-95% - Molto alta" → average = 92
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-24.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));

        Assert.NotEmpty(result.HoursDetails);
        Assert.All(result.HoursDetails, h => Assert.Equal(92, h.ReliabilityPerc));
    }

    [Fact]
    public void ParseDayPage_WithStandardHtml14_ReliabilityIs50()
    {
        // 3bmeteo-response-14.html has "<50% - Molto bassa" → single match extracts 50
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-14.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));

        Assert.NotEmpty(result.HoursDetails);
        Assert.All(result.HoursDetails, h => Assert.Equal(50, h.ReliabilityPerc));
    }

    // -------------------------------------------------------------------------
    // Precipitation values from 3bmeteo-response-14.html
    //
    // PrecipitationMm stores the raw mm value as double:
    //   01:00 → 1.1 mm → 1.1
    //   05:00 → 0.7 mm → 0.7
    //   09:00 → 0.2 mm → 0.2
    //   14:00 → assenti  → 0.0
    // -------------------------------------------------------------------------

    private static HoursWeatherDetails GetHourSlot(DayWeather day, int hour) =>
        day.HoursDetails.Single(h => h.TimeFrom.Hour == hour);

    [Fact]
    public void ParseDayPage_WithStandardHtml14_AtHour01_PrecipitationIs1()
    {
        // 01:00 has "1.1 mm" in the div → ParsePrecipitation extracts 1.1
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-14.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));
        var slot = GetHourSlot(result, 1);

        Assert.Equal(1.1, slot.PrecipitationMm);
    }

    [Fact]
    public void ParseDayPage_WithStandardHtml14_AtHour05_PrecipitationIsZero()
    {
        // 05:00 has "0.7 mm" in the div → ParsePrecipitation extracts 0.7
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-14.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));
        var slot = GetHourSlot(result, 5);

        Assert.Equal(0.7, slot.PrecipitationMm);
    }

    [Fact]
    public void ParseDayPage_WithStandardHtml14_AtHour09_PrecipitationIsZero()
    {
        // 09:00 has "0.2 mm" → ParsePrecipitation extracts 0.2
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-14.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));
        var slot = GetHourSlot(result, 9);

        Assert.Equal(0.2, slot.PrecipitationMm);
    }

    [Fact]
    public void ParseDayPage_WithStandardHtml14_AtHour14_PrecipitationIsZero()
    {
        // 14:00 has "assenti" (span.gray) → 0.0
        var scraper = CreateScraper();
        var html = File.ReadAllText(Path.Combine("ProviderExamples", "3bmeteo-response-14.html"));

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 2, 26));
        var slot = GetHourSlot(result, 14);

        Assert.Equal(0.0, slot.PrecipitationMm);
    }

    // -------------------------------------------------------------------------
    // Data correctness of the 4 slots (values from 3bmeteo-response-probabilistic-12.html)
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_NotteSlotHasCorrectTemperature()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.Equal(9, result.HoursDetails[0].TemperatureC);
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_PomeriggioSlotHasCorrectTemperature()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.Equal(15, result.HoursDetails[2].TemperatureC);
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_NotteSlotHasCorrectWind()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.Equal(7, result.HoursDetails[0].WindKmh);
        Assert.Equal("NNE", result.HoursDetails[0].WindDirection);
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_NotteSlotHasCorrectHumidity()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.Equal(95, result.HoursDetails[0].HumidityPerc);
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_NotteSlotHasCorrectPressure()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.Equal(1024, result.HoursDetails[0].PressionMbar);
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_DescriptionIsNotEmpty()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.All(result.HoursDetails, h => Assert.NotEmpty(h.WeatherTypeDescription));
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtml_WeatherTypeIsMapped()
    {
        var scraper = CreateScraper();

        var result = InvokeParseDayPage(scraper, ProbabilisticHtml, new DateOnly(2026, 3, 20));

        Assert.All(result.HoursDetails, h => Assert.NotEqual(WeatherType.Unknown, h.WeatherType));
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDayPage_WithProbabilisticHtmlMissingEsaContainer_ReturnsEmpty()
    {
        var scraper = CreateScraper();
        // Has the probabilistic marker but no table-previsioni-esa container
        var html = """
            <html><body>
              <div id="complicata-right-container"><span>La previsione è complicata.</span></div>
            </body></html>
            """;

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 3, 20));

        Assert.Empty(result.HoursDetails);
    }

    [Fact]
    public void ParseDayPage_WithProbabilisticHtmlFewerThanFourSlots_ReturnsEmpty()
    {
        var scraper = CreateScraper();
        // Has the probabilistic marker and the container but only 2 slots
        var html = """
            <html><body>
              <div id="complicata-right-container"><span>La previsione è complicata.</span></div>
              <div class="table-previsioni-esa">
                <div class="row-table-xs col-sm-1-5 vtop"><!-- slot 1 --></div>
                <div class="row-table-xs col-sm-1-5 vtop"><!-- slot 2 --></div>
              </div>
            </body></html>
            """;

        var result = InvokeParseDayPage(scraper, html, new DateOnly(2026, 3, 20));

        Assert.Empty(result.HoursDetails);
    }
}
