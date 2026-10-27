using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class FirmwareVersionRepository
    : MongoRepository<FirmwareVersion>, IFirmwareVersionRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "FirmwareVersion";

    // Compares text ignoring case, for device model uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public FirmwareVersionRepository(IMongoDatabase database)
        : base(database.GetCollection<FirmwareVersion>(CollectionName))
    {
    }

    public async Task<bool> DeviceModelExistsAsync(string deviceModel, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(firmware => firmware.DeviceModel == deviceModel, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<FirmwareVersion>(
                Builders<FirmwareVersion>.IndexKeys.Ascending(firmware => firmware.DeviceModel),
                new CreateIndexOptions { Name = "ix_firmware_version_device_model", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
