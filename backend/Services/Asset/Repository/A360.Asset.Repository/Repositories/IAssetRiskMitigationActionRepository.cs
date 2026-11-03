using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetRiskMitigationActionRepository : IMongoRepository<AssetRiskMitigationAction>
{
    Task<AssetRiskMitigationActionQueryResult> QueryAsync(AssetRiskMitigationActionQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the Risk Mitigation Actions list. Empty values mean "all".</summary>
public sealed record AssetRiskMitigationActionQuery(
    string? AssetRegistrationId,
    string? Status,
    int Page,
    int PageSize);

/// <param name="OpenCount">Actions not Completed or Cancelled, for the "(3 open)" header.</param>
public sealed record AssetRiskMitigationActionQueryResult(
    IReadOnlyCollection<AssetRiskMitigationAction> Items,
    long TotalCount,
    long OpenCount);
