
using ResourceMasterEntity = A360.Wip.Domain.Entities.ResourceMaster;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IResourceMasterRepository : IMongoRepository<ResourceMasterEntity>
{
    Task<IReadOnlyCollection<ResourceMasterEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
