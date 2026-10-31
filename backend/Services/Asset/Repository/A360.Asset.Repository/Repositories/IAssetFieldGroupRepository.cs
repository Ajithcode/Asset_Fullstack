using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetFieldGroupRepository : IMongoRepository<AssetFieldGroup>
{
    /// <summary>Group names are unique, ignoring case.</summary>
    Task<AssetFieldGroup?> GetByNameAsync(string groupName, CancellationToken cancellationToken = default);
}
