using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class DeviceConfigurationRepository
    : MongoRepository<DeviceConfiguration>, IDeviceConfigurationRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "DeviceConfiguration";

    public DeviceConfigurationRepository(IMongoDatabase database)
        : base(database.GetCollection<DeviceConfiguration>(CollectionName))
    {
    }

    public async Task<IReadOnlyList<DeviceConfiguration>> GetVersionsAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(configuration => configuration.DeviceId == deviceId && !configuration.IsDeleted)
            .SortByDescending(configuration => configuration.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceConfiguration?> GetVersionAsync(string deviceId, int version, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(configuration => configuration.DeviceId == deviceId && configuration.Version == version && !configuration.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        // Device ids are stored as the IoT device spells them, so an exact-match index is enough.
        var indexes = new[]
        {
            new CreateIndexModel<DeviceConfiguration>(
                Builders<DeviceConfiguration>.IndexKeys
                    .Ascending(configuration => configuration.DeviceId)
                    .Descending(configuration => configuration.Version),
                new CreateIndexOptions { Name = "ix_device_configuration_device_id_version", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
