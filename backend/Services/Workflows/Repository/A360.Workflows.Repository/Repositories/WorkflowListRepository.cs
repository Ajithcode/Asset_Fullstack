
using WorkflowListEntity = A360.Workflows.Domain.Entities.WorkflowList;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public sealed class WorkflowListRepository : MongoRepository<WorkflowListEntity>,
    IWorkflowListRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "workflow_lists";

    public WorkflowListRepository(IMongoDatabase database)
        : base(database.GetCollection<WorkflowListEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<WorkflowListEntity>> GetByModuleAsync(
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
            new CreateIndexModel<WorkflowListEntity>(
                Builders<WorkflowListEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.WorkflowName),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_list_client_name"
                }),

            new CreateIndexModel<WorkflowListEntity>(
                Builders<WorkflowListEntity>.IndexKeys
                    .Ascending(x => x.Module),
                new CreateIndexOptions
                {
                    Name = "ix_workflow_list_module"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
