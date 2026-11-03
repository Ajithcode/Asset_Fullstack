
using LocationMasterEntity = A360.Wip.Domain.Entities.LocationMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface ILocationMasterRepository : IMongoRepository<LocationMasterEntity>
{
    Task<IReadOnlyCollection<LocationMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
