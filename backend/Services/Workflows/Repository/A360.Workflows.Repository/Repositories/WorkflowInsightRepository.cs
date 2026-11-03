
using WorkflowInsightEntity = A360.Workflows.Domain.Entities.WorkflowInsight;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public sealed class WorkflowInsightRepository : MongoRepository<WorkflowInsightEntity>,
    IWorkflowInsightRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "workflow_insights";

    public WorkflowInsightRepository(IMongoDatabase database)
        : base(database.GetCollection<WorkflowInsightEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<WorkflowInsightEntity>> GetByModuleAsync(
        string module,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.Module == module)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<WorkflowInsightEntity>(
                Builders<WorkflowInsightEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.WorkflowName),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_insight_client_name"
                }),

            new CreateIndexModel<WorkflowInsightEntity>(
                Builders<WorkflowInsightEntity>.IndexKeys
                    .Ascending(x => x.Module),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_insight_module"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
