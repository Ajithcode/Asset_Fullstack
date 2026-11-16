using StackExchange.Redis;

namespace NetworkServer.Services;

/// <summary>
/// Adds every stored DeviceMessage to the Redis stream {Redis:Stream} for WorkerNetworkService. Entry fields:
///   msg = the parsed payload as JSON (for gateways [{"gatewayid":..,"timestamp":..},{"tagid":..}, ...]),
///   source, deviceId, topic, format (left out when null)
/// The stream is trimmed to about {Redis:MaxLength} entries.
/// </summary>
public class RedisStreamPublisher(IConnectionMultiplexer redis, IConfiguration config)
{
    private readonly RedisKey _stream = config["Redis:Stream"] ?? "networkserver_stream";
    private readonly int _maxLength = config.GetValue("Redis:MaxLength", 100_000);

    public Task PublishAsync(DeviceMessage message, string json)
    {
        var fields = new List<NameValueEntry>
        {
            new("msg", json),
            new("source", message.Source),
            new("format", message.Format),
        };
        if (message.DeviceId is not null)
            fields.Add(new("deviceId", message.DeviceId));
        if (message.Topic is not null)
            fields.Add(new("topic", message.Topic));

        return redis.GetDatabase().StreamAddAsync(_stream, fields.ToArray(), maxLength: _maxLength, useApproximateMaxLength: true);
    }
}
