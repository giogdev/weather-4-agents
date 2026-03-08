namespace Weather4Agents.Application.Settings.Integrations;

public class HomeAssistantIntegrationSettings
{
    public const string SectionName = "Integrations:HomeAssistant";

    /// <summary>
    /// Enables or disables the Home Assistant MQTT integration entirely.
    /// Can be overridden via environment variable <c>Integrations__HomeAssistant__Enabled</c>.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Hostname or IP address of the MQTT broker.
    /// Can be overridden via environment variable <c>Integrations__HomeAssistant__MqttBrokerHost</c>.
    /// </summary>
    public string MqttBrokerHost { get; set; } = "localhost";

    /// <summary>
    /// Port of the MQTT broker.
    /// Can be overridden via environment variable <c>Integrations__HomeAssistant__MqttBrokerPort</c>.
    /// </summary>
    public int MqttBrokerPort { get; set; } = 1883;

    /// <summary>
    /// MQTT broker username (optional).
    /// Can be overridden via environment variable <c>Integrations__HomeAssistant__MqttUsername</c>.
    /// </summary>
    public string MqttUsername { get; set; } = string.Empty;

    /// <summary>
    /// MQTT broker password (optional).
    /// Can be overridden via environment variable <c>Integrations__HomeAssistant__MqttPassword</c>.
    /// </summary>
    public string MqttPassword { get; set; } = string.Empty;

    /// <summary>
    /// MQTT client identifier used when connecting to the broker.
    /// Can be overridden via environment variable <c>Integrations__HomeAssistant__MqttClientId</c>.
    /// </summary>
    public string MqttClientId { get; set; } = "weather4agents";

    /// <summary>
    /// Home Assistant MQTT discovery prefix (typically "homeassistant").
    /// Can be overridden via environment variable <c>Integrations__HomeAssistant__DiscoveryPrefix</c>.
    /// </summary>
    public string DiscoveryPrefix { get; set; } = "homeassistant";
}
