
using MaintenanceTaskEntity = A360.Maintenance.Domain.Entities.MaintenanceTask;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IMaintenanceTaskRepository : IMongoRepository<MaintenanceTaskEntity>
{
    Task<IReadOnlyCollection<MaintenanceTaskEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
