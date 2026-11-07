using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Reads each configured Redis stream through a consumer group and inserts the entries into its
/// time-series collection. Entries are XACKed only after the Mongo insert succeeds, so delivery is
/// at-least-once: a crash between insert and ack can produce duplicates (time-series collections
/// cannot enforce a unique index). With DeleteAfterAck, acked entries are then XDELed from the stream.
/// </summary>
public class RedisStreamIngestWorker(
    IConnectionMultiplexer redis,
    TimeSeriesCollectionInitializer initializer,
    IOptions<RedisSettings> redisSettings,
    IOptions<List<StreamMapping>> mappings,
    ILogger<RedisStreamIngestWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (mappings.Value.Count == 0)
            logger.LogWarning("No StreamMappings configured; nothing to do.");

        return Task.WhenAll(mappings.Value.Select(m => RunMappingAsync(m, stoppingToken)));
    }

    private async Task RunMappingAsync(StreamMapping mapping, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var consumer = redisSettings.Value.ConsumerName;
        IMongoCollection<BsonDocument>? collection = null;

        // Re-deliver this consumer's un-acked entries first ("0"), then switch to new entries (">").
        var readPosition = "0";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (collection == null)
                {
                    await EnsureGroupAsync(db, mapping);
                    collection = await initializer.EnsureAsync(mapping, ct);
                    logger.LogInformation("Ingesting stream {Stream} -> {Collection} (group {Group}, consumer {Consumer})",
                        mapping.Stream, mapping.Collection, mapping.Group, consumer);
                }

                var entries = await db.StreamReadGroupAsync(mapping.Stream, mapping.Group, consumer, readPosition, mapping.BatchSize);
                if (entries.Length == 0)
                {
                    if (readPosition == "0")
                        readPosition = ">";
                    else
                        await Task.Delay(IdleDelay, ct);
                    continue;
                }

                await WriteBatchAsync(db, collection, mapping, entries, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (RedisServerException ex) when (ex.Message.StartsWith("NOGROUP"))
            {
                // Stream or group was deleted under us; recreate on the next pass.
                logger.LogWarning("Consumer group {Group} on {Stream} is gone; recreating", mapping.Group, mapping.Stream);
                collection = null;
                readPosition = "0";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ingesting stream {Stream}; retrying in {Delay}", mapping.Stream, ErrorDelay);
                // Retry un-acked entries before reading new ones.
                readPosition = "0";
                await Task.Delay(ErrorDelay, ct);
            }
        }
    }

    private async Task WriteBatchAsync(IDatabase db, IMongoCollection<BsonDocument> collection,
        StreamMapping mapping, StreamEntry[] entries, CancellationToken ct)
    {
        var documents = new List<BsonDocument>(entries.Length);
        var ackIds = new List<RedisValue>(entries.Length);

        foreach (var entry in entries)
        {
            // When re-reading pending entries, an entry deleted from the stream (e.g. by XTRIM) comes back with no values.
            if (entry.IsNull || entry.Values.Length == 0)
            {
                ackIds.Add(entry.Id);
                continue;
            }

            try
            {
                documents.AddRange(StreamEntryMapper.ToDocuments(entry, mapping));
            }
            catch (Exception ex)
            {
                // A malformed entry would otherwise be retried forever; log it and drop it.
                logger.LogError(ex, "Skipping unmappable entry {Id} on {Stream}", entry.Id, mapping.Stream);
            }
            ackIds.Add(entry.Id);
        }

        if (documents.Count > 0)
        {
            // Re-check before every insert: if the collection was dropped, inserting would create a normal collection.
            collection = await initializer.EnsureAsync(mapping, ct);
            await collection.InsertManyAsync(documents, new InsertManyOptions { IsOrdered = false }, ct);
        }

        await db.StreamAcknowledgeAsync(mapping.Stream, mapping.Group, ackIds.ToArray());
        if (mapping.DeleteAfterAck)
            await db.StreamDeleteAsync(mapping.Stream, ackIds.ToArray());
        logger.LogInformation("Stored {Count} entries from {Stream} in {Collection}", documents.Count, mapping.Stream, mapping.Collection);
    }

    private async Task EnsureGroupAsync(IDatabase db, StreamMapping mapping)
    {
        try
        {
            await db.StreamCreateConsumerGroupAsync(mapping.Stream, mapping.Group, mapping.StartId, createStream: true);
            logger.LogInformation("Created consumer group {Group} on {Stream} starting at {StartId}",
                mapping.Group, mapping.Stream, mapping.StartId);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            // Group already exists; continue from its last delivered position.
        }
    }
}
