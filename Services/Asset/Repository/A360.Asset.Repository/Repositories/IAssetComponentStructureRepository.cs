using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetComponentStructureRepository : IMongoRepository<AssetComponentStructureNode>
{
    Task<IReadOnlyCollection<AssetComponentStructureNode>> GetByAssetAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string nodeId, CancellationToken cancellationToken = default);
}
