using System.Net;
using System.Text.Json.Nodes;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Test.Integration;

/// <summary>
/// Timezone correctness at the HTTP seam: with the clock pinned via <see cref="FakeTimeProvider"/>
/// and the host running in UTC, "today" and the next-24h window must be computed on the weather
/// provider's timezone (Europe/Rome), and responses must declare that timezone explicitly.
///
/// May dates are used on purpose: Italy is on CEST (UTC+2) then, so the Italian civil date flips
/// two hours before the UTC one and any UTC-based "today" gives a different answer.
/// </summary>
public class TimezoneAndClockTests
{
    /// <summary>2026-05-14 22:30 UTC = 2026-05-15 00:30 in Italy — just past Italian midnight.</summary>
    private static readonly DateTimeOffset JustPastItalianMidnight =
        new(2026, 5, 14, 22, 30, 0, TimeSpan.Zero);

    private static DayWeather Day(DateOnly date, params HoursWeatherDetails[] slots) => new()
    {
        Date = date,
        Provider = new WeatherProvider(FakeWeatherProviderScraper.Name),
        ReliabilityPerc = 85,
        HoursDetails = [.. slots]
    };

    private static HoursWeatherDetails Slot(TimeOnly from, TimeOnly to) => new()
    {
        TimeFrom = from,
        TimeTo = to,
        WeatherType = WeatherType.Sunny,
        WeatherTypeDescription = "Sereno",
        TemperatureC = 20,
        HumidityPerc = 50,
        PressionMbar = 1015,
        WindKmh = 5,
        WindDirection = "N"
    };

    [Fact]
    public async Task GetWeekForecast_JustPastItalianMidnightInUtcHost_ComputesTodayOnItalianTime()
    {
        await using var factory = new Weather4AgentsApiFactory();
        factory.Clock.SetUtcNow(JustPastItalianMidnight);
        factory.Scraper.SetForecast(
            "bergamo",
            Day(new DateOnly(2026, 5, 14), Slot(new TimeOnly(8, 0), new TimeOnly(9, 0))),
            Day(new DateOnly(2026, 5, 15), Slot(new TimeOnly(8, 0), new TimeOnly(9, 0))));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/week");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        // In Italy it is already May 15: May 14 is a past day and must not be returned,
        // even though in UTC (the host timezone) it is still May 14.
        var days = body["forecast"]!.AsArray();
        var day = Assert.Single(days);
        Assert.Equal("2026-05-15", (string?)day!["date"]);

        // Consumers need the timezone to interpret the local dates/times in the payload.
        Assert.Equal("Europe/Rome", (string?)body["timezone"]);
    }

    [Fact]
    public async Task GetNext24Hours_At2330ItalianTime_ComputesTheWindowOnItalianTimeAcrossMidnight()
    {
        // 2026-05-14 21:30 UTC = 23:30 in Italy: the 24h window is [May 14 23:30, May 15 23:30) Italian time.
        var lateItalianEvening = new DateTimeOffset(2026, 5, 14, 21, 30, 0, TimeSpan.Zero);

        await using var factory = new Weather4AgentsApiFactory();
        factory.Clock.SetUtcNow(lateItalianEvening);
        factory.Scraper.SetForecast(
            "bergamo",
            Day(new DateOnly(2026, 5, 14),
                // Already over at 23:30 → excluded.
                Slot(new TimeOnly(14, 0), new TimeOnly(15, 0)),
                // Evening slot ending at midnight (TimeTo 00:00 rolls to May 15): still in
                // progress at 23:30 → included.
                Slot(new TimeOnly(18, 0), new TimeOnly(0, 0))),
            Day(new DateOnly(2026, 5, 15),
                // Starts inside the window (May 15 08:00 < May 15 23:30) → included.
                Slot(new TimeOnly(8, 0), new TimeOnly(9, 0))),
            Day(new DateOnly(2026, 5, 16),
                // Starts after the window ends → excluded.
                Slot(new TimeOnly(8, 0), new TimeOnly(9, 0))));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/weather/bergamo/forecast/next-24h");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        var hours = body["hours"]!.AsArray();
        Assert.Equal(2, hours.Count);
        Assert.Equal("2026-05-14", (string?)hours[0]!["date"]);
        Assert.Equal("18:00:00", (string?)hours[0]!["details"]!["timeFrom"]);
        Assert.Equal("2026-05-15", (string?)hours[1]!["date"]);
        Assert.Equal("08:00:00", (string?)hours[1]!["details"]!["timeFrom"]);

        Assert.Equal("Europe/Rome", (string?)body["timezone"]);
        Assert.Equal(lateItalianEvening, (DateTimeOffset?)body["lastUpdatedAt"]);
    }
}
