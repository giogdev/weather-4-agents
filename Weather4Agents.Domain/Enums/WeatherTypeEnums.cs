using System.Text.Json.Serialization;

namespace Weather4Agents.Domain.Enums
{
    /// <summary>
    /// Represents the general weather condition for a given time range.
    /// </summary>
    /// <remarks>
    /// The type-level <see cref="JsonStringEnumConverter"/> makes every value serialize as its
    /// member name verbatim (e.g. <c>"PartlyCloudy"</c>), never as a number and never camel-cased.
    /// This keeps the JSON contract byte-identical to the string constants used before the enum
    /// existed, regardless of the serializer options at the call site, so existing consumers
    /// (Home Assistant, agents reading the JSON files) keep working unchanged. Do not rename or
    /// reorder members without treating it as a breaking change to that contract.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter<WeatherType>))]
    public enum WeatherType
    {
        // Unknown is first so default(WeatherType) means "Unknown", matching the previous default.
        Unknown,
        Sunny,
        PartlyCloudy,
        Cloudy,
        Overcast,
        Foggy,
        Rainy,
        HeavyRain,
        Thunderstorm,
        Snowy,
        HeavySnow,
        Sleet,
        Hail,
        Windy,
        HeavyWindy,
        ProbablyRainy,
        LightRain,
        LightClouds,
    }
}
