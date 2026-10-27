using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetConditionAssessmentRepository : IMongoRepository<AssetConditionAssessment>
{
    Task<AssetConditionAssessmentQueryResult> QueryAsync(AssetConditionAssessmentQuery query, CancellationToken cancellationToken = default);

    /// <summary>The asset's latest assessment by date, or null when it has none.</summary>
    Task<AssetConditionAssessment?> GetLatestAsync(string assetRegistrationId, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the assessment history. Empty values mean "all".</summary>
public sealed record AssetConditionAssessmentQuery(
    string? AssetRegistrationId,
    string? OverallCondition,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page,
    int PageSize);

public sealed record AssetConditionAssessmentQueryResult(
    IReadOnlyCollection<AssetConditionAssessment> Items,
    long TotalCount);
