
using StatusMasterEntity = A360.Wip.Domain.Entities.StatusMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IStatusMasterRepository : IMongoRepository<StatusMasterEntity>
{
    Task<IReadOnlyCollection<StatusMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
