using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetRelationshipRepository : IMongoRepository<AssetRelationship>
{
    Task<AssetRelationshipQueryResult> QueryAsync(AssetRelationshipQuery query, CancellationToken cancellationToken = default);

    Task<AssetRelationship?> GetByMainAssetAsync(string mainAssetRegistrationId, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the relationship list. Empty values mean "all".</summary>
/// <param name="AssetRegistrationId">Matches the main asset or any of its sub assets.</param>
public sealed record AssetRelationshipQuery(
    string? Search,
    string? AssetRegistrationId,
    string? RelationshipType,
    int Page,
    int PageSize);

/// <param name="TotalSubAssetLinks">Sub asset links across every matching main asset, for the header count.</param>
public sealed record AssetRelationshipQueryResult(
    IReadOnlyCollection<AssetRelationship> Items,
    long TotalCount,
    long TotalSubAssetLinks);
