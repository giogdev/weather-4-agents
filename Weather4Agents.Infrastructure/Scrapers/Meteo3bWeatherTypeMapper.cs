using Microsoft.Extensions.Logging;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Infrastructure.Scrapers;

/// <summary>
/// Maps the Italian weather descriptions published by 3bmeteo.com to a <see cref="WeatherType"/>.
/// </summary>
public class Meteo3bWeatherTypeMapper
{
    private readonly ILogger<Meteo3bWeatherTypeMapper> _logger;

    public Meteo3bWeatherTypeMapper(ILogger<Meteo3bWeatherTypeMapper> logger)
    {
        _logger = logger;
    }

    // Order matters: more specific conditions must appear before generic ones.
    // - The "possibili piogge" rules must precede the generic Rainy rule, which
    //   also matches the plural "piogge" (e.g. "piogge diffuse").
    // - LightRain must precede PartlyCloudy because "nubi sparse con possibili
    //   piogge" contains "nubi sparse", which would otherwise match first.
    private static readonly (Func<string, bool> Matches, WeatherType WeatherType)[] WeatherMappings =
    [
        (d => d.Contains("temporal"),                                                                                                    WeatherType.Thunderstorm),
        (d => d.Contains("grandine"),                                                                                                    WeatherType.Hail),
        (d => d.Contains("neve abbondante") || d.Contains("bufera"),                                                                    WeatherType.HeavySnow),
        (d => d.Contains("nevischio") || d.Contains("pioggia mista a neve"),                                                           WeatherType.Sleet),
        (d => d.Contains("neve"),                                                                                                        WeatherType.Snowy),
        (d => d.Contains("pioggia forte") || d.Contains("acquazzone") || d.Contains("rovescio forte"),                                 WeatherType.HeavyRain),
        (d => d.Contains("possibili piogge") && (d.Contains("nubi sparse") || d.Contains("poco nuvoloso") || d.Contains("parz")),     WeatherType.LightRain),
        (d => d.Contains("possibili piogge"),                                                                                           WeatherType.ProbablyRainy),
        (d => d.Contains("pioviggine") || d.Contains("pioggia debole"),                                                                WeatherType.LightRain),
        (d => d.Contains("pioggia") || d.Contains("piogge") || d.Contains("rovescio") || d.Contains("rovesci"),                        WeatherType.Rainy),
        (d => d.Contains("nebbia") || d.Contains("foschia"),                                                                           WeatherType.Foggy),
        (d => d.Contains("coperto"),                                                                                                     WeatherType.Overcast),
        (d => d.Contains("sereno") && (d.Contains("poco nuvoloso") || d.Contains("parz")),                                            WeatherType.Sunny),
        (d => d.Contains("parz") || d.Contains("poco nuvoloso") || d.Contains("variabile") || d.Contains("nubi sparse"),              WeatherType.PartlyCloudy),
        (d => d.Contains("nuvoloso"),                                                                                                    WeatherType.Cloudy),
        (d => d.Contains("sereno") || d.Contains("soleggiato"),                                                                        WeatherType.Sunny),
        (d => d.Contains("vento forte"),                                                                                                 WeatherType.HeavyWindy),
        (d => d.Contains("velature"),                                                                                                    WeatherType.LightClouds),
    ];

    public WeatherType Map(string description)
    {
        var d = description.ToLowerInvariant();
        foreach (var (matches, weatherType) in WeatherMappings)
        {
            if (matches(d))
                return weatherType;
        }

        _logger.LogWarning("Unknown weather description from 3bMeteo: \"{Description}\"", description);
        return WeatherType.Unknown;
    }
}
