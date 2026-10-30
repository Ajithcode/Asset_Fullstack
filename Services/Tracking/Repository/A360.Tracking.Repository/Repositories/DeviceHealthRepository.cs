using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class DeviceHealthRepository
    : MongoRepository<DeviceHealth>, IDeviceHealthRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "DeviceHealth";

    // Compares text ignoring case, for device id uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public DeviceHealthRepository(IMongoDatabase database)
        : base(database.GetCollection<DeviceHealth>(CollectionName))
    {
    }

    public async Task<DeviceHealth?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(device => device.DeviceId == deviceId && !device.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> DeviceIdExistsAsync(string deviceId, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(device => device.DeviceId == deviceId && device.Id != excludeId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DeviceHealth>(
                Builders<DeviceHealth>.IndexKeys.Ascending(device => device.DeviceId),
                new CreateIndexOptions { Name = "ix_device_health_device_id", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
