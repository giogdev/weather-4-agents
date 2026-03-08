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

        // Each weather hour row is wrapped in a div.row-table.noPad
        var rows = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'row-table') and contains(@class,'noPad')]");
        if (rows is null)
            return dayWeather;

        foreach (var row in rows)
        {
            var details = ParseHourRow(row);
            if (details is not null)
                dayWeather.HoursDetails.Add(details);
        }

        //Order by hours
        dayWeather.HoursDetails = dayWeather.HoursDetails.OrderBy(x => x.TimeFrom).ToList();

        return dayWeather;
    }

    private static HoursWeatherDetails? ParseHourRow(HtmlNode outerRow)
    {
        // Left panel: time + icon + description live inside div.row-table.special_campaign
        var specialCampaign = outerRow.SelectSingleNode(
            ".//div[contains(@class,'row-table')]");
        if (specialCampaign is null)
            return null;

        // Hour number is in the first "big zoom_prv" div (e.g. "14<span>:00</span>")
        var hourDiv = specialCampaign.SelectSingleNode(
            ".//div[contains(@class,'big')]");
        if (hourDiv is null)
            return null;

        var hourMatch = NumberRegex().Match(HtmlEntity.DeEntitize(hourDiv.InnerText));
        if (!hourMatch.Success || !int.TryParse(hourMatch.Value, out var hour) || hour is < 0 or > 23)
            return null;

        var timeFrom = new TimeOnly(hour, 0);

        // Description: prefer img alt, fallback to text in col-xs-2-4 div
        var img = specialCampaign.SelectSingleNode(".//img");
        var description = img?.GetAttributeValue("alt", string.Empty).Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            var descDiv = specialCampaign.SelectSingleNode(".//div[contains(@class,'col-xs-2-4')]");
            description = HtmlEntity.DeEntitize(descDiv?.InnerText ?? string.Empty).Trim();
        }

        // Right panel: temperature, precipitation, wind, humidity, pressure (div.col-sm-3-5)
        var rightPanel = outerRow.SelectSingleNode(".//div[contains(@class,'col-sm-3-5')]");
        if (rightPanel is null)
            return null;

        // Temperature (Celsius): in the "big" div, first span.switchcelsius
        var tempSpan = rightPanel.SelectSingleNode(
            ".//div[contains(@class,'big')]//span[contains(@class,'switchcelsius')]");
        var tempC = ParseFirstDouble(tempSpan?.InnerText ?? string.Empty);

        // Precipitation: span.gray inside altriDati-precipitazioni
        var precipSpan = rightPanel.SelectSingleNode(
            ".//div[contains(@class,'altriDati-precipitazioni')]//span[contains(@class,'gray')]");
        var precipPerc = ParsePrecipitation(HtmlEntity.DeEntitize(precipSpan?.InnerText ?? string.Empty).Trim());

        // Wind speed (km/h): span.switchkm inside altriDati-venti
        var windSpeedSpan = rightPanel.SelectSingleNode(
            ".//div[contains(@class,'altriDati-venti')]//span[contains(@class,'switchkm')]");
        var windKmh = ParseFirstDouble(windSpeedSpan?.InnerText ?? string.Empty);

        // Wind direction: direct text node after the spans in altriDati-venti
        var ventiDiv = rightPanel.SelectSingleNode(".//div[contains(@class,'altriDati-venti')]");
        var windDirection = ExtractWindDirection(ventiDiv);

        // Humidity: altriDati-umidita
        var humidityDiv = rightPanel.SelectSingleNode(".//div[contains(@class,'altriDati-umidita')]");
        var humidityText = HtmlEntity.DeEntitize(humidityDiv?.InnerText ?? string.Empty).Replace("%", "").Trim();
        int.TryParse(humidityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var humidity);

        // Pressure (mbar): altriDati-pressione
        var pressureDiv = rightPanel.SelectSingleNode(".//div[contains(@class,'altriDati-pressione')]");
        var pressureMbar = (int)ParseFirstDouble(pressureDiv?.InnerText ?? string.Empty);

        return new HoursWeatherDetails
        {
            TimeFrom = timeFrom,
            TimeTo = timeFrom.AddHours(1),
            WeatherType = MapWeatherType(description),
            WeatherTypeDescription = description,
            TemperatureC = tempC,
            PrecipitationsPerc = precipPerc,
            HumidityPerc = humidity,
            PressionMbar = pressureMbar,
            WindKmh = windKmh,
            WindDirection = windDirection
        };
    }

    private static string ExtractWindDirection(HtmlNode? ventiDiv)
    {
        if (ventiDiv is null)
            return string.Empty;

        // Wind direction is a direct text node in the div, appearing after the speed/knots spans
        return ventiDiv.ChildNodes
            .Where(n => n.NodeType == HtmlNodeType.Text)
            .Select(n => HtmlEntity.DeEntitize(n.InnerText).Trim())
            .LastOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty;
    }

    private static int ParsePrecipitation(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower is "" or "-" or "assenti" or "0")
            return 0;

        var match = NumberRegex().Match(text);
        if (!match.Success)
            return 0;

        return (int)double.Parse(match.Value, CultureInfo.InvariantCulture);
    }

    private static double ParseFirstDouble(string text)
    {
        var match = NumberRegex().Match(HtmlEntity.DeEntitize(text));
        return match.Success
            ? double.Parse(match.Value, CultureInfo.InvariantCulture)
            : 0d;
    }

    private static string MapWeatherType(string description)
    {
        var d = description.ToLowerInvariant();

        // Order matters: more specific conditions before generic ones
        if (d.Contains("temporale")) return WeatherType.Thunderstorm;
        if (d.Contains("grandine")) return WeatherType.Hail;
        if (d.Contains("neve abbondante") || d.Contains("bufera")) return WeatherType.HeavySnow;
        if (d.Contains("nevischio") || d.Contains("pioggia mista a neve")) return WeatherType.Sleet;
        if (d.Contains("neve")) return WeatherType.Snowy;
        if (d.Contains("pioggia forte") || d.Contains("acquazzone") || d.Contains("rovescio forte")) return WeatherType.HeavyRain;
        if (d.Contains("pioggia") || d.Contains("rovescio") || d.Contains("pioggere")) return WeatherType.Rainy;
        if (d.Contains("nebbia") || d.Contains("foschia")) return WeatherType.Foggy;
        if (d.Contains("coperto")) return WeatherType.Overcast;
        if (d.Contains("parz") || d.Contains("parz nuvoloso") || d.Contains("poco nuvoloso") || d.Contains("variabile") || d.Contains("nubi sparse")) return WeatherType.PartlyCloudy;
        if (d.Contains("nuvoloso")) return WeatherType.Cloudy;
        if (d.Contains("sereno") || d.Contains("soleggiato")) return WeatherType.Sunny;
        if (d.Contains("vento forte")) return WeatherType.HeavyWindy;
        if (d.Contains("possibili piogge")) return WeatherType.ProbablyRainy;

        return WeatherType.Unknown;
    }

    // Matches integers and decimals with dot or comma as separator
    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex NumberRegex();
}
