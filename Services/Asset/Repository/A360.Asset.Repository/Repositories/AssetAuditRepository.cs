using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetAuditRepository
    : MongoRepository<AssetAudit>, IAssetAuditRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetAudit";

    public const string DraftStatus = "Draft";
    public const string ScheduledStatus = "Scheduled";
    public const string InProgressStatus = "In Progress";
    public const string CompletedStatus = "Completed";

    // Shown for a Scheduled or In Progress audit whose end date has passed; never stored.
    public const string OverdueStatus = "Overdue";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetAuditRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetAudit>(CollectionName))
    {
    }

    /// <summary>The status to show: Overdue for an open audit past its end date, else the stored status.</summary>
    public static string? DisplayStatus(AssetAudit audit, DateTime today) =>
        audit.Status is ScheduledStatus or InProgressStatus && audit.Schedule.EndDate < today
            ? OverdueStatus
            : audit.Status;

    public async Task<AssetAuditQueryResult> QueryAsync(AssetAuditQuery query, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetAudit>.Filter;
        var filters = new List<FilterDefinition<AssetAudit>>
        {
            builder.Eq(audit => audit.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(audit => audit.AuditReference, pattern),
                builder.Regex(audit => audit.AuditName, pattern),
                builder.Regex("scope.location_name", pattern),
                builder.Regex("team.name", pattern)));
        }

        if (!string.IsNullOrWhiteSpace(query.AuditType))
        {
            filters.Add(builder.Regex(audit => audit.AuditType, new BsonRegularExpression($"^{Regex.Escape(query.AuditType.Trim())}$", "i")));
        }

        if (!string.IsNullOrWhiteSpace(query.LocationId))
        {
            filters.Add(builder.Eq("scope.location_id", query.LocationId.Trim()));
        }

        if (query.FromDate is not null)
        {
            filters.Add(builder.Or(builder.Eq("schedule.end_date", BsonNull.Value), builder.Gte("schedule.end_date", query.FromDate)));
        }

        if (query.ToDate is not null)
        {
            filters.Add(builder.Lte("schedule.start_date", query.ToDate));
        }

        // Status is applied after the counts, so the summary cards keep every status while one is selected.
        var withoutStatus = builder.And(filters);
        var filter = builder.And(withoutStatus, StatusFilter(query.Status, query.Today));

        var find = Collection
            .Find(filter)
            .Sort(Builders<AssetAudit>.Sort.Descending("schedule.start_date").Descending(audit => audit.CreatedAt).Ascending(audit => audit.Id));

        if (query.PageSize > 0)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize > MaxPageSize ? DefaultPageSize : query.PageSize;
            find = find.Skip((page - 1) * pageSize).Limit(pageSize);
        }

        var items = await find.ToListAsync(cancellationToken);
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var statuses = await Collection
            .Find(withoutStatus)
            .Project(audit => new AssetAudit { Status = audit.Status, Schedule = new AssetAuditSchedule { EndDate = audit.Schedule.EndDate } })
            .ToListAsync(cancellationToken);

        var statusCounts = statuses
            .GroupBy(audit => DisplayStatus(audit, query.Today) ?? string.Empty)
            .ToDictionary(group => group.Key, group => (long)group.Count());

        return new AssetAuditQueryResult(items, totalCount, statusCounts);
    }

    public async Task<bool> ReferenceExistsAsync(string auditReference, string? excludeId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(audit => audit.AuditReference == auditReference && audit.Id != excludeId && !audit.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    private static FilterDefinition<AssetAudit> StatusFilter(string? status, DateTime today)
    {
        var builder = Builders<AssetAudit>.Filter;
        var open = builder.In(audit => audit.Status, [ScheduledStatus, InProgressStatus]);
        var pastEnd = builder.Lt("schedule.end_date", today);

        return status?.Trim() switch
        {
            null or "" => builder.Ne(audit => audit.Status, DraftStatus),
            var value when value.Equals(OverdueStatus, StringComparison.OrdinalIgnoreCase) => builder.And(open, pastEnd),
            var value when value.Equals(ScheduledStatus, StringComparison.OrdinalIgnoreCase)
                || value.Equals(InProgressStatus, StringComparison.OrdinalIgnoreCase) =>
                builder.And(
                    builder.Regex(audit => audit.Status, new BsonRegularExpression($"^{Regex.Escape(value)}$", "i")),
                    builder.Not(pastEnd)),
            var value => builder.Regex(audit => audit.Status, new BsonRegularExpression($"^{Regex.Escape(value)}$", "i"))
        };
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetAudit>(
                Builders<AssetAudit>.IndexKeys.Ascending(audit => audit.AuditReference),
                new CreateIndexOptions { Name = "ix_asset_audit_audit_reference" }),
            new CreateIndexModel<AssetAudit>(
                Builders<AssetAudit>.IndexKeys.Ascending(audit => audit.Status).Ascending("schedule.end_date"),
                new CreateIndexOptions { Name = "ix_asset_audit_status_end_date" }),
            new CreateIndexModel<AssetAudit>(
                Builders<AssetAudit>.IndexKeys.Descending("schedule.start_date"),
                new CreateIndexOptions { Name = "ix_asset_audit_start_date" }),
            new CreateIndexModel<AssetAudit>(
                Builders<AssetAudit>.IndexKeys.Ascending("scope.location_id"),
                new CreateIndexOptions { Name = "ix_asset_audit_location_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
