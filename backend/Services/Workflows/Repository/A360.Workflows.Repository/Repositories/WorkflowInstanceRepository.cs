
using WorkflowInstanceEntity = A360.Workflows.Domain.Entities.WorkflowInstance;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public sealed class WorkflowInstanceRepository : MongoRepository<WorkflowInstanceEntity>,
    IWorkflowInstanceRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "workflow_instances";

    public WorkflowInstanceRepository(IMongoDatabase database)
        : base(database.GetCollection<WorkflowInstanceEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<WorkflowInstanceEntity>> GetByWorkflowNameAsync(
        string workflowName,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.WorkflowName == workflowName)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<WorkflowInstanceEntity>(
                Builders<WorkflowInstanceEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.WorkflowName),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_instance_client_name"
                }),

            new CreateIndexModel<WorkflowInstanceEntity>(
                Builders<WorkflowInstanceEntity>.IndexKeys
                    .Ascending(x => x.RequestedBy),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_instance_requested_by"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
