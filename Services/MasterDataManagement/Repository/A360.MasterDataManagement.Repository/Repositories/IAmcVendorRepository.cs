using AmcVendorEntity = A360.MasterDataManagement.Domain.Entities.AmcVendor;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IAmcVendorRepository : IMongoRepository<AmcVendorEntity>
{
    Task<IReadOnlyCollection<AmcVendorEntity>> SearchAsync(AmcVendorFilter filter, CancellationToken cancellationToken = default);

    Task<AmcVendorEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithAssetTypeAsync(string assetTypeId, CancellationToken cancellationToken = default);

    Task UpdateAssetTypeNameAsync(string assetTypeId, string assetTypeName, CancellationToken cancellationToken = default);
}

public sealed record AmcVendorFilter(
    string? Search,
    string? Status,
    string? AssetTypeId,
    string? ContractType,
    // Only contracts that cover this day.
    DateOnly? ActiveOn,
    // Only contracts ending on or before this day.
    DateOnly? EndsBy,
    string? ClientId);
