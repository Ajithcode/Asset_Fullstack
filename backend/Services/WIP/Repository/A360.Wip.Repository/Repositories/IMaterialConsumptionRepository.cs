
using MaterialConsumptionEntity = A360.Wip.Domain.Entities.MaterialConsumption;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IMaterialConsumptionRepository : IMongoRepository<MaterialConsumptionEntity>
{
    Task<IReadOnlyCollection<MaterialConsumptionEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MaterialConsumptionEntity>> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);
}
