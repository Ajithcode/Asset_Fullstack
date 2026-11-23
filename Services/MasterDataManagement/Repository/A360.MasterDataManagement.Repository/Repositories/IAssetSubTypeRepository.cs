using A360.Repository.Repositories;
using A360.MasterDataManagement.Domain.Entities;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IAssetSubTypeRepository : IMongoRepository<AssetSubType>
{
    Task<IReadOnlyCollection<AssetSubType>> SearchAsync(AssetSubTypeFilter filter, CancellationToken cancellationToken = default);

    Task<AssetSubType?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithAssetTypeAsync(string assetTypeId, CancellationToken cancellationToken = default);

    Task UpdateAssetTypeNameAsync(string assetTypeId, string assetTypeName, CancellationToken cancellationToken = default);
}

public sealed record AssetSubTypeFilter(
    string? Search,
    string? Status,
    string? AssetTypeId,
    string? ClientId);
