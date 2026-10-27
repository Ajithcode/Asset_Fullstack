using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetRegistrationRepository : IMongoRepository<AssetRegistration>
{
    Task<bool> AssetTagExistsAsync(string assetTag, CancellationToken cancellationToken = default);

    Task<AssetRegistration?> GetByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    Task<AssetRegistrationQueryResult> QueryAsync(AssetRegistrationQuery query, CancellationToken cancellationToken = default);
}
