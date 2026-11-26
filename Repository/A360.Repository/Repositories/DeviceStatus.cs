using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Repository.Repositories;

/// <summary>Online (heard within 2 min), Idle (within 10 min), Offline (older or never).</summary>
public sealed record DeviceStatus(string DeviceId, DateTime? LastSeen, string Status, IReadOnlyList<string> TagIds)
{
    public int TagsInRange => TagIds.Count;
}

/// <summary>
/// ONE read-side rule for gateway / repeater status, used by the Real-Time Location popup (dashboard gateway feed), IoT Device
/// Management, Telemetry and the Dashboard network status. Last seen = newest NetworkTimeSeries reading with gatewayid = the
/// device, whoever "won" the tag; a repeater is also alive when a gateway hears its own beacon (GatewayTimeSeries, tagid =
/// device id). Assets in range = distinct tags the device heard in the last 2 minutes.
/// </summary>
public sealed class DeviceStatusReader(IMongoDatabase database)
{
    public static readonly TimeSpan OnlineWithin = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan IdleWithin = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LookBack = TimeSpan.FromDays(30);

    public static string Classify(DateTime? lastSeen, DateTime now) =>
        lastSeen is null || now - lastSeen > IdleWithin ? "Offline" : now - lastSeen > OnlineWithin ? "Idle" : "Online";

    /// <summary>Status per device id (lower case, no ':'). deviceIds null = every device heard in the last 24 h.</summary>
    public async Task<Dictionary<string, DeviceStatus>> GetAsync(IEnumerable<string>? deviceIds, CancellationToken ct, DateTime? nowOverride = null)
    {
        var now = nowOverride ?? DateTime.UtcNow;
        var ids = deviceIds?.Where(i => !string.IsNullOrWhiteSpace(i)).Select(Normalize).Distinct().ToList();
        var result = new Dictionary<string, DeviceStatus>(StringComparer.Ordinal);
        if (ids is { Count: 0 }) return result;

        var since = ids is null ? now - TimeSpan.FromHours(24) : now - LookBack;
        var idFilter = ids is null ? null : new BsonDocument("$in", new BsonArray(ids.SelectMany(Variants).Distinct()));
        var notOut = new BsonDocument("$ne", "out");
        var networkSeries = database.GetCollection<BsonDocument>("NetworkTimeSeries");

        // newest reading per device (as gatewayid)
        var match = new BsonDocument { { "timestamp", new BsonDocument("$gte", since) }, { "device_trigger", notOut }, { "meta.gatewayid", idFilter ?? new BsonDocument("$type", "string") } };
        var last = (await networkSeries.Aggregate<BsonDocument>(new BsonDocument[] {
                new BsonDocument("$match", match),
                new BsonDocument("$group", new BsonDocument { { "_id", new BsonDocument("$toLower", "$meta.gatewayid") }, { "at", new BsonDocument("$max", "$timestamp") } })
            }, cancellationToken: ct).ToListAsync(ct))
            .Where(r => r["_id"].IsString).ToDictionary(r => Normalize(r["_id"].AsString), r => r["at"].ToUniversalTime());

        // distinct tags heard in the last 2 minutes
        var rangeMatch = new BsonDocument { { "timestamp", new BsonDocument("$gte", now - OnlineWithin) }, { "device_trigger", notOut }, { "meta.gatewayid", idFilter ?? new BsonDocument("$type", "string") }, { "meta.tagid", new BsonDocument("$type", "string") } };
        var tags = (await networkSeries.Aggregate<BsonDocument>(new BsonDocument[] {
                new BsonDocument("$match", rangeMatch),
                new BsonDocument("$group", new BsonDocument { { "_id", new BsonDocument("$toLower", "$meta.gatewayid") }, { "tags", new BsonDocument("$addToSet", new BsonDocument("$toLower", "$meta.tagid")) } })
            }, cancellationToken: ct).ToListAsync(ct))
            .Where(r => r["_id"].IsString).ToDictionary(r => Normalize(r["_id"].AsString), r => r["tags"].AsBsonArray.Select(t => t.AsString).OrderBy(t => t, StringComparer.Ordinal).ToList());

        // repeater beacons: a gateway heard the device itself (GatewayTimeSeries, tagid = device id)
        if (ids is { Count: > 0 })
        {
            var beacons = await database.GetCollection<BsonDocument>("GatewayTimeSeries").Aggregate<BsonDocument>(new BsonDocument[] {
                    new BsonDocument("$match", new BsonDocument { { "timestamp", new BsonDocument("$gte", since) }, { "meta.tagid", idFilter } }),
                    new BsonDocument("$group", new BsonDocument { { "_id", new BsonDocument("$toLower", "$meta.tagid") }, { "at", new BsonDocument("$max", "$timestamp") } })
                }, cancellationToken: ct).ToListAsync(ct);
            foreach (var b in beacons.Where(b => b["_id"].IsString))
            {
                var key = Normalize(b["_id"].AsString);
                var at = b["at"].ToUniversalTime();
                if (!last.TryGetValue(key, out var existing) || at > existing) last[key] = at;
            }
        }

        foreach (var key in ids ?? last.Keys.ToList())
        {
            DateTime? seen = last.TryGetValue(key, out var t) ? t : null;
            result[key] = new DeviceStatus(key, seen, Classify(seen, now), tags.TryGetValue(key, out var list) ? list : []);
        }
        return result;
    }

    public static string Normalize(string id) => id.Trim().Replace(":", "").Replace("-", "").ToLowerInvariant();

    private static IEnumerable<string> Variants(string id)
    {
        yield return id;
        yield return id.ToUpperInvariant();
    }
}
