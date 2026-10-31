using A360.Domain.Entities;

namespace A360.Repository.Repositories;

public interface IEventLogRepository : IMongoRepository<EventLog>
{
    Task<EventLogQueryResult> QueryAsync(EventLogQuery query, CancellationToken cancellationToken = default);
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
