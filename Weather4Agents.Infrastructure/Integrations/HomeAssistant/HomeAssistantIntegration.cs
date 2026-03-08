using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Weather4Agents.Application.Settings.Integrations;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Infrastructure.Integrations.HomeAssistant;

public class HomeAssistantIntegration : IHomeAssistantIntegration
{
    private readonly HomeAssistantIntegrationSettings _settings;
    private readonly ILogger<HomeAssistantIntegration> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HomeAssistantIntegration(
        IOptions<HomeAssistantIntegrationSettings> options,
        ILogger<HomeAssistantIntegration> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task PublishWeatherAsync(string location, IEnumerable<DayWeather> forecast, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var days = forecast.ToList();
        if (days.Count == 0)
        {
            _logger.LogWarning("No forecast data available for '{Location}'. Skipping HA publish.", location);
            return;
        }

        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        try
        {
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.MqttBrokerHost, _settings.MqttBrokerPort)
                .WithClientId(_settings.MqttClientId);

            if (!string.IsNullOrEmpty(_settings.MqttUsername))
                optionsBuilder = optionsBuilder.WithCredentials(_settings.MqttUsername, _settings.MqttPassword);

            await client.ConnectAsync(optionsBuilder.Build(), ct);

            var nodeId = HomeAssistantWeatherMapper.BuildNodeId(location);
            var discoveryTopic = HomeAssistantWeatherMapper.BuildDiscoveryTopic(_settings.DiscoveryPrefix, nodeId);
            var stateTopic = HomeAssistantWeatherMapper.BuildStateTopic(_settings.DiscoveryPrefix, nodeId);
            var forecastDailyTopic = HomeAssistantWeatherMapper.BuildForecastDailyTopic(_settings.DiscoveryPrefix, nodeId);

            await PublishRetainedAsync(client,
                discoveryTopic,
                HomeAssistantWeatherMapper.BuildDiscoveryPayload(location, _settings.DiscoveryPrefix),
                ct);

            await PublishRetainedAsync(client,
                stateTopic,
                HomeAssistantWeatherMapper.BuildStatePayload(days),
                ct);

            await PublishRetainedAsync(client,
                forecastDailyTopic,
                HomeAssistantWeatherMapper.BuildForecastDailyPayload(days),
                ct);

            _logger.LogInformation("Published HA weather update for '{Location}'", location);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to publish HA weather update for '{Location}'", location);
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(cancellationToken: ct);
        }
    }

    private static async Task PublishRetainedAsync(IMqttClient client, string topic, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithRetainFlag(true)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await client.PublishAsync(message, ct);
    }
}
