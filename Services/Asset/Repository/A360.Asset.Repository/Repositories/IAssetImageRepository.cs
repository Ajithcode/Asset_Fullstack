using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetImageRepository : IMongoRepository<AssetImage>
{
    Task<AssetImageQueryResult> QueryAsync(AssetImageQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every image of one asset that is not deleted, newest first.</summary>
    /// <summary>Image types, tags and uploaders in use, for the filter dropdowns; one asset's when an id is given.</summary>
    Task<IReadOnlyCollection<AssetImage>> GetFilterFieldsAsync(string? assetRegistrationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssetImage>> GetByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the image list. Empty values mean "all".</summary>
public sealed record AssetImageQuery(
    string? Search,
    string? AssetRegistrationId,
    string? ImageType,
    int Page,
    int PageSize,
    string? Tag = null,
    string? UploadedBy = null,
    DateTime? UploadedFrom = null,
    DateTime? UploadedTo = null);

public sealed record AssetImageQueryResult(
    IReadOnlyCollection<AssetImage> Items,
    long TotalCount);
