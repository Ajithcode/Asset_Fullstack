using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetFieldLayoutRepository
    : MongoRepository<AssetFieldLayout>, IAssetFieldLayoutRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetFieldLayout";

    // Compares text ignoring case, for layout name uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetFieldLayoutRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetFieldLayout>(CollectionName))
    {
    }

    public async Task<bool> NameExistsAsync(string layoutName, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(layout => layout.LayoutName == layoutName && layout.Id != excludeId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetFieldLayout>> GetUsingGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<AssetFieldLayout>.Filter.ElemMatch(layout => layout.Sections, section => section.GroupId == groupId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetFieldLayout>> GetUsingFieldAsync(string fieldName, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<AssetFieldLayout>.Filter.ElemMatch(layout => layout.Sections, section => section.FieldNames.Contains(fieldName)))
            .ToListAsync(cancellationToken);
    }

    public async Task RenameFieldAsync(string oldFieldName, string newFieldName, CancellationToken cancellationToken = default)
    {
        foreach (var layout in await GetUsingFieldAsync(oldFieldName, cancellationToken))
        {
            foreach (var section in layout.Sections)
            {
                section.FieldNames = section.FieldNames.Select(name => name == oldFieldName ? newFieldName : name).ToList();
            }

            await Collection.UpdateOneAsync(
                existing => existing.Id == layout.Id,
                Builders<AssetFieldLayout>.Update.Set(existing => existing.Sections, layout.Sections),
                cancellationToken: cancellationToken);
        }
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetFieldLayout>(
                Builders<AssetFieldLayout>.IndexKeys.Ascending(layout => layout.LayoutName),
                new CreateIndexOptions { Name = "ix_asset_field_layout_layout_name", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<AssetFieldLayout>(
                Builders<AssetFieldLayout>.IndexKeys.Ascending(layout => layout.AssetTypeId),
                new CreateIndexOptions { Name = "ix_asset_field_layout_asset_type_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
