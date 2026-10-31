
using WorkflowApprovalTaskEntity = A360.Workflows.Domain.Entities.WorkflowApprovalTask;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public sealed class WorkflowApprovalTaskRepository : MongoRepository<WorkflowApprovalTaskEntity>,
    IWorkflowApprovalTaskRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "workflow_approval_tasks";

    public WorkflowApprovalTaskRepository(IMongoDatabase database)
        : base(database.GetCollection<WorkflowApprovalTaskEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<WorkflowApprovalTaskEntity>> GetByAssignedToAsync(
        string assignedTo,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssignedTo == assignedTo)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<WorkflowApprovalTaskEntity>(
                Builders<WorkflowApprovalTaskEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.WorkflowName),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_approval_task_client_name"
                }),

            new CreateIndexModel<WorkflowApprovalTaskEntity>(
                Builders<WorkflowApprovalTaskEntity>.IndexKeys
                    .Ascending(x => x.AssignedTo),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_approval_task_assigned_to"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
