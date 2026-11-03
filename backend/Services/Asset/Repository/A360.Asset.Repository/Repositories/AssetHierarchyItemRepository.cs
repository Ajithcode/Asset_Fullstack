using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetHierarchyItemRepository
    : MongoRepository<AssetHierarchyItem>, IAssetHierarchyItemRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetHierarchyItem";

    // Compares text ignoring case, for name and asset tag uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetHierarchyItemRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetHierarchyItem>(CollectionName))
    {
    }

    public async Task<bool> NameExistsUnderParentAsync(string name, string? parentId, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(item => item.Name == name && item.ParentId == parentId && item.Id != excludeId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AssetTagExistsAsync(string assetTag, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(item => item.AssetTag == assetTag && item.Id != excludeId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetHierarchyItem>(
                Builders<AssetHierarchyItem>.IndexKeys
                    .Ascending(item => item.ParentId)
                    .Ascending(item => item.Name),
                new CreateIndexOptions { Name = "ix_asset_hierarchy_item_parent_id_name", Unique = true, Collation = CaseInsensitive }),
            // Unique only among items that have a tag; items without one are not compared.
            new CreateIndexModel<AssetHierarchyItem>(
                Builders<AssetHierarchyItem>.IndexKeys.Ascending(item => item.AssetTag),
                new CreateIndexOptions<AssetHierarchyItem>
                {
                    Name = "ix_asset_hierarchy_item_asset_tag",
                    Unique = true,
                    Collation = CaseInsensitive,
                    PartialFilterExpression = Builders<AssetHierarchyItem>.Filter.Type(item => item.AssetTag, BsonType.String)
                }),
            new CreateIndexModel<AssetHierarchyItem>(
                Builders<AssetHierarchyItem>.IndexKeys.Ascending(item => item.AssetTypeId),
                new CreateIndexOptions { Name = "ix_asset_hierarchy_item_asset_type_id" }),
            new CreateIndexModel<AssetHierarchyItem>(
                Builders<AssetHierarchyItem>.IndexKeys.Ascending(item => item.ModelId),
                new CreateIndexOptions { Name = "ix_asset_hierarchy_item_model_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
