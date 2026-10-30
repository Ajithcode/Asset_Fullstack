
using PreventiveMaintenanceEntity = A360.Maintenance.Domain.Entities.PreventiveMaintenance;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IPreventiveMaintenanceRepository : IMongoRepository<PreventiveMaintenanceEntity>
{
    Task<IReadOnlyCollection<PreventiveMaintenanceEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
