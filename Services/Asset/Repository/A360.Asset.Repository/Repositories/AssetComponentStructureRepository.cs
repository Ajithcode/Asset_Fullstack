using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetComponentStructureRepository
    : MongoRepository<AssetComponentStructureNode>, IAssetComponentStructureRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetComponentStructure";

    public AssetComponentStructureRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetComponentStructureNode>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetComponentStructureNode>> GetByAssetAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(node => node.AssetRegistrationId == assetRegistrationId && !node.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(node => node.ParentNodeId == nodeId && !node.IsDeleted)
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AssetComponentStructureNode>(
                Builders<AssetComponentStructureNode>.IndexKeys.Ascending(node => node.AssetRegistrationId),
                new CreateIndexOptions { Name = "ix_asset_component_structure_asset_registration_id" }),
            cancellationToken: cancellationToken);
    }
}
