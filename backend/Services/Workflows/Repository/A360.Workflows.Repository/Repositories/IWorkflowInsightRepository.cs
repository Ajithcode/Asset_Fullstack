
using WorkflowInsightEntity = A360.Workflows.Domain.Entities.WorkflowInsight;
using A360.Repository.Repositories;

namespace A360.Workflows.Repository.Repositories;

public interface IWorkflowInsightRepository : IMongoRepository<WorkflowInsightEntity>
{
    Task<IReadOnlyCollection<WorkflowInsightEntity>> GetByModuleAsync(string module, CancellationToken cancellationToken = default);
}
