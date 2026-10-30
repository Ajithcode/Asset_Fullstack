
using ChecklistItemEntity = A360.Wip.Domain.Entities.ChecklistItem;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class ChecklistItemRepository : MongoRepository<ChecklistItemEntity>,
    IChecklistItemRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "checklist_items";

    public ChecklistItemRepository(IMongoDatabase database)
        : base(database.GetCollection<ChecklistItemEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ChecklistItemEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChecklistItemEntity>> GetByChecklistIdAsync(
        string checklistId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.ChecklistId == checklistId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ChecklistItemEntity>(
                Builders<ChecklistItemEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.ItemId),
                new CreateIndexOptions
                {
                    Name = "ix_checklist_item_client_reference"
                }),

            new CreateIndexModel<ChecklistItemEntity>(
                Builders<ChecklistItemEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_checklist_item_asset"
                }),

            new CreateIndexModel<ChecklistItemEntity>(
                Builders<ChecklistItemEntity>.IndexKeys
                    .Ascending(x => x.ChecklistId)
                    .Ascending(x => x.SequenceOrder),
                new CreateIndexOptions
                {
                    Name = "ix_checklist_item_checklist_sequence"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
