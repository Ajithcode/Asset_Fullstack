
using SlaMasterEntity = A360.Wip.Domain.Entities.SlaMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface ISlaMasterRepository : IMongoRepository<SlaMasterEntity>
{
    Task<IReadOnlyCollection<SlaMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
