using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class TagEventRepository
    : MongoRepository<TagEvent>, ITagEventRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "TagEvent";

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public TagEventRepository(IMongoDatabase database)
        : base(database.GetCollection<TagEvent>(CollectionName))
    {
    }

    public async Task<(IReadOnlyList<TagEvent> Items, long Total)> QueryAsync(
        TagEventQuery query,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TagEvent>.Filter;
        var match = filter.Eq(tagEvent => tagEvent.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            match &= filter.Or(
                filter.Regex(tagEvent => tagEvent.TagId, pattern),
                filter.Regex(tagEvent => tagEvent.Details, pattern),
                filter.Regex(tagEvent => tagEvent.AssetName, pattern),
                filter.Regex(tagEvent => tagEvent.AssetCode, pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            match &= filter.Eq(tagEvent => tagEvent.EventType, query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(query.Technology))
        {
            match &= filter.Eq(tagEvent => tagEvent.Technology, query.Technology);
        }

        if (!string.IsNullOrWhiteSpace(query.PerformedBy))
        {
            match &= filter.Eq(tagEvent => tagEvent.PerformedBy, query.PerformedBy.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.TagId))
        {
            match &= filter.Eq(tagEvent => tagEvent.TagId, query.TagId.Trim());
        }

        if (query.FromUtc is { } fromUtc)
        {
            match &= filter.Gte(tagEvent => tagEvent.OccurredAt, fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            match &= filter.Lt(tagEvent => tagEvent.OccurredAt, toUtc);
        }

        var options = new FindOptions { Collation = CaseInsensitive };
        var total = await Collection.CountDocumentsAsync(match, new CountOptions { Collation = CaseInsensitive }, cancellationToken);
        var items = await Collection
            .Find(match, options)
            .SortByDescending(tagEvent => tagEvent.OccurredAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<TagEvent>> GetByTagIdAsync(string tagId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(tagEvent => tagEvent.TagId == tagId && !tagEvent.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .SortByDescending(tagEvent => tagEvent.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPerformersAsync(CancellationToken cancellationToken = default)
    {
        var performers = await Collection
            .Distinct(tagEvent => tagEvent.PerformedBy, tagEvent => !tagEvent.IsDeleted, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return performers
            .Where(performer => !string.IsNullOrWhiteSpace(performer))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetStatusesAtAsync(
        DateTime beforeUtc,
        CancellationToken cancellationToken = default)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "is_deleted", false },
                { "occurred_at", new BsonDocument("$lt", beforeUtc) }
            }),
            new BsonDocument("$sort", new BsonDocument("occurred_at", -1)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$toUpper", "$tag_id") },
                { "status", new BsonDocument("$first", "$status_after") }
            })
        };

        var rows = await Collection.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row["_id"].AsString,
            row => row.TryGetValue("status", out var status) && status.IsString ? status.AsString : null,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TagEvent>(
                Builders<TagEvent>.IndexKeys.Descending(tagEvent => tagEvent.OccurredAt),
                new CreateIndexOptions { Name = "ix_tag_event_occurred_at" }),
            new CreateIndexModel<TagEvent>(
                Builders<TagEvent>.IndexKeys.Ascending(tagEvent => tagEvent.TagId).Descending(tagEvent => tagEvent.OccurredAt),
                new CreateIndexOptions { Name = "ix_tag_event_tag_id", Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
