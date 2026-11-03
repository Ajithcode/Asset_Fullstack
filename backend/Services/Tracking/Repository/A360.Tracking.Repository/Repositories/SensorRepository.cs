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

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Sensor>(
                Builders<Sensor>.IndexKeys.Ascending(sensor => sensor.SensorId),
                new CreateIndexOptions { Name = "ix_sensor_sensor_id", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
