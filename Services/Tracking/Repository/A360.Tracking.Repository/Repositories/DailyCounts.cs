using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Repository.Repositories;

/// <summary>One bucket of a per-day count: the UTC day (yyyy-MM-dd), an optional group key and the number of records.</summary>
public sealed record DailyCount(string Date, string? Key, int Count);

/// <summary>Counts records per UTC day in Mongo, so timelines never load the records themselves.</summary>
internal static class DailyCounts
{
    /// <param name="dateField">The Bson name of the date field, e.g. "event_time".</param>
    /// <param name="keyField">Optional Bson field to split each day by, e.g. "event_type".</param>
    public static async Task<IReadOnlyList<DailyCount>> CountAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        string dateField,
        DateTime fromUtc,
        DateTime toUtc,
        string? keyField = null,
        CancellationToken cancellationToken = default)
    {
        var groupId = new BsonDocument("date", new BsonDocument("$dateToString", new BsonDocument
        {
            { "format", "%Y-%m-%d" },
            { "date", "$" + dateField }
        }));

        if (keyField is not null)
        {
            groupId.Add("key", "$" + keyField);
        }

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "is_deleted", false },
                { dateField, new BsonDocument { { "$gte", fromUtc }, { "$lt", toUtc } } }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", groupId },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var rows = await collection.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            var id = row["_id"].AsBsonDocument;
            var key = id.TryGetValue("key", out var keyValue) && keyValue.IsString ? keyValue.AsString : null;
            return new DailyCount(id["date"].AsString, key, row["count"].ToInt32());
        }).ToList();
    }
}
