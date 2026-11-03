
using WorkflowBuilderEntity = A360.Workflows.Domain.Entities.WorkflowBuilder;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public sealed class WorkflowBuilderRepository : MongoRepository<WorkflowBuilderEntity>,
    IWorkflowBuilderRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "workflow_builders";

    public WorkflowBuilderRepository(IMongoDatabase database)
        : base(database.GetCollection<WorkflowBuilderEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<WorkflowBuilderEntity>> GetByModuleAsync(
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
            new CreateIndexModel<WorkflowBuilderEntity>(
                Builders<WorkflowBuilderEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.WorkflowName),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_builder_client_name"
                }),

            new CreateIndexModel<WorkflowBuilderEntity>(
                Builders<WorkflowBuilderEntity>.IndexKeys
                    .Ascending(x => x.Module),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_builder_module"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
