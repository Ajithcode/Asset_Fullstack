using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetImportExportJobRepository : IMongoRepository<AssetImportExportJob>
{
    /// <summary>Import History: newest first, without the file rows.</summary>
    Task<AssetImportExportHistoryResult> QueryHistoryAsync(AssetImportExportHistoryQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Empty values mean "all".</summary>
/// <param name="Type">"Import" or "Export".</param>
/// <param name="IncludeInProgress">When false (and no Status is given), only Completed and Failed runs are listed.</param>
public sealed record AssetImportExportHistoryQuery(string? Type, string? Status, string? Search, bool IncludeInProgress, int Page, int PageSize);

public sealed record AssetImportExportHistoryResult(IReadOnlyCollection<AssetImportExportJob> Items, long TotalCount);
