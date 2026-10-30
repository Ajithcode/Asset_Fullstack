using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetCustomFieldRepository
    : MongoRepository<AssetCustomFieldDefinition>, IAssetCustomFieldRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetCustomFieldDefinition";

    public AssetCustomFieldRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetCustomFieldDefinition>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetCustomFieldDefinition>> GetByScopesAsync(IReadOnlyCollection<string> scopes, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<AssetCustomFieldDefinition>.Filter.In(field => field.Scope, scopes) & Builders<AssetCustomFieldDefinition>.Filter.Eq(field => field.IsDeleted, false))
            .ToListAsync(cancellationToken);
    }

    public async Task SetGroupFieldsAsync(string groupId, IReadOnlyList<string> orderedFieldIds, CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            Builders<AssetCustomFieldDefinition>.Filter.Eq(field => field.GroupId, groupId) & Builders<AssetCustomFieldDefinition>.Filter.Nin(field => field.Id, orderedFieldIds),
            Builders<AssetCustomFieldDefinition>.Update.Set(field => field.GroupId, null).Set(field => field.DisplayOrder, 0),
            cancellationToken: cancellationToken);

        if (orderedFieldIds.Count == 0)
        {
            return;
        }

        var updates = orderedFieldIds.Select((fieldId, index) => new UpdateOneModel<AssetCustomFieldDefinition>(
            Builders<AssetCustomFieldDefinition>.Filter.Eq(field => field.Id, fieldId),
            Builders<AssetCustomFieldDefinition>.Update.Set(field => field.GroupId, groupId).Set(field => field.DisplayOrder, index + 1)));

        await Collection.BulkWriteAsync(updates, cancellationToken: cancellationToken);
    }

    public async Task ClearGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            field => field.GroupId == groupId,
            Builders<AssetCustomFieldDefinition>.Update.Set(field => field.GroupId, null).Set(field => field.DisplayOrder, 0),
            cancellationToken: cancellationToken);
    }

    public async Task<int> NextDisplayOrderAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var last = await Collection
            .Find(field => field.GroupId == groupId)
            .SortByDescending(field => field.DisplayOrder)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);

        return (last?.DisplayOrder ?? 0) + 1;
    }

    public async Task<bool> AnyAppliesToCategoryAsync(string categoryId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(field => field.AppliesToCategoryId == categoryId && !field.IsDeleted)
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetCustomFieldDefinition>(
                Builders<AssetCustomFieldDefinition>.IndexKeys
                    .Ascending(field => field.Scope)
                    .Ascending(field => field.FieldName),
                new CreateIndexOptions { Name = "ix_asset_custom_field_scope_field_name" }),
            new CreateIndexModel<AssetCustomFieldDefinition>(
                Builders<AssetCustomFieldDefinition>.IndexKeys
                    .Ascending(field => field.GroupId)
                    .Ascending(field => field.DisplayOrder),
                new CreateIndexOptions { Name = "ix_asset_custom_field_group_id_display_order" }),
            new CreateIndexModel<AssetCustomFieldDefinition>(
                Builders<AssetCustomFieldDefinition>.IndexKeys.Ascending(field => field.AppliesToCategoryId),
                new CreateIndexOptions { Name = "ix_asset_custom_field_applies_to_category_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
