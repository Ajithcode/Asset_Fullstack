using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetAuditRepository : IMongoRepository<AssetAudit>
{
    Task<AssetAuditQueryResult> QueryAsync(AssetAuditQuery query, CancellationToken cancellationToken = default);

    Task<bool> ReferenceExistsAsync(string auditReference, string? excludeId, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the audit list. Empty values mean "all".</summary>
/// <param name="Status">Draft, Scheduled, In Progress, Completed or Overdue. Empty lists every audit except drafts.</param>
/// <param name="LocationId">The scope's location.</param>
/// <param name="FromDate">Audits still running on or after this date.</param>
/// <param name="ToDate">Audits starting on or before this date.</param>
/// <param name="Page">Ignored when PageSize is 0, which returns every match (for export).</param>
public sealed record AssetAuditQuery(
    string? Search,
    string? AuditType,
    string? Status,
    string? LocationId,
    DateTime? FromDate,
    DateTime? ToDate,
    DateTime Today,
    int Page,
    int PageSize);

/// <param name="StatusCounts">Audit counts per shown status (Overdue counted apart from Scheduled / In Progress) for the same filters except Status.</param>
public sealed record AssetAuditQueryResult(
    IReadOnlyCollection<AssetAudit> Items,
    long TotalCount,
    IReadOnlyDictionary<string, long> StatusCounts);
