using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using WorkerService.Models;
using WorkerService.Settings;

namespace WorkerService.Services
{
    public class RedisStreamConsumerService : BackgroundService
    {
        private readonly ILogger<RedisStreamConsumerService> _logger;
        private readonly MongoRepository _repository;
        private readonly RedisSettings _settings;

        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public RedisStreamConsumerService(
            ILogger<RedisStreamConsumerService> logger,
            MongoRepository repository,
            IOptions<RedisSettings> settings)
        {
            _logger = logger;
            _repository = repository;
            _settings = settings.Value;

            _redis = ConnectionMultiplexer.Connect(_settings.ConnectionString);
            _db = _redis.GetDatabase();

            CreateConsumerGroup();
        }

        private void CreateConsumerGroup()
        {
            try
            {
                _db.StreamCreateConsumerGroup(
                    _settings.StreamName,
                    _settings.GroupName,
                    "0-0",
                    true);

                _logger.LogInformation("Consumer group created");
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                _logger.LogInformation("Consumer group already exists");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Redis Stream Worker Started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var entries = _db.StreamReadGroup(
                        _settings.StreamName,
                        _settings.GroupName,
                        _settings.ConsumerName,
                        ">",
                        count: 10);

                    foreach (var entry in entries)
                    {
                        await ProcessMessage(entry, stoppingToken);
                    }

                    // Retry entries left un-acked because no matching device
                    // existed yet (e.g. it may have been added to "device" since).
                    // Must run even when no new entries arrived, otherwise a
                    // pending entry never gets reclaimed once the stream goes idle.
                    await RetryUnmatchedMessages(stoppingToken);

                    if (entries.Length == 0)
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while reading Redis stream");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task RetryUnmatchedMessages(CancellationToken token)
        {
            var claimed = _db.StreamAutoClaim(
                _settings.StreamName,
                _settings.GroupName,
                _settings.ConsumerName,
                minIdleTimeInMs: 5000,
                startAtId: "0-0",
                count: 50);

            foreach (var entry in claimed.ClaimedEntries)
            {
                await ProcessMessage(entry, token);
            }
        }

        private async Task ProcessMessage(StreamEntry entry, CancellationToken token)
        {
            try
            {
                // Try to read "data" first
var json = entry.Values
    .FirstOrDefault(v => v.Name == "data")
    .Value;

// If not found, try "msg"
if (json.IsNullOrEmpty)
{
    json = entry.Values
        .FirstOrDefault(v => v.Name == "msg")
        .Value;
}

// If neither exists, log all fields for debugging
if (json.IsNullOrEmpty)
{
    var fields = string.Join(", ", entry.Values.Select(v => v.Name));

    _logger.LogWarning(
        "No 'data' or 'msg' field found in stream message {Id}. Available fields: {Fields}",
        entry.Id,
        fields);

    return;
}

                var message = JsonSerializer.Deserialize<RedisSensorMessage>(
                    json!,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (message == null)
                {
                    _logger.LogWarning("Failed to deserialize message {Id}", entry.Id);
                    return;
                }

                // Find device hierarchy
                var device = await _repository.GetDevice(message.device_id);

                if (device == null)
                {
                    _logger.LogWarning(
                        "Device not found for unique_id: {DeviceId}",
                        message.device_id);

                    return;
                }

                // Create Mongo document
                var history = new SensorHistory
                {
                    stream_entry_id = entry.Id.ToString(),
                    mydevice_id = device.Id,
                    model_id = device.model_id,
                    type = device.type,
                    unique_id = device.unique_id,

                    project_id = device.project_id,
                    project_name = device.project_name,

                    building_id = device.building_id,
                    building_name = device.building_name,

                    floor_id = device.floor_id,
                    floor_name = device.floor_name,

                    area_id = device.area_id,
                    area_name = device.area_name,

                    zone_id = device.zone_id,
                    zone_name = device.zone_name,

                    country_id = device.country_id,
                    country_name = device.country_name,

                    description = device.description,
                    module = device.module,

                    macaddress = message.macaddress,
                    gatewayid = message.gatewayid,
                    ant_id = message.ant_id,

                    sensor_status = message.sensor_status,
                    device_id = message.device_id,
                    device_name = message.device_name,

                    mfg_name = message.mfg_name,
                    device_type = message.device_type,

                    sensor_attributes = message.sensor_attributes,

                    read_date = DateTime.UtcNow,
                    created_date = message.created_date ?? DateTime.UtcNow,
                    created_date_utc = message.created_date_utc ?? DateTime.UtcNow
                };

                // Save to MongoDB
                await _repository.Save(history);

                // ACK + Delete from Redis
                _db.StreamAcknowledge(
                    _settings.StreamName,
                    _settings.GroupName,
                    entry.Id);

                _db.StreamDelete(
    _settings.StreamName,
    new RedisValue[] { entry.Id });

                _logger.LogInformation(
                    "Processed and removed message {Id} for device {DeviceId}",
                    entry.Id,
                    message.device_id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {Id}", entry.Id);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Redis Stream Worker");

            await _redis.CloseAsync();
            _redis.Dispose();

            await base.StopAsync(cancellationToken);
        }
    }
}