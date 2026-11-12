using A360.Domain.Entities;

namespace A360.Repository.Repositories;

public interface IEventLogRepository : IMongoRepository<EventLog>
{
    Task<EventLogQueryResult> QueryAsync(EventLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>System Audit Log page: filtered, sorted and paged.</summary>
    Task<EventLogQueryResult> QueryAuditAsync(AuditLogFilter filter, AuditLogSort sort, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Events per <see cref="AuditCategory"/> for the tab counts; <see cref="AuditLogFilter.Category"/> is ignored.</summary>
    Task<IReadOnlyDictionary<string, long>> CountByCategoryAsync(AuditLogFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Distinct non-empty values of a field (users, modules, actions) for the filter dropdowns.</summary>
    Task<IReadOnlyList<string>> DistinctValuesAsync(AuditLogField field, CancellationToken cancellationToken = default);

    /// <summary>Classifies up to <paramref name="batchSize"/> events written before the audit fields existed (or with older rules).</summary>
    Task<int> ClassifyOutdatedAsync(int batchSize, CancellationToken cancellationToken = default);
}

public sealed record AuditLogFilter(
    string? Category = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Action = null,
    string? Module = null,
    string? User = null,
    string? Severity = null,
    string? IpAddress = null,
    string? Outcome = null,
    string? Search = null,
    string? EntityType = null,
    string? EntityId = null);

public enum AuditLogSortField
{
    Timestamp,
    Event,
    Module,
    User,
    IpAddress,
    Severity,
    Outcome
}

public sealed record AuditLogSort(AuditLogSortField Field = AuditLogSortField.Timestamp, bool Descending = true);

public enum AuditLogField
{
    User,
    Module,
    Action,
    EntityType
}

public sealed record EventLogQuery(
    string? ServiceName,
    string? EntityType,
    string? EntityId,
    string? Action,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize,
    string? PerformedBy = null);

public sealed record EventLogQueryResult(IReadOnlyCollection<EventLog> Items, long TotalCount);
