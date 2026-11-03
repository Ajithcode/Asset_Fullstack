using MongoDB.Driver;
using A360.Domain.Entities;

namespace A360.Repository.Repositories;

public sealed class EventLogRepository : MongoRepository<EventLog>, IEventLogRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "event_logs";

    public EventLogRepository(IMongoDatabase database)
        : base(database.GetCollection<EventLog>(CollectionName))
    {
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.ServiceName),
                new CreateIndexOptions { Name = "ix_event_logs_service_name" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.EntityType),
                new CreateIndexOptions { Name = "ix_event_logs_entity_type" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.EntityId),
                new CreateIndexOptions { Name = "ix_event_logs_entity_id" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.Action),
                new CreateIndexOptions { Name = "ix_event_logs_action" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.PerformedBy),
                new CreateIndexOptions { Name = "ix_event_logs_performed_by" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Descending(eventLog => eventLog.CreatedAt),
                new CreateIndexOptions { Name = "ix_event_logs_created_at_desc" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task<EventLogQueryResult> QueryAsync(EventLogQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<EventLog>>();

        if (!string.IsNullOrWhiteSpace(query.ServiceName))
        {
            filters.Add(Builders<EventLog>.Filter.Eq(eventLog => eventLog.ServiceName, query.ServiceName));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            filters.Add(Builders<EventLog>.Filter.Eq(eventLog => eventLog.EntityType, query.EntityType));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            filters.Add(Builders<EventLog>.Filter.Eq(eventLog => eventLog.EntityId, query.EntityId));
        }

        if (!string.IsNullOrWhiteSpace(query.PerformedBy))
        {
            filters.Add(Builders<EventLog>.Filter.Eq(eventLog => eventLog.PerformedBy, query.PerformedBy));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            filters.Add(Builders<EventLog>.Filter.Eq(eventLog => eventLog.Action, query.Action));
        }

        if (query.From.HasValue)
        {
            filters.Add(Builders<EventLog>.Filter.Gte(eventLog => eventLog.CreatedAt, query.From.Value));
        }

        if (query.To.HasValue)
        {
            filters.Add(Builders<EventLog>.Filter.Lte(eventLog => eventLog.CreatedAt, query.To.Value));
        }

        var filter = filters.Count > 0 ? Builders<EventLog>.Filter.And(filters) : Builders<EventLog>.Filter.Empty;

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize;

        var items = await Collection
            .Find(filter)
            .SortByDescending(eventLog => eventLog.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new EventLogQueryResult(items, totalCount);
    }
}
