
using WorkflowBuilderEntity = A360.Workflows.Domain.Entities.WorkflowBuilder;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public interface IWorkflowBuilderRepository : IMongoRepository<WorkflowBuilderEntity>
{
    Task<IReadOnlyCollection<WorkflowBuilderEntity>> GetByModuleAsync(string module, CancellationToken cancellationToken = default);
}
