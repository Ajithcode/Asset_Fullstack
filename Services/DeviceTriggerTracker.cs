using MongoDB.Bson;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Sets mapping.DeviceTriggerField ("in" / "out") on every document from the tag's previous sighting:
///   - tag never seen before                          -> "in"  at this gateway
///   - seen again at the same gateway                 -> toggles: "in" -> "out", "out" -> "in"
///   - seen at another gateway while "in" at the old  -> an extra "out" document for the old gateway
///                                                       (same timestamp), and "in" at the new gateway
///   - seen at another gateway while "out" at the old -> "in" at the new gateway
/// The last state per tag ("gatewayid|in" / "gatewayid|out") is kept in the Redis hash
/// mapping.DeviceTriggerStateKey so it survives restarts. Save the returned changes only after the insert succeeds.
/// </summary>
public class DeviceTriggerTracker(IConnectionMultiplexer redis)
{
    public const string In = "in";
    public const string Out = "out";
    private const string GatewayKey = "gatewayid";
    private const string TagKey = "tagid";

    public record Result(List<BsonDocument> Documents, Dictionary<string, string> Changes);

    public async Task<Result> ApplyAsync(List<BsonDocument> documents, StreamMapping mapping)
    {
        var field = mapping.DeviceTriggerField!;
        // Process in time order (stable, so equal timestamps keep stream order).
        var ordered = documents.OrderBy(d => d["timestamp"].ToUniversalTime()).ToList();

        var tagIds = ordered.Select(TagId).Distinct().ToArray();
        var stored = await redis.GetDatabase().HashGetAsync(mapping.DeviceTriggerStateKey,
            tagIds.Select(t => (RedisValue)t).ToArray());
        var state = new Dictionary<string, (string Gateway, string Trigger)>();
        for (var i = 0; i < tagIds.Length; i++)
            if (Parse(stored[i]) is { } s)
                state[tagIds[i]] = s;

        var result = new List<BsonDocument>(ordered.Count);
        var changes = new Dictionary<string, string>();
        foreach (var doc in ordered)
        {
            var tagId = TagId(doc);
            var gatewayId = doc["meta"][GatewayKey].ToString()!;

            string trigger;
            if (!state.TryGetValue(tagId, out var prev))
                trigger = In;
            else if (prev.Gateway == gatewayId)
                trigger = prev.Trigger == In ? Out : In;
            else
            {
                if (prev.Trigger == In)
                    result.Add(OutDocument(doc, mapping, prev.Gateway));
                trigger = In;
            }

            doc[field] = trigger;
            result.Add(doc);
            state[tagId] = (gatewayId, trigger);
            changes[tagId] = $"{gatewayId}|{trigger}";
        }

        return new Result(result, changes);
    }

    public Task SaveAsync(StreamMapping mapping, Dictionary<string, string> changes) =>
        changes.Count == 0
            ? Task.CompletedTask
            : redis.GetDatabase().HashSetAsync(mapping.DeviceTriggerStateKey,
                changes.Select(c => new HashEntry(c.Key, c.Value)).ToArray());

    // "out" for the gateway the tag left: same timestamp and tag, the old gateway, the other fields null
    // (rssi etc. were measured by the new gateway, not the old one).
    private static BsonDocument OutDocument(BsonDocument doc, StreamMapping mapping, string previousGateway)
    {
        var meta = doc["meta"].AsBsonDocument.DeepClone().AsBsonDocument;
        meta[GatewayKey] = previousGateway;
        var outDoc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "timestamp", doc["timestamp"] },
            { "meta", meta },
        };
        foreach (var f in mapping.Fields)
            outDoc[f.Name] = BsonNull.Value;
        outDoc[mapping.DeviceTriggerField!] = Out;
        return outDoc;
    }

    private static string TagId(BsonDocument doc) => doc["meta"][TagKey].ToString()!;

    private static (string, string)? Parse(RedisValue value)
    {
        if (value.IsNullOrEmpty)
            return null;
        var s = value.ToString();
        var i = s.LastIndexOf('|');
        return i > 0 ? (s[..i], s[(i + 1)..]) : null;
    }
}
