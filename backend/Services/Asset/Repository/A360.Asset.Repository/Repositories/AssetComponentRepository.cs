using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetComponentRepository
    : MongoRepository<AssetComponent>, IAssetComponentRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetComponent";

    // Compares text ignoring case, for component tag uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetComponentRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetComponent>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetComponent>> GetByAssetAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(component => component.AssetRegistrationId == assetRegistrationId && !component.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TagExistsAsync(string assetRegistrationId, string componentTag, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(
                component => component.AssetRegistrationId == assetRegistrationId && component.ComponentTag == componentTag && component.Id != excludeId,
                new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(string componentId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(component => component.ParentComponentId == componentId)
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetComponent>(
                Builders<AssetComponent>.IndexKeys
                    .Ascending(component => component.AssetRegistrationId)
                    .Ascending(component => component.ComponentTag),
                new CreateIndexOptions { Name = "ix_asset_component_asset_registration_id_component_tag", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<AssetComponent>(
                Builders<AssetComponent>.IndexKeys.Ascending(component => component.ParentComponentId),
                new CreateIndexOptions { Name = "ix_asset_component_parent_component_id" }),
            new CreateIndexModel<AssetComponent>(
                Builders<AssetComponent>.IndexKeys.Ascending(component => component.ExpectedReplacementDate),
                new CreateIndexOptions { Name = "ix_asset_component_expected_replacement_date" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
