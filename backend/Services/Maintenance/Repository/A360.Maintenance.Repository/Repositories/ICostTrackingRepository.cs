
using CostTrackingEntity = A360.Maintenance.Domain.Entities.CostTracking;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface ICostTrackingRepository : IMongoRepository<CostTrackingEntity>
{
    Task<IReadOnlyCollection<CostTrackingEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
