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

        // Detect probabilistic page via div#complicata-right-container
        var isProbabilistic = doc.GetElementbyId("complicata-right-container") is not null;

        if (isProbabilistic)
        {
            dayWeather.HoursDetails = ParseProbabilisticSlots(doc);
        }
        else
        {
            var reliability = ParseReliability(doc);

            // Each weather hour row is wrapped in a div.row-table.noPad
            var rows = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'row-table') and contains(@class,'noPad')]");
            if (rows is not null)
            {
                foreach (var row in rows)
                {
                    var details = ParseHourRow(row, reliability);
                    if (details is not null)
                        dayWeather.HoursDetails.Add(details);
                }

                dayWeather.HoursDetails = dayWeather.HoursDetails.OrderBy(x => x.TimeFrom).ToList();
            }
        }

        return dayWeather;
    }

    private static int ParseReliability(HtmlDocument doc)
    {
        // Reliability is always inside a <small> within div.perc,
        // e.g. "90-95% - Molto alta" or "<50% - Molto bassa".
        // Using XPath with the accented char 'à' is unreliable in HtmlAgilityPack,
        // so we target the container class directly.
        var percSmall = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'perc')]//small");
        if (percSmall is null)
            return 100;

        var text = HtmlEntity.DeEntitize(percSmall.InnerText).Trim();

        var rangeMatch = ReliabilityRangeRegex().Match(text);
        if (rangeMatch.Success)
        {
            var lo = int.Parse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var hi = int.Parse(rangeMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            return (lo + hi) / 2;
        }

        var singleMatch = ReliabilitySingleRegex().Match(text);
        if (singleMatch.Success)
            return int.Parse(singleMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        return 100;
    }

    private static List<HoursWeatherDetails> ParseProbabilisticSlots(HtmlDocument doc)
    {
        const int reliability = 20; // probabilistic pages default reliability

        var esaContainer = doc.DocumentNode.SelectSingleNode(
            "//div[contains(@class,'table-previsioni-esa')]");
        if (esaContainer is null)
            return [];

        var slots = esaContainer.SelectNodes(
            ".//div[contains(@class,'row-table-xs') and contains(@class,'col-sm-1-5')]");
        if (slots is null || slots.Count < 4)
            return [];

        (TimeOnly From, TimeOnly To)[] slotDefs =
        [
            (new TimeOnly(0,  0), new TimeOnly(6,  0)),
            (new TimeOnly(6,  0), new TimeOnly(12, 0)),
            (new TimeOnly(12, 0), new TimeOnly(18, 0)),
            (new TimeOnly(18, 0), new TimeOnly(0,  0)),
        ];

        var results = new List<HoursWeatherDetails>();
        for (var i = 0; i < 4; i++)
        {
            var slot = slots[i];
            var (timeFrom, timeTo) = slotDefs[i];

            // Description: <small class="hidden-xs"> = full desktop text; fallback: hidden-sm
            // img alt is empty on probabilistic pages
            var descNode = slot.SelectSingleNode(".//small[contains(@class,'hidden-xs')]")
                        ?? slot.SelectSingleNode(".//small[contains(@class,'hidden-sm')]");
            var description = HtmlEntity.DeEntitize(descNode?.InnerText ?? string.Empty).Trim();

            var tempSpan = slot.SelectSingleNode(".//p[contains(@class,'switchcelsius')]");
            var tempC = ParseFirstDouble(tempSpan?.InnerText ?? string.Empty);

            var precipDiv = slot.SelectSingleNode(".//div[contains(@class,'altriDati-pioggia')]");
            var precipText = ExtractLabeledValue(precipDiv, "prec");
            var precipPerc = ParsePrecipitation(precipText);

            var windSpeedSpan = precipDiv?.SelectSingleNode(".//span[contains(@class,'switchkm')]");
            var windKmh = ParseFirstDouble(windSpeedSpan?.InnerText ?? string.Empty);

            // Wind direction is a text node inside the <p> containing "venti",
            // not a direct child of precipDiv — so we pass that <p> to ExtractWindDirection.
            var ventiP = precipDiv?.SelectNodes(".//p")
                ?.FirstOrDefault(p => HtmlEntity.DeEntitize(p.InnerText)
                    .Contains("venti", StringComparison.OrdinalIgnoreCase));
            var windDirection = ExtractWindDirection(ventiP);

            var humidityText = ExtractLabeledValue(precipDiv, "umid").Replace("%", "").Trim();
            int.TryParse(humidityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var humidity);

            var altroDiv = slot.SelectSingleNode(".//div[contains(@class,'altriDati-altro')]");
            var pressureText = ExtractLabeledValue(altroDiv, "press");
            var pressureMbar = (int)ParseFirstDouble(pressureText);

            results.Add(new HoursWeatherDetails
            {
                TimeFrom               = timeFrom,
                TimeTo                 = timeTo,
                WeatherType            = MapWeatherType(description),
                WeatherTypeDescription = description,
                TemperatureC           = tempC,
                PrecipitationMm        = precipPerc,
                HumidityPerc           = humidity,
                PressionMbar           = pressureMbar,
                WindKmh                = windKmh,
                WindDirection          = windDirection,
                ReliabilityPerc        = reliability,
            });
        }
        return results;
    }

    private static string ExtractLabeledValue(HtmlNode? div, string labelFragment)
    {
        if (div is null) return string.Empty;
        foreach (var p in div.SelectNodes(".//p") ?? Enumerable.Empty<HtmlNode>())
        {
            var text = HtmlEntity.DeEntitize(p.InnerText);
            if (text.Contains(labelFragment, StringComparison.OrdinalIgnoreCase))
            {
                var labelSpan = p.SelectSingleNode(".//span[@class='small']");
                var label = HtmlEntity.DeEntitize(labelSpan?.InnerText ?? string.Empty);
                return text.Replace(label, string.Empty).Trim();
            }
        }
        return string.Empty;
    }

    private static HoursWeatherDetails? ParseHourRow(HtmlNode outerRow, int reliability)
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

        // Precipitation: "assenti" hours use a span.gray child; rainy hours store the mm value
        // as direct text in the div (e.g. "1.1 mm"). We prefer span.gray when present,
        // otherwise fall back to the div's own direct text nodes.
        var precipDiv = rightPanel.SelectSingleNode(".//div[contains(@class,'altriDati-precipitazioni')]");
        var precipSpan = precipDiv?.SelectSingleNode(".//span[contains(@class,'gray')]");
        string precipText;
        if (precipSpan is not null)
        {
            precipText = HtmlEntity.DeEntitize(precipSpan.InnerText).Trim();
        }
        else
        {
            // Extract only direct text nodes from the div (skip child element text like img alt)
            precipText = string.Concat(
                precipDiv?.ChildNodes
                    .Where(n => n.NodeType == HtmlNodeType.Text)
                    .Select(n => HtmlEntity.DeEntitize(n.InnerText))
                ?? []).Trim();
        }
        var precipPerc = ParsePrecipitation(precipText);

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
            PrecipitationMm = precipPerc,
            HumidityPerc = humidity,
            PressionMbar = pressureMbar,
            WindKmh = windKmh,
            WindDirection = windDirection,
            ReliabilityPerc = reliability,
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

    private static double ParsePrecipitation(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower is "" or "-" or "assenti" or "0")
            return 0d;

        var match = NumberRegex().Match(text);
        if (!match.Success)
            return 0d;

        return double.Parse(match.Value, CultureInfo.InvariantCulture);
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
        // velature estese, velature sparse, velature lievi
        if (d.Contains("velature")) return WeatherType.LightClouds;

        return WeatherType.Unknown;
    }

    // Matches integers and decimals with dot or comma as separator
    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex NumberRegex();

    // Matches "X-Y%" reliability ranges (e.g. "90-95%")
    [GeneratedRegex(@"(\d+)-(\d+)%")]
    private static partial Regex ReliabilityRangeRegex();

    // Matches a single "X%" reliability value (e.g. "70%")
    [GeneratedRegex(@"(\d+)%")]
    private static partial Regex ReliabilitySingleRegex();
}
