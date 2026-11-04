using System.Text;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Connects to the configured MQTT broker, subscribes to every topic in Mqtt:Subscriptions and adds
/// each received message to the Redis stream Mqtt:TargetStream (payload in Mqtt:PayloadField, plus
/// "topic"). RedisStreamIngestWorker then stores it in MongoDB. Reconnects after a lost connection.
/// </summary>
public class MqttSubscriberWorker(
    IConnectionMultiplexer redis,
    IOptions<MqttSettings> options,
    ILogger<MqttSubscriberWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (settings.Subscriptions.Count == 0)
        {
            logger.LogWarning("No Mqtt:Subscriptions configured; MQTT subscriber not started.");
            return;
        }

        using var client = new MqttFactory().CreateMqttClient();
        var disconnected = new TaskCompletionSource();

        client.ApplicationMessageReceivedAsync += e => OnMessageAsync(settings, e);
        client.DisconnectedAsync += e =>
        {
            if (!stoppingToken.IsCancellationRequested)
                logger.LogWarning(e.Exception, "MQTT disconnected from {Host}:{Port} ({Reason})",
                    settings.Host, settings.Port, e.Reason);
            disconnected.TrySetResult();
            return Task.CompletedTask;
        };

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(settings.Host, settings.Port)
            .WithClientId(settings.ClientId)
            .WithCleanSession(settings.CleanSession)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(settings.KeepAliveSeconds));
        if (!string.IsNullOrEmpty(settings.Username))
            builder.WithCredentials(settings.Username, settings.Password);
        if (settings.UseTls)
            builder.WithTlsOptions(o => o.UseTls());
        var clientOptions = builder.Build();

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder();
        foreach (var s in settings.Subscriptions)
            subscribeOptions.WithTopicFilter(s.Topic, (MqttQualityOfServiceLevel)s.Qos);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                disconnected = new TaskCompletionSource();
                await client.ConnectAsync(clientOptions, stoppingToken);
                await client.SubscribeAsync(subscribeOptions.Build(), stoppingToken);
                logger.LogInformation("MQTT connected to {Host}:{Port}, subscribed to {Topics}",
                    settings.Host, settings.Port, string.Join(", ", settings.Subscriptions.Select(s => s.Topic)));

                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MQTT connection to {Host}:{Port} failed", settings.Host, settings.Port);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(settings.ReconnectDelaySeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        if (client.IsConnected)
            await client.DisconnectAsync();
    }

    private async Task OnMessageAsync(MqttSettings settings, MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        try
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            await redis.GetDatabase().StreamAddAsync(settings.TargetStream,
            [
                new NameValueEntry(settings.PayloadField, payload),
                new NameValueEntry("topic", topic),
            ]);
            logger.LogInformation("MQTT received on {Topic} ({Bytes} bytes): {Preview}",
                topic, payload.Length, Preview(payload));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward MQTT message on {Topic} to Redis", topic);
        }
    }

    private static string Preview(string s) => s.Length > 200 ? s[..200] + "..." : s;
}
