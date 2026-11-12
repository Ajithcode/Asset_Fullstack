using System.Text.RegularExpressions;
using MongoDB.Bson;
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
                new CreateIndexOptions { Name = "ix_event_logs_created_at_desc" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.Category).Descending(eventLog => eventLog.CreatedAt),
                new CreateIndexOptions { Name = "ix_event_logs_category_created_at" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.Severity),
                new CreateIndexOptions { Name = "ix_event_logs_severity" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.Outcome),
                new CreateIndexOptions { Name = "ix_event_logs_outcome" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.Module),
                new CreateIndexOptions { Name = "ix_event_logs_module" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.IpAddress),
                new CreateIndexOptions { Name = "ix_event_logs_ip_address" }),
            new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Ascending(eventLog => eventLog.AuditVersion),
                new CreateIndexOptions { Name = "ix_event_logs_audit_version" })
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

    public async Task<EventLogQueryResult> QueryAuditAsync(
        AuditLogFilter filter,
        AuditLogSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var mongoFilter = BuildAuditFilter(filter, includeCategory: true);
        var totalCount = await Collection.CountDocumentsAsync(mongoFilter, cancellationToken: cancellationToken);

        var items = await Collection
            .Find(mongoFilter)
            .Sort(BuildAuditSort(sort))
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new EventLogQueryResult(items, totalCount);
    }

    public async Task<IReadOnlyDictionary<string, long>> CountByCategoryAsync(AuditLogFilter filter, CancellationToken cancellationToken = default)
    {
        var groups = await Collection
            .Aggregate()
            .Match(BuildAuditFilter(filter, includeCategory: false))
            .Group(eventLog => eventLog.Category, group => new { Category = group.Key, Count = group.LongCount() })
            .ToListAsync(cancellationToken);

        return groups
            .Where(group => group.Category is not null)
            .ToDictionary(group => group.Category!, group => group.Count, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> DistinctValuesAsync(AuditLogField field, CancellationToken cancellationToken = default)
    {
        FieldDefinition<EventLog, string> definition = field switch
        {
            AuditLogField.User => "performed_by",
            AuditLogField.Module => "module",
            AuditLogField.Action => "action",
            _ => "entity_type"
        };

        var cursor = await Collection.DistinctAsync(definition, Builders<EventLog>.Filter.Empty, cancellationToken: cancellationToken);
        var values = await cursor.ToListAsync(cancellationToken);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> ClassifyOutdatedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        // $not/$gte also matches events that have no audit_version yet
        var outdated = Builders<EventLog>.Filter.Not(Builders<EventLog>.Filter.Gte(eventLog => eventLog.AuditVersion, AuditClassifier.Version));
        var batch = await Collection
            .Find(outdated)
            .Project(Builders<EventLog>.Projection
                .Include(eventLog => eventLog.Id)
                .Include(eventLog => eventLog.ServiceName)
                .Include(eventLog => eventLog.EntityType)
                .Include(eventLog => eventLog.Action))
            .Limit(batchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            return 0;
        }

        var updates = batch.Select(document =>
        {
            var classification = AuditClassifier.Classify(
                StringOf(document, "service_name"),
                StringOf(document, "entity_type"),
                StringOf(document, "action"));

            return new UpdateOneModel<EventLog>(
                Builders<EventLog>.Filter.Eq("_id", document["_id"]),
                Builders<EventLog>.Update
                    .Set(eventLog => eventLog.Category, classification.Category)
                    .Set(eventLog => eventLog.Severity, classification.Severity)
                    .Set(eventLog => eventLog.SeverityRank, AuditSeverity.RankOf(classification.Severity))
                    .Set(eventLog => eventLog.Outcome, classification.Outcome)
                    .Set(eventLog => eventLog.Module, classification.Module)
                    .Set(eventLog => eventLog.AuditVersion, AuditClassifier.Version));
        }).ToList();

        await Collection.BulkWriteAsync(updates, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
        return batch.Count;

        static string? StringOf(BsonDocument document, string name) =>
            document.TryGetValue(name, out var value) && value.IsString ? value.AsString : null;
    }

    private static FilterDefinition<EventLog> BuildAuditFilter(AuditLogFilter filter, bool includeCategory)
    {
        var builder = Builders<EventLog>.Filter;
        var filters = new List<FilterDefinition<EventLog>>();

        if (includeCategory && !string.IsNullOrWhiteSpace(filter.Category))
        {
            filters.Add(builder.Eq(eventLog => eventLog.Category, filter.Category));
        }

        if (filter.From.HasValue)
        {
            filters.Add(builder.Gte(eventLog => eventLog.CreatedAt, filter.From.Value));
        }

        if (filter.To.HasValue)
        {
            // A plain date ("2026-10-28") means the whole day
            filters.Add(filter.To.Value.TimeOfDay == TimeSpan.Zero
                ? builder.Lt(eventLog => eventLog.CreatedAt, filter.To.Value.AddDays(1))
                : builder.Lte(eventLog => eventLog.CreatedAt, filter.To.Value));
        }

        AddEquals(filters, "action", filter.Action);
        AddEquals(filters, "module", filter.Module);
        AddEquals(filters, "severity", filter.Severity);
        AddEquals(filters, "outcome", filter.Outcome);
        AddEquals(filters, "entity_type", filter.EntityType);
        AddEquals(filters, "entity_id", filter.EntityId);

        if (!string.IsNullOrWhiteSpace(filter.User))
        {
            var user = filter.User.Trim();
            filters.Add(builder.Or(
                builder.Eq(eventLog => eventLog.PerformedBy, user),
                builder.Eq(eventLog => eventLog.PerformedByRefId, user),
                builder.Eq(eventLog => eventLog.PerformedByEmail, user)));
        }

        if (!string.IsNullOrWhiteSpace(filter.IpAddress))
        {
            // "10.20.4.18" exactly, or a prefix such as "10.20."
            filters.Add(builder.Regex(eventLog => eventLog.IpAddress, new BsonRegularExpression("^" + Regex.Escape(filter.IpAddress.Trim()))));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(eventLog => eventLog.EntityName, pattern),
                builder.Regex(eventLog => eventLog.Details, pattern),
                builder.Regex(eventLog => eventLog.EntityId, pattern),
                builder.Regex(eventLog => eventLog.EntityType, pattern),
                builder.Regex(eventLog => eventLog.PerformedBy, pattern),
                builder.Regex(eventLog => eventLog.Action, pattern),
                builder.Regex(eventLog => eventLog.IpAddress, pattern)));
        }

        return filters.Count > 0 ? builder.And(filters) : builder.Empty;

        void AddEquals(List<FilterDefinition<EventLog>> target, string field, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Case-insensitive exact match, so "assets" finds "Assets"
                target.Add(builder.Regex(field, new BsonRegularExpression("^" + Regex.Escape(value.Trim()) + "$", "i")));
            }
        }
    }

    private static SortDefinition<EventLog> BuildAuditSort(AuditLogSort sort)
    {
        var builder = Builders<EventLog>.Sort;
        SortDefinition<EventLog> Direction(FieldDefinition<EventLog> field) =>
            sort.Descending ? builder.Descending(field) : builder.Ascending(field);

        var primary = sort.Field switch
        {
            AuditLogSortField.Event => builder.Combine(Direction("entity_type"), Direction("action")),
            AuditLogSortField.Module => Direction("module"),
            AuditLogSortField.User => Direction("performed_by"),
            AuditLogSortField.IpAddress => Direction("ip_address"),
            AuditLogSortField.Severity => Direction("severity_rank"),
            AuditLogSortField.Outcome => Direction("outcome"),
            _ => Direction("created_at")
        };

        return sort.Field == AuditLogSortField.Timestamp
            ? primary
            : builder.Combine(primary, builder.Descending(eventLog => eventLog.CreatedAt));
    }
}
