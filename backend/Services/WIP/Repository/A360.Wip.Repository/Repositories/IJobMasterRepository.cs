
using JobMasterEntity = A360.Wip.Domain.Entities.JobMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IJobMasterRepository : IMongoRepository<JobMasterEntity>
{
    Task<IReadOnlyCollection<JobMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
