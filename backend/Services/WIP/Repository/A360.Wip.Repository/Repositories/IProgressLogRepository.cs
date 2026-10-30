
using ProgressLogEntity = A360.Wip.Domain.Entities.ProgressLog;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IProgressLogRepository : IMongoRepository<ProgressLogEntity>
{
    Task<IReadOnlyCollection<ProgressLogEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProgressLogEntity>> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProgressLogEntity>> GetByTaskIdAsync(string taskId, CancellationToken cancellationToken = default);
}
