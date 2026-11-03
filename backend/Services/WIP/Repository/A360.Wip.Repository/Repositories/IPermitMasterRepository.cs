
using PermitMasterEntity = A360.Wip.Domain.Entities.PermitMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IPermitMasterRepository : IMongoRepository<PermitMasterEntity>
{
    Task<IReadOnlyCollection<PermitMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PermitMasterEntity>> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);
}
