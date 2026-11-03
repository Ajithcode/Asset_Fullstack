
using ChecklistMasterEntity = A360.Wip.Domain.Entities.ChecklistMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class ChecklistMasterRepository : MongoRepository<ChecklistMasterEntity>,
    IChecklistMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "checklist_masters";

    public ChecklistMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<ChecklistMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ChecklistMasterEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ChecklistMasterEntity>(
                Builders<ChecklistMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.ChecklistId),
                new CreateIndexOptions
                {
                    Name = "ix_checklist_master_client_reference"
                }),

            new CreateIndexModel<ChecklistMasterEntity>(
                Builders<ChecklistMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_checklist_master_asset"
                }),

            new CreateIndexModel<ChecklistMasterEntity>(
                Builders<ChecklistMasterEntity>.IndexKeys
                    .Ascending(x => x.ChecklistType),
                new CreateIndexOptions
                {
                    Name = "ix_checklist_master_type"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
