using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetModelRepository
    : MongoRepository<AssetModel>, IAssetModelRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetModel";

    // Compares text ignoring case, for code and name uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetModelRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetModel>(CollectionName))
    {
    }

    public async Task<bool> CodeExistsAsync(string modelCode, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(model => model.ModelCode == modelCode && model.Id != excludeId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> NameExistsInTypeAsync(string modelName, string assetTypeId, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(
                model => model.ModelName == modelName && model.AssetTypeId == assetTypeId && model.Id != excludeId,
                new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyInTypeAsync(string assetTypeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(model => model.AssetTypeId == assetTypeId)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, long>> CountByTypeAsync(CancellationToken cancellationToken = default)
    {
        var counts = await Collection.Aggregate()
            .Group(model => model.AssetTypeId, group => new { AssetTypeId = group.Key, Count = group.LongCount() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(count => count.AssetTypeId, count => count.Count);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetModel>(
                Builders<AssetModel>.IndexKeys.Ascending(model => model.ModelCode),
                new CreateIndexOptions { Name = "ix_asset_model_model_code", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<AssetModel>(
                Builders<AssetModel>.IndexKeys
                    .Ascending(model => model.AssetTypeId)
                    .Ascending(model => model.ModelName),
                new CreateIndexOptions { Name = "ix_asset_model_asset_type_id_model_name", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
