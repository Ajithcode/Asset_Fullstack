
using WorkflowListEntity = A360.Workflows.Domain.Entities.WorkflowList;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public interface IWorkflowListRepository : IMongoRepository<WorkflowListEntity>
{
    Task<IReadOnlyCollection<WorkflowListEntity>> GetByModuleAsync(string module, CancellationToken cancellationToken = default);
}
