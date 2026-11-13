using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetDocumentRepository : IMongoRepository<AssetDocumentRecord>
{
    Task<AssetDocumentQueryResult> QueryAsync(AssetDocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every document of one asset that is not deleted, newest first.</summary>
    /// <summary>Document types and lifecycle stages in use, for the filter dropdowns; one asset's when an id is given.</summary>
    Task<IReadOnlyCollection<AssetDocumentRecord>> GetFilterFieldsAsync(string? assetRegistrationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssetDocumentRecord>> GetByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the document list. Empty values mean "all".</summary>
public sealed record AssetDocumentQuery(
    string? Search,
    string? AssetRegistrationId,
    string? DocumentType,
    string? LifecycleStage,
    string? Status,
    int Page,
    int PageSize,
    DateTime? Today = null);

/// <summary>
/// Status shown for a document: Inactive when set so, Expired once an active document's expiry date has passed,
/// otherwise Active.
/// </summary>
public static class AssetDocumentEffectiveStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Expired = "Expired";

    public static string Of(AssetDocumentRecord document, DateTime today)
    {
        if (string.Equals(document.Status, Inactive, StringComparison.OrdinalIgnoreCase))
        {
            return Inactive;
        }

        return document.ExpiryDate is not null && document.ExpiryDate.Value.Date < today.Date ? Expired : document.Status ?? Active;
    }
}

public sealed record AssetDocumentQueryResult(
    IReadOnlyCollection<AssetDocumentRecord> Items,
    long TotalCount);
