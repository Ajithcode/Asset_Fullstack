
using WorkflowInstanceEntity = A360.Workflows.Domain.Entities.WorkflowInstance;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public interface IWorkflowInstanceRepository : IMongoRepository<WorkflowInstanceEntity>
{
    Task<IReadOnlyCollection<WorkflowInstanceEntity>> GetByWorkflowNameAsync(string workflowName, CancellationToken cancellationToken = default);
}
