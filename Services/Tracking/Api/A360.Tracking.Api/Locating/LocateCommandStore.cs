using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Locating;

/// <summary>
/// One document per LED / buzzer command in {Locating:CommandCollection} (default "TagLocateCommands"):
/// { reqId, command, tagid, gatewayid, topic, options, request, status, code, error, sentAt, acceptedAt, completedAt, responses[] }.
/// status: sent -> accepted (code 1) -> completed (code 2, error 0) | failed (code 2, error != 0) | timeout.
/// </summary>
public class LocateCommandStore(IMongoDatabase database, IOptions<LocatingSettings> options, ILogger<LocateCommandStore> logger)
{
    private readonly IMongoCollection<BsonDocument> _collection =
        database.GetCollection<BsonDocument>(options.Value.CommandCollection);

    private static readonly FilterDefinitionBuilder<BsonDocument> Filter = Builders<BsonDocument>.Filter;
    private static readonly UpdateDefinitionBuilder<BsonDocument> Update = Builders<BsonDocument>.Update;

    public async Task InsertAsync(LocateCommand command, string gatewayId, string topic, DateTime sentAt, CancellationToken ct)
    {
        await _collection.InsertOneAsync(new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "reqId", command.ReqId },
            { "command", command.Kind },
            { "tagid", command.TagId },
            { "gatewayid", gatewayId },
            { "topic", topic },
            { "options", BsonDocument.Parse(command.Options.ToJsonString()) },
            { "request", BsonDocument.Parse(command.Message.ToJsonString()) },
            { "status", GatewayReply.StatusSent },
            { "sentAt", sentAt },
            { "responses", new BsonArray() },
        }, cancellationToken: ct);
    }

    /// <summary>Records a gateway response against the newest open command with that gateway + req_id.</summary>
    public async Task ApplyResponseAsync(string gatewayId, GatewayReply reply, string raw, DateTime receivedAt)
    {
        var status = reply.Status;
        // Code 1 never overrides a final state; a late code 2 may still turn a timeout into completed / failed.
        string[] from = status == GatewayReply.StatusAccepted
            ? [GatewayReply.StatusSent]
            : [GatewayReply.StatusSent, GatewayReply.StatusAccepted, GatewayReply.StatusTimeout];

        var filter = Filter.And(
            Filter.Eq("gatewayid", gatewayId),
            Filter.Eq("reqId", reply.ReqId),
            Filter.In("status", from));

        var updates = new List<UpdateDefinition<BsonDocument>>
        {
            Update.Push("responses", new BsonDocument
            {
                { "receivedAt", receivedAt },
                { "code", reply.Code.HasValue ? reply.Code.Value : BsonNull.Value },
                { "error", reply.Error.HasValue ? reply.Error.Value : BsonNull.Value },
                { "message", reply.Message is null ? (BsonValue)BsonNull.Value : new BsonString(reply.Message) },
                { "raw", raw },
            }),
        };
        if (status != null)
        {
            updates.Add(Update.Set("status", status));
            updates.Add(Update.Set("code", reply.Code.HasValue ? (BsonValue)reply.Code.Value : BsonNull.Value));
            updates.Add(Update.Set("error", reply.Error.HasValue ? (BsonValue)reply.Error.Value : BsonNull.Value));
            updates.Add(Update.Set("gatewayMessage", reply.Message is null ? (BsonValue)BsonNull.Value : new BsonString(reply.Message)));
            updates.Add(Update.Set(status == GatewayReply.StatusAccepted ? "acceptedAt" : "completedAt", receivedAt));
        }

        try
        {
            await _collection.FindOneAndUpdateAsync(filter, Update.Combine(updates),
                new FindOneAndUpdateOptions<BsonDocument> { Sort = Builders<BsonDocument>.Sort.Descending("sentAt") });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record response req_id {ReqId} from gateway {GatewayId}", reply.ReqId, gatewayId);
        }
    }

    public async Task MarkTimeoutAsync(string gatewayId, int reqId)
    {
        try
        {
            await _collection.UpdateOneAsync(
                Filter.And(
                    Filter.Eq("gatewayid", gatewayId),
                    Filter.Eq("reqId", reqId),
                    Filter.In("status", new[] { GatewayReply.StatusSent, GatewayReply.StatusAccepted })),
                Update.Set("status", GatewayReply.StatusTimeout).Set("completedAt", DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark req_id {ReqId} on gateway {GatewayId} as timed out", reqId, gatewayId);
        }
    }

    public async Task<List<BsonDocument>> RecentForTagAsync(string tagId, int limit, CancellationToken ct) =>
        await _collection.Find(Filter.Eq("tagid", tagId))
            .Sort(Builders<BsonDocument>.Sort.Descending("sentAt"))
            .Project(Builders<BsonDocument>.Projection.Exclude("request").Exclude("responses"))
            .Limit(limit)
            .ToListAsync(ct);

    public async Task EnsureIndexesAsync(CancellationToken ct)
    {
        try
        {
            await _collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("gatewayid").Ascending("reqId").Descending("sentAt")),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tagid").Descending("sentAt")),
            ], ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not create indexes on {Collection}", options.Value.CommandCollection);
        }
    }
}
