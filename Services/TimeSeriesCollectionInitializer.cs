using MongoDB.Bson;
using MongoDB.Driver;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Creates the target collection as a time-series collection (timeField "timestamp", metaField "meta") if missing.
/// MongoDB silently creates a normal collection when inserting into a missing one, so the worker calls
/// <see cref="EnsureAsync"/> before every insert: if the collection was dropped, it is recreated as time-series.
/// </summary>
public class TimeSeriesCollectionInitializer(IMongoDatabase database, ILogger<TimeSeriesCollectionInitializer> logger)
{
    public async Task<IMongoCollection<BsonDocument>> EnsureAsync(StreamMapping mapping, CancellationToken ct)
    {
        var filter = new BsonDocument("name", mapping.Collection);
        var existing = await (await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter }, ct))
            .FirstOrDefaultAsync(ct);
        var collection = database.GetCollection<BsonDocument>(mapping.Collection);

        if (existing != null && existing.GetValue("type", "collection") == "timeseries")
            return collection;

        if (existing == null)
        {
            var granularity = Enum.Parse<TimeSeriesGranularity>(mapping.Granularity, ignoreCase: true);
            await database.CreateCollectionAsync(mapping.Collection, new CreateCollectionOptions
            {
                TimeSeriesOptions = new TimeSeriesOptions("timestamp", "meta", granularity),
            }, ct);
            logger.LogInformation("Created time-series collection {Collection}", mapping.Collection);
        }
        else
        {
            throw new InvalidOperationException(
                $"Collection '{mapping.Collection}' exists but is not a time-series collection.");
        }

        // Only reached right after creating the collection, so indexes are built once per (re)creation.
        var indexes = mapping.Meta.Keys.Select(key => new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending($"meta.{key}").Descending("timestamp")));
        if (indexes.Any())
            await collection.Indexes.CreateManyAsync(indexes, ct);

        return collection;
    }
}
