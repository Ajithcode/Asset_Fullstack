
using AssetLinkingEntity = A360.Wip.Domain.Entities.AssetLinking;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class AssetLinkingRepository : MongoRepository<AssetLinkingEntity>,
    IAssetLinkingRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "asset_linkings";

    public AssetLinkingRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetLinkingEntity>(CollectionName))
    {
    }

    public async Task<AssetLinkingEntity?> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetLinkingEntity>(
                Builders<AssetLinkingEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_asset_linking_client_asset",
                    Unique = true
                }),

            new CreateIndexModel<AssetLinkingEntity>(
                Builders<AssetLinkingEntity>.IndexKeys
                    .Ascending(x => x.CurrentStatus),
                new CreateIndexOptions
                {
                    Name = "ix_asset_linking_current_status"
                }),

            new CreateIndexModel<AssetLinkingEntity>(
                Builders<AssetLinkingEntity>.IndexKeys
                    .Ascending(x => x.RfidTag),
                new CreateIndexOptions
                {
                    Name = "ix_asset_linking_rfid_tag"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
