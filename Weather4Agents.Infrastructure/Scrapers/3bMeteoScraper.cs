using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Hybrid;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;
using Weather4Agents.Infrastructure.Scrapers.Base;

namespace Weather4Agents.Infrastructure.Scrapers;

/// <summary>
/// Scraping of 3bmeteo.com, an Italian weather website with detailed hourly forecasts
/// </summary>
public partial class Meteo3bScraper : BaseWeatherScraper
{
    private const string BaseUrl = "https://www.3bmeteo.com/meteo";

    public Meteo3bScraper(HttpClient httpClient, HybridCache hybridCache)
        : base(httpClient, hybridCache)
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
    }

    public override string ProviderName => "3bMeteo";

    protected override async Task<IEnumerable<DayWeather>> ScrapeAsync(string location, CancellationToken ct)
    {
        var results = new List<DayWeather>();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var normalizedLocation = location.ToLowerInvariant().Replace(' ', '-');

        // Day 0 = today, days 1–7 = subsequent days
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            ct.ThrowIfCancellationRequested();

            var url = dayOffset == 0
                ? $"{BaseUrl}/{normalizedLocation}"
                : $"{BaseUrl}/{normalizedLocation}/{dayOffset}";

            try
            {
                var html = await HttpClient.GetStringAsync(url, ct);
                var dayWeather = ParseDayPage(html, today.AddDays(dayOffset));
                if (dayWeather.HoursDetails.Count > 0)
                    results.Add(dayWeather);
            }
            catch (HttpRequestException)
            {
                // Skip days whose page fails to load
            }
        }

        return results;
    }

    private DayWeather ParseDayPage(string html, DateOnly date)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var dayWeather = new DayWeather
        {
            Date = date,
            Provider = new WeatherProvider(ProviderName)
        };

        // Complicated pages have the "Oraria" tab disabled — only 6-hour slots are available
        var isComplicated = IsComplicatedPage(doc);
        var reliability = isComplicated ? 20 : ParseReliability(doc);

        dayWeather.HoursDetails = isComplicated
            ? ParseEsaSlots(doc, reliability)
            : ParseHourlyItems(doc, reliability);

        return dayWeather;
    }

    private static bool IsComplicatedPage(HtmlDocument doc)
    {
        var tabOrario = doc.GetElementbyId("tab-orario");
        return tabOrario is not null && tabOrario.Attributes["disabled"] is not null;
    }

    private static int ParseReliability(HtmlDocument doc)
    {
        // Reliability is shown as "NN%" in a span inside the fc-accordion-footer card
        var span = doc.DocumentNode.SelectSingleNode(
            "//div[contains(@class,'fc-accordion-footer')]//span[contains(@class,'ds-label-medium')]");
        if (span is null)
            return 100;

        var match = ReliabilitySingleRegex().Match(HtmlEntity.DeEntitize(span.InnerText).Trim());
        return match.Success
            ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            : 100;
    }

    private static List<HoursWeatherDetails> ParseHourlyItems(HtmlDocument doc, int reliability)
    {
        var items = doc.DocumentNode.SelectNodes(
            "//div[@id='content-orario']//li[contains(@class,'fc-accordion-item')]");
        if (items is null)
            return [];

        var results = new List<HoursWeatherDetails>();
        foreach (var item in items)
        {
            var timeNode = item.SelectSingleNode(".//span[contains(@class,'ds-forecast-time')]");
            var timeText = HtmlEntity.DeEntitize(timeNode?.InnerText ?? string.Empty).Trim();
            if (!int.TryParse(timeText, out var hour) || hour is < 0 or > 23)
                continue;

            var details = ParseAccordionItem(item, new TimeOnly(hour, 0), new TimeOnly(hour, 0).AddHours(1), reliability);
            if (details is not null)
                results.Add(details);
        }

        return [.. results.OrderBy(x => x.TimeFrom)];
    }

    private static readonly (TimeOnly From, TimeOnly To)[] EsaSlotDefs =
    [
        (new TimeOnly(0,  0), new TimeOnly(6,  0)),   // Not. - Notte
        (new TimeOnly(6,  0), new TimeOnly(12, 0)),   // Mat. - Mattino
        (new TimeOnly(12, 0), new TimeOnly(18, 0)),   // Pom. - Pomeriggio
        (new TimeOnly(18, 0), new TimeOnly(0,  0)),   // Ser. - Sera
    ];

    private static List<HoursWeatherDetails> ParseEsaSlots(HtmlDocument doc, int reliability)
    {
        var items = doc.DocumentNode.SelectNodes(
            "//div[@id='content-esaorario']//li[contains(@class,'fc-accordion-item')]");
        if (items is null || items.Count < 4)
            return [];

        var results = new List<HoursWeatherDetails>();
        for (var i = 0; i < 4; i++)
        {
            var (timeFrom, timeTo) = EsaSlotDefs[i];
            var details = ParseAccordionItem(items[i], timeFrom, timeTo, reliability);
            if (details is not null)
                results.Add(details);
        }
        return results;
    }

    private static HoursWeatherDetails? ParseAccordionItem(HtmlNode item, TimeOnly timeFrom, TimeOnly timeTo, int reliability)
    {
        // Prefer the detailed summary text; fall back to the short header label
        var descNode = item.SelectSingleNode(".//div[contains(@class,'fc-accordion-summary-mobile')]")
                    ?? item.SelectSingleNode(".//div[contains(@class,'fc-accordion-condition')]//span[contains(@class,'ds-body-medium')]");
        var description = HtmlEntity.DeEntitize(descNode?.InnerText ?? string.Empty).Trim();

        // Temperature: data-temp-c attribute on span.unit-temp
        var tempSpan = item.SelectSingleNode(".//span[contains(@class,'unit-temp')]");
        double.TryParse(
            tempSpan?.GetAttributeValue("data-temp-c", "0").Trim(),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var tempC);

        // Wind: data-wind-kmh and data-wind-dir attributes on span.unit-wind
        var windSpan = item.SelectSingleNode(".//span[contains(@class,'unit-wind')]");
        double.TryParse(
            windSpan?.GetAttributeValue("data-wind-kmh", "0").Trim(),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var windKmh);
        var windDir = windSpan?.GetAttributeValue("data-wind-dir", string.Empty).Trim() ?? string.Empty;

        var precipMm = ParsePrecipitation(GetParamValue(item, "precipitazioni"));

        var humidityText = GetParamValue(item, "umidita").Replace("%", "").Trim();
        int.TryParse(humidityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var humidity);

        var pressureMbar = (int)ParseFirstDouble(GetParamValue(item, "pressione"));

        return new HoursWeatherDetails
        {
            TimeFrom               = timeFrom,
            TimeTo                 = timeTo,
            WeatherType            = MapWeatherType(description),
            WeatherTypeDescription = description,
            TemperatureC           = tempC,
            PrecipitationMm        = precipMm,
            HumidityPerc           = humidity,
            PressionMbar           = pressureMbar,
            WindKmh                = windKmh,
            WindDirection          = windDir,
            ReliabilityPerc        = reliability,
        };
    }

    private static string GetParamValue(HtmlNode item, string paramName)
    {
        var grid = item.SelectSingleNode(".//ul[contains(@class,'fc-accordion-grid')]");
        if (grid is null) return string.Empty;

        foreach (var li in grid.SelectNodes("li") ?? Enumerable.Empty<HtmlNode>())
        {
            if (li.GetAttributeValue("data-param", string.Empty) == paramName)
            {
                var valueSpan = li.SelectSingleNode(".//span[contains(@class,'ds-label-medium')]");
                return HtmlEntity.DeEntitize(valueSpan?.InnerText ?? string.Empty).Trim();
            }
        }
        return string.Empty;
    }

    private static double ParsePrecipitation(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower is "" or "-" or "assenti" or "0")
            return 0d;

        var match = NumberRegex().Match(text);
        return match.Success
            ? double.Parse(match.Value, CultureInfo.InvariantCulture)
            : 0d;
    }

    private static double ParseFirstDouble(string text)
    {
        var match = NumberRegex().Match(HtmlEntity.DeEntitize(text));
        return match.Success
            ? double.Parse(match.Value, CultureInfo.InvariantCulture)
            : 0d;
    }

    // Order matters: more specific conditions must appear before generic ones.
    // LightRain must precede PartlyCloudy because "nubi sparse con possibili piogge"
    // contains "nubi sparse" which would otherwise match PartlyCloudy first.
    private static readonly (Func<string, bool> Matches, string WeatherType)[] WeatherMappings =
    [
        (d => d.Contains("temporal"),                                                                                                    WeatherType.Thunderstorm),
        (d => d.Contains("grandine"),                                                                                                    WeatherType.Hail),
        (d => d.Contains("neve abbondante") || d.Contains("bufera"),                                                                    WeatherType.HeavySnow),
        (d => d.Contains("nevischio") || d.Contains("pioggia mista a neve"),                                                           WeatherType.Sleet),
        (d => d.Contains("neve"),                                                                                                        WeatherType.Snowy),
        (d => d.Contains("pioggia forte") || d.Contains("acquazzone") || d.Contains("rovescio forte"),                                 WeatherType.HeavyRain),
        (d => d.Contains("pioggia") || d.Contains("rovescio") || d.Contains("rovesci") || d.Contains("pioggere"),                     WeatherType.Rainy),
        (d => d.Contains("nebbia") || d.Contains("foschia"),                                                                           WeatherType.Foggy),
        (d => d.Contains("coperto"),                                                                                                     WeatherType.Overcast),
        (d => d.Contains("possibili piogge") && (d.Contains("nubi sparse") || d.Contains("poco nuvoloso") || d.Contains("parz")),     WeatherType.LightRain),
        (d => d.Contains("possibili piogge"),                                                                                           WeatherType.ProbablyRainy),
        (d => d.Contains("sereno") && (d.Contains("poco nuvoloso") || d.Contains("parz")),                                            WeatherType.Sunny),
        (d => d.Contains("parz") || d.Contains("poco nuvoloso") || d.Contains("variabile") || d.Contains("nubi sparse"),              WeatherType.PartlyCloudy),
        (d => d.Contains("nuvoloso"),                                                                                                    WeatherType.Cloudy),
        (d => d.Contains("sereno") || d.Contains("soleggiato"),                                                                        WeatherType.Sunny),
        (d => d.Contains("vento forte"),                                                                                                 WeatherType.HeavyWindy),
        (d => d.Contains("velature"),                                                                                                    WeatherType.LightClouds),
    ];

    private static string MapWeatherType(string description)
    {
        var d = description.ToLowerInvariant();
        foreach (var (matches, weatherType) in WeatherMappings)
        {
            if (matches(d))
                return weatherType;
        }
        return WeatherType.Unknown;
    }

    // Matches integers and decimals with dot or comma as separator
    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex NumberRegex();

    // Matches a single "X%" reliability value (e.g. "90%")
    [GeneratedRegex(@"(\d+)%")]
    private static partial Regex ReliabilitySingleRegex();
}
