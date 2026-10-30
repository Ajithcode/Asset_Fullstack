
using KpiMasterEntity = A360.Wip.Domain.Entities.KpiMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IKpiMasterRepository : IMongoRepository<KpiMasterEntity>
{
    Task<IReadOnlyCollection<KpiMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
