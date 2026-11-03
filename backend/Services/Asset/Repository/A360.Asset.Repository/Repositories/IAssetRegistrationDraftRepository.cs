using System.Linq.Expressions;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetRegistrationDraftRepository : IMongoRepository<AssetRegistrationDraft>
{
    Task<AssetRegistrationDraft?> GetByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    /// <summary>Drafts newest first; limited to the given creator when one is supplied.</summary>
    Task<IReadOnlyCollection<AssetRegistrationDraft>> GetRecentAsync(string? createdBy, CancellationToken cancellationToken = default);

    Task<bool> AssetTagExistsAsync(string assetTag, string? excludeAssetRegistrationId, CancellationToken cancellationToken = default);

    /// <summary>Replaces one step's section and moves CurrentStep forward to that step if it is further.</summary>
    Task<AssetRegistrationDraft?> UpdateSectionAsync<TSection>(
        string assetRegistrationId,
        Expression<Func<AssetRegistrationDraft, TSection?>> section,
        TSection value,
        int step,
        string? updatedBy,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default);
}
