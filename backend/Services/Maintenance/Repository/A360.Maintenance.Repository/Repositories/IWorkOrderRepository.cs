
using WorkOrderEntity = A360.Maintenance.Domain.Entities.WorkOrder;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IWorkOrderRepository : IMongoRepository<WorkOrderEntity>
{
    Task<IReadOnlyCollection<WorkOrderEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
