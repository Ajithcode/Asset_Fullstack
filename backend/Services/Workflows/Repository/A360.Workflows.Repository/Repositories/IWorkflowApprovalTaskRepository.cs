
using WorkflowApprovalTaskEntity = A360.Workflows.Domain.Entities.WorkflowApprovalTask;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public interface IWorkflowApprovalTaskRepository : IMongoRepository<WorkflowApprovalTaskEntity>
{
    Task<IReadOnlyCollection<WorkflowApprovalTaskEntity>> GetByAssignedToAsync(string assignedTo, CancellationToken cancellationToken = default);
}
