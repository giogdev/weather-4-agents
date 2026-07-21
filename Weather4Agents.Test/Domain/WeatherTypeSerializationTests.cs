using System.Text.Json;
using System.Text.Json.Serialization;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Test.Domain;

/// <summary>
/// Locks the wire format of <see cref="WeatherType"/> (ticket 14). The type became a real enum,
/// but the strings serialized to the API responses and the on-disk JSON files must stay
/// byte-identical to the string constants used before — consumers (Home Assistant, agents
/// reading the files) match on these verbatim.
/// </summary>
public class WeatherTypeSerializationTests
{
    // The exact strings every weather type has always serialized to. This is the snapshot: any
    // rename or casing change here is a breaking change for existing consumers.
    public static TheoryData<WeatherType, string> Snapshot => new()
    {
        { WeatherType.Unknown, "Unknown" },
        { WeatherType.Sunny, "Sunny" },
        { WeatherType.PartlyCloudy, "PartlyCloudy" },
        { WeatherType.Cloudy, "Cloudy" },
        { WeatherType.Overcast, "Overcast" },
        { WeatherType.Foggy, "Foggy" },
        { WeatherType.Rainy, "Rainy" },
        { WeatherType.HeavyRain, "HeavyRain" },
        { WeatherType.Thunderstorm, "Thunderstorm" },
        { WeatherType.Snowy, "Snowy" },
        { WeatherType.HeavySnow, "HeavySnow" },
        { WeatherType.Sleet, "Sleet" },
        { WeatherType.Hail, "Hail" },
        { WeatherType.Windy, "Windy" },
        { WeatherType.HeavyWindy, "HeavyWindy" },
        { WeatherType.ProbablyRainy, "ProbablyRainy" },
        { WeatherType.LightRain, "LightRain" },
        { WeatherType.LightClouds, "LightClouds" },
    };

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void Serializes_AsHistoricalString_WithApiDefaults(WeatherType type, string expected)
    {
        // The web defaults are what ASP.NET Core uses for controller responses: they register no
        // enum converter, so the string form comes solely from the type-level [JsonConverter].
        var json = JsonSerializer.Serialize(type, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal($"\"{expected}\"", json);
    }

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void Serializes_AsHistoricalString_UnderCamelCasePropertyNaming(WeatherType type, string expected)
    {
        // camelCase applies to property names, not to enum values: the weather type stays
        // PascalCase even when the surrounding object is camelCased (as in the file-storage job).
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var json = JsonSerializer.Serialize(type, options);

        Assert.Equal($"\"{expected}\"", json);
    }

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void RoundTrips_FromHistoricalString(WeatherType type, string serialized)
    {
        var parsed = JsonSerializer.Deserialize<WeatherType>(
            $"\"{serialized}\"", new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(type, parsed);
    }

    [Fact]
    public void DefaultValue_IsUnknown()
    {
        // HoursWeatherDetails relies on the enum default meaning "Unknown".
        Assert.Equal(WeatherType.Unknown, default(WeatherType));
    }
}
