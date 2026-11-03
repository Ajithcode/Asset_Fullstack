using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetCategoryRepository
    : MongoRepository<AssetCategory>, IAssetCategoryRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetCategory";

    // Compares text ignoring case, for code and name uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetCategoryRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetCategory>(CollectionName))
    {
    }

    public async Task<bool> CodeExistsAsync(string code, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(category => category.Code == code && category.Id != excludeId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> NameExistsUnderParentAsync(string name, string? parentCategoryId, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(
                category => category.Name == name && category.ParentCategoryId == parentCategoryId && category.Id != excludeId,
                new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(category => category.ParentCategoryId == id)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> UpdateDetailsAsync(AssetCategory category, CancellationToken cancellationToken = default)
    {
        var result = await Collection.UpdateOneAsync(
            existing => existing.Id == category.Id,
            Builders<AssetCategory>.Update
                .Set(existing => existing.ParentCategoryId, category.ParentCategoryId)
                .Set(existing => existing.Name, category.Name)
                .Set(existing => existing.Code, category.Code)
                .Set(existing => existing.Description, category.Description)
                .Set(existing => existing.Status, category.Status)
                .Set(existing => existing.ImageUrl, category.ImageUrl)
                .Set(existing => existing.UpdatedBy, category.UpdatedBy)
                .Set(existing => existing.UpdatedAt, category.UpdatedAt),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> AddCustomFieldAsync(string categoryId, AssetCategoryCustomField field, CancellationToken cancellationToken = default)
    {
        var result = await Collection.UpdateOneAsync(
            category => category.Id == categoryId,
            Builders<AssetCategory>.Update.Push(category => category.CustomFields, field),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> RemoveCustomFieldAsync(string categoryId, string fieldId, CancellationToken cancellationToken = default)
    {
        var result = await Collection.UpdateOneAsync(
            category => category.Id == categoryId,
            Builders<AssetCategory>.Update.PullFilter(category => category.CustomFields, field => field.Id == fieldId),
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> AddAttributeAsync(string categoryId, AssetCategoryAttribute attribute, CancellationToken cancellationToken = default)
    {
        var result = await Collection.UpdateOneAsync(
            category => category.Id == categoryId,
            Builders<AssetCategory>.Update.Push(category => category.Attributes, attribute),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> RemoveAttributeAsync(string categoryId, string attributeId, CancellationToken cancellationToken = default)
    {
        var result = await Collection.UpdateOneAsync(
            category => category.Id == categoryId,
            Builders<AssetCategory>.Update.PullFilter(category => category.Attributes, attribute => attribute.Id == attributeId),
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetCategory>(
                Builders<AssetCategory>.IndexKeys.Ascending(category => category.Code),
                new CreateIndexOptions { Name = "ix_asset_category_code", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<AssetCategory>(
                Builders<AssetCategory>.IndexKeys
                    .Ascending(category => category.ParentCategoryId)
                    .Ascending(category => category.Name),
                new CreateIndexOptions { Name = "ix_asset_category_parent_category_id_name", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
