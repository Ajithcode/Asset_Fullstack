using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class DeviceModelRepository
    : MongoRepository<DeviceModel>, IDeviceModelRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "DeviceModel";

    // Compares text ignoring case, for model number lookups and uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public DeviceModelRepository(IMongoDatabase database)
        : base(database.GetCollection<DeviceModel>(CollectionName))
    {
    }

    public async Task<DeviceModel?> GetByModelNumberAsync(string modelNumber, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(model => model.ModelNumber == modelNumber && !model.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ModelNumberExistsAsync(string modelNumber, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(model => model.ModelNumber == modelNumber, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByDeviceTypeAsync(CancellationToken cancellationToken = default)
    {
        var counts = await Collection
            .Aggregate()
            .Match(model => !model.IsDeleted)
            .Group(model => model.DeviceType, group => new { DeviceType = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(count => count.DeviceType, count => count.Count);
    }

    public async Task<IReadOnlyList<DeviceModel>> GetSelectableAsync(string? deviceType, CancellationToken cancellationToken = default)
    {
        var filter = Builders<DeviceModel>.Filter.Where(model => !model.IsDeleted && model.Status != DeviceModelStatus.Inactive);
        if (!string.IsNullOrWhiteSpace(deviceType))
        {
            filter &= Builders<DeviceModel>.Filter.Eq(model => model.DeviceType, deviceType.Trim());
        }

        return await Collection
            .Find(filter, new FindOptions { Collation = CaseInsensitive })
            .SortBy(model => model.Manufacturer)
            .ThenBy(model => model.ModelNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DeviceModel>(
                Builders<DeviceModel>.IndexKeys.Ascending(model => model.ModelNumber),
                new CreateIndexOptions { Name = "ix_device_model_model_number", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
