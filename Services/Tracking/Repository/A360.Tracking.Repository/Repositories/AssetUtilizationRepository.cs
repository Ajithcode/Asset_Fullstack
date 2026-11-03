using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class AssetUtilizationRepository
    : MongoRepository<AssetUtilization>, IAssetUtilizationRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetUtilization";

    // Compares text ignoring case, for id and asset type lookups.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetUtilizationRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetUtilization>(CollectionName))
    {
    }

    public async Task<AssetUtilization?> GetByUtilizationIdAsync(string utilizationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(record => record.UtilizationId == utilizationId && !record.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AssetUtilization?> GetByAssetAndDateAsync(string assetRegistrationId, DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(record => record.AssetRegistrationId == assetRegistrationId && record.Date == dateUtc && !record.IsDeleted,
                new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetUtilization>> GetInRangeAsync(
        DateTime fromUtc, DateTime toUtc, string? assetType = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AssetUtilization>.Filter;
        var filters = new List<FilterDefinition<AssetUtilization>>
        {
            filter.Eq(record => record.IsDeleted, false),
            filter.Gte(record => record.Date, fromUtc),
            filter.Lt(record => record.Date, toUtc)
        };

        if (!string.IsNullOrWhiteSpace(assetType))
        {
            filters.Add(filter.Eq(record => record.AssetType, assetType.Trim()));
        }

        return await Collection
            .Find(filter.And(filters), new FindOptions { Collation = CaseInsensitive })
            .SortByDescending(record => record.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetUtilization>(
                Builders<AssetUtilization>.IndexKeys.Ascending(record => record.UtilizationId),
                new CreateIndexOptions { Name = "ix_asset_utilization_utilization_id", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<AssetUtilization>(
                Builders<AssetUtilization>.IndexKeys.Descending(record => record.Date).Ascending(record => record.AssetType),
                new CreateIndexOptions { Name = "ix_asset_utilization_date_asset_type" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
