using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetCategoryTypeRepository
    : MongoRepository<AssetCategoryType>, IAssetCategoryTypeRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetCategoryType";

    // Compares text ignoring case, for code and name uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetCategoryTypeRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetCategoryType>(CollectionName))
    {
    }

    public async Task<bool> CodeExistsAsync(string typeCode, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(type => type.TypeCode == typeCode && type.Id != excludeId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> NameExistsInCategoryAsync(string typeName, string categoryId, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(
                type => type.TypeName == typeName && type.CategoryId == categoryId && type.Id != excludeId,
                new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyInCategoryAsync(string categoryId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(type => type.CategoryId == categoryId)
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetCategoryType>(
                Builders<AssetCategoryType>.IndexKeys.Ascending(type => type.TypeCode),
                new CreateIndexOptions { Name = "ix_asset_category_type_type_code", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<AssetCategoryType>(
                Builders<AssetCategoryType>.IndexKeys
                    .Ascending(type => type.CategoryId)
                    .Ascending(type => type.TypeName),
                new CreateIndexOptions { Name = "ix_asset_category_type_category_id_type_name", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
