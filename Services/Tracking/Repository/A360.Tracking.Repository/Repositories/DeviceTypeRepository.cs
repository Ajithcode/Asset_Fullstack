using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IDeviceTypeRepository : IMongoRepository<DeviceType>
{
    Task<DeviceType?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Names are unique, ignoring case.</summary>
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Every device type that is not deleted, by name.</summary>
    Task<IReadOnlyList<DeviceType>> GetActiveAsync(CancellationToken cancellationToken = default);
}

public sealed class DeviceTypeRepository
    : MongoRepository<DeviceType>, IDeviceTypeRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "DeviceType";

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public DeviceTypeRepository(IMongoDatabase database)
        : base(database.GetCollection<DeviceType>(CollectionName))
    {
    }

    public async Task<DeviceType?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(type => type.Name == name && !type.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(type => type.Name == name, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceType>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(type => !type.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .SortBy(type => type.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Creates the unique name index and, when there are no device types yet, the standard ones.</summary>
    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<DeviceType>(
                Builders<DeviceType>.IndexKeys.Ascending(type => type.Name),
                new CreateIndexOptions { Name = "ix_device_type_name", Unique = true, Collation = CaseInsensitive }),
            cancellationToken: cancellationToken);

        if (await Collection.Find(Builders<DeviceType>.Filter.Empty).AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        await Collection.InsertManyAsync(DeviceTypeDefaults.All.Select(type => new DeviceType
        {
            Id = MongoObjectId.Create(),
            Name = type.Name,
            Icon = type.Icon,
            ThumbnailStyle = type.Thumbnail,
            Description = type.Description,
            CreatedBy = "system (default device types)",
            CreatedAt = now,
            IsDeleted = false
        }), cancellationToken: cancellationToken);
    }
}
