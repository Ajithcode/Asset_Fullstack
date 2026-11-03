using MongoDB.Driver;
using A360.Repository.Repositories;
using PriorityEntity = A360.Inspection.Domain.Entities.Priority;

namespace A360.Inspection.Repository.Repositories;

public sealed class PriorityRepository : MongoRepository<PriorityEntity>, IPriorityRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "priorities";

    public PriorityRepository(IMongoDatabase database)
        : base(database.GetCollection<PriorityEntity>(CollectionName))
    {
    }

    public async Task<PriorityEntity?> GetByPriorityCodeAsync(string priorityCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(priority => priority.PriorityCode == priorityCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<PriorityEntity>(
                Builders<PriorityEntity>.IndexKeys.Ascending(priority => priority.PriorityCode),
                new CreateIndexOptions { Name = "ix_priorities_priority_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
