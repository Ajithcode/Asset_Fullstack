
using ChecklistMasterEntity = A360.Wip.Domain.Entities.ChecklistMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IChecklistMasterRepository : IMongoRepository<ChecklistMasterEntity>
{
    Task<IReadOnlyCollection<ChecklistMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
