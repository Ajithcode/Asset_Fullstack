using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetDocumentRepository : IMongoRepository<AssetDocumentRecord>
{
    Task<AssetDocumentQueryResult> QueryAsync(AssetDocumentQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the document list. Empty values mean "all".</summary>
public sealed record AssetDocumentQuery(
    string? Search,
    string? AssetRegistrationId,
    string? DocumentType,
    string? LifecycleStage,
    string? Status,
    int Page,
    int PageSize);

public sealed record AssetDocumentQueryResult(
    IReadOnlyCollection<AssetDocumentRecord> Items,
    long TotalCount);
