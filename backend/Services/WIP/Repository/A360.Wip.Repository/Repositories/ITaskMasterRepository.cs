
using TaskMasterEntity = A360.Wip.Domain.Entities.TaskMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface ITaskMasterRepository : IMongoRepository<TaskMasterEntity>
{
    Task<IReadOnlyCollection<TaskMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskMasterEntity>> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);
}
