using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class ChecklistItemRepository
    : MongoRepository<ChecklistItem>, IChecklistItemRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_checklist_items";

    public ChecklistItemRepository(IMongoDatabase database)
        : base(database.GetCollection<ChecklistItem>(CollectionName))
    {
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ChecklistItem>(
                Builders<ChecklistItem>.IndexKeys.Ascending(item => item.ItemId),
                new CreateIndexOptions { Name = "ux_administration_checklist_items_item_id", Unique = true }),
            new CreateIndexModel<ChecklistItem>(
                Builders<ChecklistItem>.IndexKeys
                    .Ascending(item => item.TenantId)
                    .Ascending(item => item.Section),
                new CreateIndexOptions { Name = "ix_administration_checklist_items_tenant_id_section" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
