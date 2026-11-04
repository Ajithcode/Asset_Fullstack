using MongoDB.Bson;
using MongoDB.Driver;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>Creates the target collection as a time-series collection (timeField "timestamp", metaField "meta") if missing.</summary>
public class TimeSeriesCollectionInitializer(IMongoDatabase database, ILogger<TimeSeriesCollectionInitializer> logger)
{
    public async Task<IMongoCollection<BsonDocument>> EnsureAsync(StreamMapping mapping, CancellationToken ct)
    {
        var filter = new BsonDocument("name", mapping.Collection);
        var existing = await (await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter }, ct))
            .FirstOrDefaultAsync(ct);

        if (existing == null)
        {
            var granularity = Enum.Parse<TimeSeriesGranularity>(mapping.Granularity, ignoreCase: true);
            await database.CreateCollectionAsync(mapping.Collection, new CreateCollectionOptions
            {
                TimeSeriesOptions = new TimeSeriesOptions("timestamp", "meta", granularity),
            }, ct);
            logger.LogInformation("Created time-series collection {Collection}", mapping.Collection);
        }
        else if (existing.GetValue("type", "collection") != "timeseries")
        {
            throw new InvalidOperationException(
                $"Collection '{mapping.Collection}' exists but is not a time-series collection.");
        }

        var collection = database.GetCollection<BsonDocument>(mapping.Collection);
        var indexes = mapping.Meta.Keys.Select(key => new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending($"meta.{key}").Descending("timestamp")));
        if (indexes.Any())
            await collection.Indexes.CreateManyAsync(indexes, ct);

        return collection;
    }
}
