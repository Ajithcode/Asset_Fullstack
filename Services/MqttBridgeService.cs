using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace NetworkServer.Services;

/// <summary>
/// Subscribes to an external MQTT broker (MqttBridge:Host) and passes every message on its topics to
/// MongoMessageStore, exactly as if the device had published to the built-in broker. That way gateway reports from
/// the external broker go through the same parsing, AllowedTags filter, repeater handling and aggregation.
/// Disabled when MqttBridge:Host or MqttBridge:Topics is empty. Reconnects after a lost connection.
/// </summary>
public class MqttBridgeService(MongoMessageStore store, IConfiguration config, ILogger<MqttBridgeService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = config.GetSection("MqttBridge");
        var host = section["Host"];
        var topics = section.GetSection("Topics").Get<string[]>()?.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray() ?? [];
        if (string.IsNullOrWhiteSpace(host) || topics.Length == 0)
        {
            logger.LogInformation("MqttBridge:Host or MqttBridge:Topics not set; MQTT bridge not started.");
            return;
        }

        var port = section.GetValue("Port", 1883);
        var username = section["Username"];
        var qos = (MqttQualityOfServiceLevel)section.GetValue("Qos", 1);
        var reconnectDelay = TimeSpan.FromSeconds(section.GetValue("ReconnectDelaySeconds", 5));

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(section["ClientId"] is { Length: > 0 } id ? id : $"networkserver-bridge-{Environment.MachineName}")
            .WithCleanSession(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(section.GetValue("KeepAliveSeconds", 60)));
        if (!string.IsNullOrEmpty(username))
            builder.WithCredentials(username, section["Password"]);
        if (section.GetValue("UseTls", false))
            builder.WithTlsOptions(o => o.UseTls());
        var clientOptions = builder.Build();

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder();
        foreach (var topic in topics)
            subscribeOptions.WithTopicFilter(topic, qos);

        using var client = new MqttFactory().CreateMqttClient();
        var disconnected = new TaskCompletionSource();

        client.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            try
            {
                // DeviceId = broker host, so its gateways are grouped apart from devices on the built-in broker.
                await store.IngestAsync("mqtt", host, topic, e.ApplicationMessage.PayloadSegment.ToArray(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to store bridged MQTT message from {Host} on {Topic}", host, topic);
            }
        };
        client.DisconnectedAsync += e =>
        {
            if (!stoppingToken.IsCancellationRequested)
                logger.LogWarning(e.Exception, "MQTT bridge disconnected from {Host}:{Port} ({Reason})", host, port, e.Reason);
            disconnected.TrySetResult();
            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                disconnected = new TaskCompletionSource();
                await client.ConnectAsync(clientOptions, stoppingToken);
                await client.SubscribeAsync(subscribeOptions.Build(), stoppingToken);
                logger.LogInformation("MQTT bridge connected to {Host}:{Port}, subscribed to {Topics}",
                    host, port, string.Join(", ", topics));
                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MQTT bridge connection to {Host}:{Port} failed", host, port);
            }

            try { await Task.Delay(reconnectDelay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        if (client.IsConnected)
            await client.DisconnectAsync();
    }
}
