using MongoDB.Bson;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Tracks which gateway (zone) each tag is in and adds to every document, right after mapping.DeviceTriggerField:
///   device_trigger "in" / "out", zone_in (date), zone_out (date), time_spent (minutes).
/// Rules per tag:
///   - first sighting, or again at the same gateway -> "in", zone_in = when the tag first came in at this gateway,
///                                                     zone_out = null, time_spent = null
///   - seen at another gateway                      -> an extra "out" document for the previous gateway at this
///                                                     timestamp (zone_in = its first in, zone_out = this timestamp,
///                                                     time_spent = zone_out - zone_in in minutes), and "in" at the
///                                                     new gateway with zone_in = this timestamp
/// Each tag's current gateway and zone_in ("gatewayid|unixMs") are kept in the Redis hash
/// mapping.DeviceTriggerStateKey so they survive restarts. Save the returned changes only after the insert succeeds.
/// </summary>
public class DeviceTriggerTracker(IConnectionMultiplexer redis)
{
    public const string In = "in";
    public const string Out = "out";
    public const string ZoneIn = "zone_in";
    public const string ZoneOut = "zone_out";
    public const string TimeSpent = "time_spent";
    private const string GatewayKey = "gatewayid";
    private const string TagKey = "tagid";

    public record Result(List<BsonDocument> Documents, Dictionary<string, string> Changes);

    private record Zone(string Gateway, DateTime Since);

    public async Task<Result> ApplyAsync(List<BsonDocument> documents, StreamMapping mapping)
    {
        // Process in time order (stable, so equal timestamps keep stream order).
        var ordered = documents.OrderBy(Time).ToList();

        var tagIds = ordered.Select(TagId).Distinct().ToArray();
        var stored = await redis.GetDatabase().HashGetAsync(mapping.DeviceTriggerStateKey,
            tagIds.Select(t => (RedisValue)t).ToArray());
        var zones = new Dictionary<string, Zone>();
        for (var i = 0; i < tagIds.Length; i++)
            if (Parse(stored[i]) is { } z)
                zones[tagIds[i]] = z;

        var result = new List<BsonDocument>(ordered.Count);
        var changes = new Dictionary<string, string>();
        foreach (var doc in ordered)
        {
            var tagId = TagId(doc);
            var gatewayId = doc["meta"][GatewayKey].ToString()!;
            var time = Time(doc);

            if (!zones.TryGetValue(tagId, out var zone) || zone.Gateway != gatewayId)
            {
                if (zone != null)
                    result.Add(OutDocument(doc, mapping, zone, time));
                zone = new Zone(gatewayId, time);
                zones[tagId] = zone;
                changes[tagId] = $"{gatewayId}|{new DateTimeOffset(time).ToUnixTimeMilliseconds()}";
            }

            SetTrigger(doc, mapping, In, zone.Since, null);
            result.Add(doc);
        }

        return new Result(result, changes);
    }

    public Task SaveAsync(StreamMapping mapping, Dictionary<string, string> changes) =>
        changes.Count == 0
            ? Task.CompletedTask
            : redis.GetDatabase().HashSetAsync(mapping.DeviceTriggerStateKey,
                changes.Select(c => new HashEntry(c.Key, c.Value)).ToArray());

    // "out" for the gateway the tag left: same timestamp and tag, the old gateway, sensor fields null
    // (rssi etc. were measured by the new gateway, not the old one).
    private static BsonDocument OutDocument(BsonDocument doc, StreamMapping mapping, Zone previous, DateTime time)
    {
        var meta = doc["meta"].AsBsonDocument.DeepClone().AsBsonDocument;
        meta[GatewayKey] = previous.Gateway;
        var outDoc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "timestamp", doc["timestamp"] },
            { "meta", meta },
        };
        foreach (var f in mapping.Fields)
            outDoc[f.Name] = BsonNull.Value;
        SetTrigger(outDoc, mapping, Out, previous.Since, time);
        return outDoc;
    }

    // Writes device_trigger, then zone_in / zone_out / time_spent right after it.
    private static void SetTrigger(BsonDocument doc, StreamMapping mapping, string trigger, DateTime zoneIn, DateTime? zoneOut)
    {
        var field = mapping.DeviceTriggerField!;
        doc[field] = trigger;
        var index = doc.IndexOfName(field);
        BsonValue timeSpent = zoneOut is { } o ? Math.Round((o - zoneIn).TotalMinutes, 2) : BsonNull.Value;
        Put(doc, ++index, ZoneIn, new BsonDateTime(zoneIn));
        Put(doc, ++index, ZoneOut, zoneOut is { } z ? new BsonDateTime(z) : BsonNull.Value);
        Put(doc, ++index, TimeSpent, timeSpent);
    }

    private static void Put(BsonDocument doc, int index, string name, BsonValue value)
    {
        doc.Remove(name);
        doc.InsertAt(index, new BsonElement(name, value));
    }

    private static DateTime Time(BsonDocument doc) => doc["timestamp"].ToUniversalTime();

    private static string TagId(BsonDocument doc) => doc["meta"][TagKey].ToString()!;

    private static Zone? Parse(RedisValue value)
    {
        if (value.IsNullOrEmpty)
            return null;
        var s = value.ToString();
        var i = s.LastIndexOf('|');
        return i > 0 && long.TryParse(s[(i + 1)..], out var ms)
            ? new Zone(s[..i], DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime)
            : null;
    }
}
