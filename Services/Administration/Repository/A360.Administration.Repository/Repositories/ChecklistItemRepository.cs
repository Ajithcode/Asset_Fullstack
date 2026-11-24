using MongoDB.Bson;
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

    public async Task<ChecklistItem?> GetByItemIdAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        return await Collection
            .Find(item => item.ItemId == itemId && !item.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChecklistItem>> GetAsync(
        string? tenantId,
        string? clientId,
        string? section,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ChecklistItem>.Filter;
        var filter = builder.Eq(item => item.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(item => item.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(item => item.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(section))
        {
            filter &= builder.Regex(item => item.Section, ExactIgnoreCase(section));
        }

        return await Collection
            .Find(filter)
            .SortBy(item => item.Section)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
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

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
