
using AlertMasterEntity = A360.Wip.Domain.Entities.AlertMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IAlertMasterRepository : IMongoRepository<AlertMasterEntity>
{
    Task<IReadOnlyCollection<AlertMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
