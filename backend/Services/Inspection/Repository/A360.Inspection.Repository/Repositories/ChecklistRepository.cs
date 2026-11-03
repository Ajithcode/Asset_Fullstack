using MongoDB.Driver;
using A360.Repository.Repositories;
using ChecklistEntity = A360.Inspection.Domain.Entities.Checklist;

namespace A360.Inspection.Repository.Repositories;

public sealed class ChecklistRepository : MongoRepository<ChecklistEntity>, IChecklistRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "checklists";

    public ChecklistRepository(IMongoDatabase database)
        : base(database.GetCollection<ChecklistEntity>(CollectionName))
    {
    }

    public async Task<ChecklistEntity?> GetByChecklistCodeAsync(string checklistCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(checklist => checklist.ChecklistCode == checklistCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ChecklistEntity>(
                Builders<ChecklistEntity>.IndexKeys.Ascending(checklist => checklist.ChecklistCode),
                new CreateIndexOptions { Name = "ix_checklists_checklist_code", Unique = true }),
            new CreateIndexModel<ChecklistEntity>(
                Builders<ChecklistEntity>.IndexKeys.Ascending(checklist => checklist.InspectionTypeId),
                new CreateIndexOptions { Name = "ix_checklists_inspection_type_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
