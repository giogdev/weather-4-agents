using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Infrastructure.Integrations.HomeAssistant;

/// <summary>
/// Maps Weather4Agents domain models to Home Assistant MQTT Discovery payloads.
/// </summary>
internal static class HomeAssistantWeatherMapper
{
    /// <summary>
    /// Builds the MQTT Discovery config payload that registers a native <c>weather</c> entity in HA.
    /// </summary>
    public static object BuildDiscoveryPayload(string location, string discoveryPrefix)
    {
        var nodeId = BuildNodeId(location);
        var stateTopic = BuildStateTopic(discoveryPrefix, nodeId);
        var forecastDailyTopic = BuildForecastDailyTopic(discoveryPrefix, nodeId);

        return new
        {
            name = $"Weather4Agents {location}",
            unique_id = nodeId,
            state_topic = stateTopic,
            forecast_daily_topic = forecastDailyTopic,
            temperature_unit = "C",
            wind_speed_unit = "km/h",
            pressure_unit = "hPa",
            precipitation_unit = "mm",
            attribution = $"Weather4Agents / {location}"
        };
    }

    /// <summary>
    /// Builds the state payload with current weather conditions derived from the first time slot of today.
    /// </summary>
    public static object BuildStatePayload(IEnumerable<DayWeather> forecast)
    {
        var today = forecast.FirstOrDefault();
        var currentSlot = today?.HoursDetails.FirstOrDefault();

        return new
        {
            condition = MapCondition(currentSlot?.WeatherType),
            temperature = currentSlot?.TemperatureC ?? 0,
            humidity = currentSlot?.HumidityPerc ?? 0,
            pressure = currentSlot?.PressionMbar ?? 0,
            wind_speed = currentSlot?.WindKmh ?? 0,
            wind_bearing = MapWindBearing(currentSlot?.WindDirection)
        };
    }

    /// <summary>
    /// Builds the daily forecast payload for the HA forecast card.
    /// Each entry represents one day using the median time slot (or the first available).
    /// </summary>
    public static object BuildForecastDailyPayload(IEnumerable<DayWeather> forecast)
    {
        var entries = forecast.Select(day =>
        {
            var representative = PickRepresentativeSlot(day.HoursDetails);
            var tempHigh = day.HoursDetails.Any() ? day.HoursDetails.Max(h => h.TemperatureC) : 0;
            var tempLow = day.HoursDetails.Any() ? day.HoursDetails.Min(h => h.TemperatureC) : 0;
            var totalPrecip = day.HoursDetails.Sum(h => h.PrecipitationMm);

            return new
            {
                datetime = day.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToString("yyyy-MM-ddTHH:mm:sszzz"),
                condition = MapCondition(representative?.WeatherType),
                temperature = tempHigh,
                templow = tempLow,
                precipitation = totalPrecip,
                wind_speed = representative?.WindKmh ?? 0,
                wind_bearing = MapWindBearing(representative?.WindDirection)
            };
        });

        return new { forecast = entries };
    }

    public static string BuildNodeId(string location) =>
        $"weather4agents_{location.ToLowerInvariant().Replace(" ", "_")}";

    public static string BuildStateTopic(string discoveryPrefix, string nodeId) =>
        $"{discoveryPrefix}/weather/{nodeId}/state";

    public static string BuildForecastDailyTopic(string discoveryPrefix, string nodeId) =>
        $"{discoveryPrefix}/weather/{nodeId}/forecast/daily";

    public static string BuildDiscoveryTopic(string discoveryPrefix, string nodeId) =>
        $"{discoveryPrefix}/weather/{nodeId}/config";

    /// <summary>
    /// Maps a WeatherType constant to a Home Assistant weather condition string.
    /// </summary>
    private static string MapCondition(string? weatherType) => weatherType switch
    {
        WeatherType.Sunny => "sunny",
        WeatherType.PartlyCloudy => "partlycloudy",
        WeatherType.LightClouds => "partlycloudy",
        WeatherType.Cloudy => "cloudy",
        WeatherType.Overcast => "cloudy",
        WeatherType.Foggy => "fog",
        WeatherType.Rainy => "rainy",
        WeatherType.ProbablyRainy => "rainy",
        WeatherType.HeavyRain => "pouring",
        WeatherType.Thunderstorm => "lightning-rainy",
        WeatherType.Snowy => "snowy",
        WeatherType.HeavySnow => "snowy",
        WeatherType.Sleet => "snowy-rainy",
        WeatherType.Hail => "hail",
        WeatherType.Windy => "windy",
        WeatherType.HeavyWindy => "windy-variant",
        _ => "exceptional"
    };

    /// <summary>
    /// Converts a cardinal wind direction string to degrees (0–360).
    /// Returns null if the direction is unknown or empty.
    /// </summary>
    private static int? MapWindBearing(string? direction) => direction?.ToUpperInvariant() switch
    {
        "N" => 0,
        "NNE" => 22,
        "NE" => 45,
        "ENE" => 67,
        "E" => 90,
        "ESE" => 112,
        "SE" => 135,
        "SSE" => 157,
        "S" => 180,
        "SSW" => 202,
        "SW" => 225,
        "WSW" => 247,
        "W" => 270,
        "WNW" => 292,
        "NW" => 315,
        "NNW" => 337,
        _ => null
    };

    /// <summary>
    /// Picks the most representative slot for a day (midday slot, or first available).
    /// </summary>
    private static HoursWeatherDetails? PickRepresentativeSlot(IEnumerable<HoursWeatherDetails> slots)
    {
        var list = slots.ToList();
        if (list.Count == 0) return null;

        // Prefer a slot that covers midday (12:00)
        var midday = new TimeOnly(12, 0);
        return list.FirstOrDefault(h => h.TimeFrom <= midday && h.TimeTo > midday)
               ?? list[list.Count / 2];
    }
}
