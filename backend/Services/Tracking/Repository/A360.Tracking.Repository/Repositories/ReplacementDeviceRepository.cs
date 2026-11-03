using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class ReplacementDeviceRepository
    : MongoRepository<ReplacementDevice>, IReplacementDeviceRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "ReplacementDevice";

    // Compares text ignoring case, for device id lookups and uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public ReplacementDeviceRepository(IMongoDatabase database)
        : base(database.GetCollection<ReplacementDevice>(CollectionName))
    {
    }

    public async Task<ReplacementDevice?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(device => device.DeviceId == deviceId && !device.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> DeviceIdExistsAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(device => device.DeviceId == deviceId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ReplacementDevice>(
                Builders<ReplacementDevice>.IndexKeys.Ascending(device => device.DeviceId),
                new CreateIndexOptions { Name = "ix_replacement_device_device_id", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
