using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class SensorRepository
    : MongoRepository<Sensor>, ISensorRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "Sensor";

    // Compares text ignoring case, for sensor id lookups and uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public SensorRepository(IMongoDatabase database)
        : base(database.GetCollection<Sensor>(CollectionName))
    {
    }

    public async Task<Sensor?> GetBySensorIdAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(sensor => sensor.SensorId == sensorId && !sensor.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SensorIdExistsAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(sensor => sensor.SensorId == sensorId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> SerialNumberExistsAsync(string serialNumber, string? exceptId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Sensor>.Filter.Eq(sensor => sensor.SerialNumber, serialNumber);
        if (!string.IsNullOrEmpty(exceptId))
        {
            filter &= Builders<Sensor>.Filter.Ne(sensor => sensor.Id, exceptId);
        }

        return await Collection
            .Find(filter, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Sensor>> GetByDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(sensor => sensor.DeviceId == deviceId && !sensor.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .SortBy(sensor => sensor.Channel)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Sensor>(
                Builders<Sensor>.IndexKeys.Ascending(sensor => sensor.SensorId),
                new CreateIndexOptions { Name = "ix_sensor_sensor_id", Unique = true, Collation = CaseInsensitive }),
            // Older sensors have no serial number, so uniqueness is enforced in the API rather than by a unique index.
            new CreateIndexModel<Sensor>(
                Builders<Sensor>.IndexKeys.Ascending(sensor => sensor.SerialNumber),
                new CreateIndexOptions { Name = "ix_sensor_serial_number", Collation = CaseInsensitive }),
            new CreateIndexModel<Sensor>(
                Builders<Sensor>.IndexKeys.Ascending(sensor => sensor.DeviceId).Ascending(sensor => sensor.Channel),
                new CreateIndexOptions { Name = "ix_sensor_device_channel", Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
