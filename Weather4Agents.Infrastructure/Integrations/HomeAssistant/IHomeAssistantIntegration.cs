using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Infrastructure.Integrations.HomeAssistant;

public interface IHomeAssistantIntegration
{
    /// <summary>
    /// Publishes weather forecast data to Home Assistant via MQTT Discovery.
    /// Creates or updates a native <c>weather</c> entity for the given location.
    /// Does nothing if the integration is disabled.
    /// </summary>
    Task PublishWeatherAsync(string location, IEnumerable<DayWeather> forecast, CancellationToken ct = default);
}
