using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace NetworkServer.Services;

/// <summary>
/// Built-in MQTT broker: devices connect straight to this service (default port 1883) and publish.
/// Every published message is stored in MongoDB (gateway data via TagAggregator), and still delivered to any other subscribers.
/// </summary>
public class MqttBrokerService(MongoMessageStore store, IConfiguration config, ILogger<MqttBrokerService> logger)
    : BackgroundService
{
    private MqttServer? _server;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = config.GetSection("Mqtt");
        var port = section.GetValue("Port", 1883);
        var username = section["Username"];
        var password = section["Password"];

        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .Build();

        _server = new MqttFactory().CreateMqttServer(options);

        // Optional username/password check (disabled when Mqtt:Username is empty).
        _server.ValidatingConnectionAsync += e =>
        {
            if (!string.IsNullOrEmpty(username) && (e.UserName != username || e.Password != password))
            {
                e.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                logger.LogWarning("Rejected MQTT client {ClientId}: bad credentials", e.ClientId);
            }
            return Task.CompletedTask;
        };

        _server.ClientConnectedAsync += e =>
        {
            logger.LogInformation("MQTT client connected: {ClientId} ({Endpoint})", e.ClientId, e.Endpoint);
            return Task.CompletedTask;
        };

        _server.InterceptingPublishAsync += async e =>
        {
            try
            {
                var payload = e.ApplicationMessage.PayloadSegment.ToArray();
                await store.IngestAsync("mqtt", e.ClientId, e.ApplicationMessage.Topic, payload, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to store MQTT message from {ClientId} on {Topic}",
                    e.ClientId, e.ApplicationMessage.Topic);
            }
        };

        await _server.StartAsync();
        logger.LogInformation("MQTT broker listening on port {Port}", port);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server is not null)
            await _server.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}
