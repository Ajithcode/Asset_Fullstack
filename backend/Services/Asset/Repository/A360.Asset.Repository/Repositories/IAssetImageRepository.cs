using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetImageRepository : IMongoRepository<AssetImage>
{
    Task<AssetImageQueryResult> QueryAsync(AssetImageQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the image list. Empty values mean "all".</summary>
public sealed record AssetImageQuery(
    string? Search,
    string? AssetRegistrationId,
    string? ImageType,
    int Page,
    int PageSize);

public sealed record AssetImageQueryResult(
    IReadOnlyCollection<AssetImage> Items,
    long TotalCount);
