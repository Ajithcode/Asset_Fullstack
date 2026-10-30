
using IssueMasterEntity = A360.Wip.Domain.Entities.IssueMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IIssueMasterRepository : IMongoRepository<IssueMasterEntity>
{
    Task<IReadOnlyCollection<IssueMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IssueMasterEntity>> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);
}
