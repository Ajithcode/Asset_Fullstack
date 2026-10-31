
using PredictiveMaintenanceEntity = A360.Maintenance.Domain.Entities.PredictiveMaintenance;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IPredictiveMaintenanceRepository : IMongoRepository<PredictiveMaintenanceEntity>
{
    Task<IReadOnlyCollection<PredictiveMaintenanceEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
