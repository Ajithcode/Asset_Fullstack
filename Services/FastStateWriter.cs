using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>Runtime switch of the fast resolver (env Location:FastMode, overridden by the Redis key Location:Fast:FlagKey) + the
/// current device / zone-since per tag, for the 15 s ingest worker to label stored readings.</summary>
public class FastModeFlag(IOptions<LocationSettings> location)
{
    public volatile bool Active = location.Value.FastMode;
}

public class FastStateStore
{
    public readonly ConcurrentDictionary<string, (string Device, long SinceMs)> Current = new();
}

/// <summary>
/// Everything the fast resolver writes when it is live (never in shadow mode):
///   TagLocationState (normal collection, 1 doc per tag): device, zone, since, updated_at / last_seen_at (throttled);
///   ZoneVisit (normal collection, 1 doc per visit): a zone change closes the open visit with a conditional update
///     (tagId + isOpen + exitedAt null + visit id) and inserts the new one (_id = tagId:enteredAtMs, so a retry is a duplicate key,
///     never a second visit; a unique partial index allows one open visit per tag). Signal loss closes it at the last-seen time;
///   compatibility "out" document (zone_in, zone_out, time_spent) in the append-only time series, as the 15 s resolver wrote;
///   LocationLatency (one row per decision) and the Redis pub/sub message for the tracking WebSocket.
/// The time-series collections are only ever inserted into.
/// </summary>
public class FastStateWriter(
    IMongoDatabase database,
    IConnectionMultiplexer redis,
    TimeSeriesCollectionInitializer initializer,
    FastStateStore store,
    IOptions<LocationSettings> location,
    IOptions<FastLocationSettings> fastOptions,
    IOptions<List<StreamMapping>> mappings,
    ILogger<FastStateWriter> logger)
{
    private FastLocationSettings F => fastOptions.Value;
    private IMongoCollection<BsonDocument> State => database.GetCollection<BsonDocument>(location.Value.StateCollection);
    private IMongoCollection<BsonDocument> Visits => database.GetCollection<BsonDocument>(F.VisitCollection);
    private IMongoCollection<BsonDocument> Latency => database.GetCollection<BsonDocument>(F.LatencyCollection);
    private readonly ConcurrentDictionary<string, string> _openVisit = new(); // tag -> visit id
    private readonly Dictionary<string, string?> _assets = new();

    public async Task EnsureIndexesAsync(CancellationToken ct)
    {
        var visit = Builders<BsonDocument>.IndexKeys;
        await Visits.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(visit.Ascending("tagId").Ascending("exitedAt")),
            new CreateIndexModel<BsonDocument>(visit.Ascending("tagId").Descending("enteredAt")),
            new CreateIndexModel<BsonDocument>(visit.Ascending("zoneId").Descending("enteredAt")),
            // one open visit per tag: isOpen exists only while the visit is open
            new CreateIndexModel<BsonDocument>(visit.Ascending("tagId"), new CreateIndexOptions<BsonDocument>
            {
                Name = "ux_zonevisit_open_tag", Unique = true,
                PartialFilterExpression = new BsonDocument("isOpen", true),
            }),
        ], ct);
        var ttl = TimeSpan.FromDays(F.LogRetentionDays);
        foreach (var name in new[] { F.LatencyCollection, F.ShadowLogCollection })
            await database.GetCollection<BsonDocument>(name).Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(visit.Ascending("at"), new CreateIndexOptions { ExpireAfter = ttl, Name = "ttl_at" }),
                new CreateIndexModel<BsonDocument>(visit.Ascending("tag").Ascending("atMs")),
            ], ct);
        foreach (var v in await Visits.Find(new BsonDocument("isOpen", true)).Project(new BsonDocument("tagId", 1)).ToListAsync(ct))
            _openVisit[v["tagId"].AsString] = v["_id"].AsString;
    }

    /// <summary>Current TagLocationState rows (tag, device, zone, labels, since ms, last seen ms).</summary>
    public async Task<List<(string Tag, string? Device, string? Zone, string? ZoneName, string? Floor, long SinceMs, long SeenMs)>> LoadStateAsync(CancellationToken ct)
    {
        var rows = await State.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        string? S(BsonDocument d, string n) => d.GetValue(n, BsonNull.Value) is { IsString: true } v ? v.AsString : null;
        long Ms(BsonDocument d, string n) => d.GetValue(n, BsonNull.Value) is { IsValidDateTime: true } v ? new DateTimeOffset(v.ToUniversalTime()).ToUnixTimeMilliseconds() : 0;
        return rows.Select(d => (d["_id"].AsString, S(d, "device_id"), S(d, "zone_id"), S(d, "zone_name"), S(d, "floor_plan_id"), Ms(d, "since"), Ms(d, "updated_at"))).ToList();
    }

    /// <summary>Makes ZoneVisit match TagLocationState when the fast resolver goes live: open a visit for every located tag
    /// that has none, close visits of tags that moved or lost their location.</summary>
    public async Task ReconcileVisitsAsync(CancellationToken ct)
    {
        var states = await LoadStateAsync(ct);
        foreach (var s in states)
        {
            _openVisit.TryGetValue(s.Tag, out var open);
            var openZone = open == null ? null : (await Visits.Find(new BsonDocument("_id", open)).FirstOrDefaultAsync(ct))?.GetValue("zoneId", BsonNull.Value).ToString();
            if (s.Zone == null || s.Device == null)
            {
                if (open != null) await CloseVisitAsync(s.Tag, open, s.SeenMs > 0 ? s.SeenMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), s.SeenMs, ct);
                continue;
            }
            if (open != null && openZone == s.Zone) continue;
            if (open != null) await CloseVisitAsync(s.Tag, open, s.SinceMs, s.SeenMs, ct);
            await OpenVisitAsync(s.Tag, s.Zone, s.Device, s.SinceMs == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : s.SinceMs, s.SeenMs, ct);
        }
        logger.LogInformation("ZoneVisit reconciled with TagLocationState ({Tags} tags, {Open} open visits)", states.Count, _openVisit.Count);
    }

    public async Task ApplyAsync(FastDecision d, long publishedStartMs, CancellationToken ct)
    {
        var tag = d.Tag;
        var stateSet = new BsonDocument
        {
            { "updated_at", new BsonDateTime(d.DecidedMs) }, { "last_seen_at", new BsonDateTime(Math.Max(d.LastSeenMs, d.DecidedMs)) },
            { "pending_device", BsonNull.Value }, { "pending_count", 0 }, { "mode", "fast" },
        };
        switch (d.Kind)
        {
            case FastDecisionKind.SignalLost:
                stateSet["device_id"] = BsonNull.Value; stateSet["zone_id"] = BsonNull.Value; stateSet["zone_name"] = BsonNull.Value;
                stateSet["floor_plan_id"] = BsonNull.Value; stateSet["since"] = BsonNull.Value; stateSet["rssi"] = BsonNull.Value;
                stateSet["updated_at"] = new BsonDateTime(d.LastSeenMs);
                if (_openVisit.TryGetValue(tag, out var lost)) await CloseVisitAsync(tag, lost, d.LastSeenMs, d.LastSeenMs, ct);
                store.Current.TryRemove(tag, out _);
                break;

            case FastDecisionKind.DeviceChange:
                stateSet["device_id"] = d.Device; stateSet["rssi"] = d.Rssi is { } r1 ? r1 : BsonNull.Value;
                if (_openVisit.TryGetValue(tag, out var same))
                    await Visits.UpdateOneAsync(new BsonDocument("_id", same), new BsonDocument("$set", new BsonDocument("deviceId", d.Device)), cancellationToken: ct);
                store.Current[tag] = (d.Device!, d.SinceMs);
                break;

            default: // Assign / ZoneChange
                stateSet["device_id"] = d.Device; stateSet["zone_id"] = d.ZoneId; stateSet["zone_name"] = d.ZoneName is null ? BsonNull.Value : d.ZoneName;
                stateSet["floor_plan_id"] = d.FloorPlanId is null ? BsonNull.Value : d.FloorPlanId;
                stateSet["since"] = new BsonDateTime(d.SinceMs); stateSet["rssi"] = d.Rssi is { } r2 ? r2 : BsonNull.Value;
                if (_openVisit.TryGetValue(tag, out var open)) await CloseVisitAsync(tag, open, d.DecidedMs, d.LastSeenMs, ct);
                await OpenVisitAsync(tag, d.ZoneId!, d.Device!, d.DecidedMs, d.LastSeenMs, ct);
                if (d.Kind == FastDecisionKind.ZoneChange && d.FromDevice != null) await WriteOutDocumentAsync(d, ct);
                store.Current[tag] = (d.Device!, d.SinceMs);
                break;
        }
        await State.UpdateOneAsync(new BsonDocument("_id", tag), new BsonDocument("$set", stateSet), new UpdateOptions { IsUpsert = true }, ct);
        var savedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var id = ObjectId.GenerateNewId();
        await redis.GetDatabase().PublishAsync(RedisChannel.Literal(F.PubSubChannel), JsonSerializer.Serialize(new
        {
            id = id.ToString(), tag, kind = d.Kind.ToString(), zoneId = d.ZoneId, zoneName = d.ZoneName, floorPlanId = d.FloorPlanId,
            device = d.Device, fromZoneId = d.FromZoneId, sinceMs = d.SinceMs, decidedMs = d.DecidedMs,
        }));
        var publishedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await Latency.InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "at", new BsonDateTime(d.DecidedMs) }, { "atMs", d.DecidedMs }, { "tag", tag }, { "kind", d.Kind.ToString() },
            { "toZone", d.ZoneId is null ? BsonNull.Value : d.ZoneId }, { "fromZone", d.FromZoneId is null ? BsonNull.Value : d.FromZoneId },
            { "deviceTsMs", d.LastSampleTsMs }, { "recvMs", d.LastSampleRecvMs }, { "readMs", d.LastSampleReadMs },
            { "decidedMs", d.DecidedMs }, { "savedMs", savedMs }, { "publishedMs", publishedMs }, { "wsSentMs", BsonNull.Value },
        }, cancellationToken: ct);
    }

    /// <summary>Throttled "still here": updated_at / last_seen_at on the state and lastSeenAt on the open visit.</summary>
    public async Task TouchAsync(IEnumerable<(string Tag, long SeenMs, double? Rssi)> seen, CancellationToken ct)
    {
        var state = new List<WriteModel<BsonDocument>>();
        var visits = new List<WriteModel<BsonDocument>>();
        foreach (var (tag, seenMs, rssi) in seen)
        {
            var set = new BsonDocument { { "updated_at", new BsonDateTime(seenMs) }, { "last_seen_at", new BsonDateTime(seenMs) } };
            if (rssi is { } r) set["rssi"] = r;
            state.Add(new UpdateOneModel<BsonDocument>(new BsonDocument("_id", tag), new BsonDocument("$set", set)));
            if (_openVisit.TryGetValue(tag, out var v))
                visits.Add(new UpdateOneModel<BsonDocument>(new BsonDocument("_id", v),
                    new BsonDocument("$set", new BsonDocument("lastSeenAt", new BsonDateTime(seenMs)))));
        }
        if (state.Count > 0) await State.BulkWriteAsync(state, new BulkWriteOptions { IsOrdered = false }, ct);
        if (visits.Count > 0) await Visits.BulkWriteAsync(visits, new BulkWriteOptions { IsOrdered = false }, ct);
    }

    /// <summary>Battery / status come with the 15 s window readings, not with the fast lane.</summary>
    public async Task UpdateBatteryStatusAsync(Dictionary<string, (double? Battery, string? Status)> latest, CancellationToken ct)
    {
        var writes = latest.Select(kv =>
        {
            var set = new BsonDocument();
            if (kv.Value.Battery is { } b) set["battery"] = b;
            if (kv.Value.Status is { } s) set["status"] = s;
            return set.ElementCount == 0 ? null : (WriteModel<BsonDocument>)new UpdateOneModel<BsonDocument>(
                new BsonDocument("_id", kv.Key), new BsonDocument("$set", set));
        }).Where(w => w != null).Select(w => w!).ToList();
        if (writes.Count > 0) await State.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, ct);
    }

    // ---- visits ----------------------------------------------------------------------------------------------------

    private async Task OpenVisitAsync(string tag, string zoneId, string device, long enteredMs, long seenMs, CancellationToken ct)
    {
        var id = $"{tag}:{enteredMs}";
        var doc = new BsonDocument
        {
            { "_id", id }, { "tagId", tag }, { "assetId", await AssetOfAsync(tag, ct) is { } a ? a : BsonNull.Value },
            { "zoneId", zoneId }, { "deviceId", device }, { "enteredAt", new BsonDateTime(enteredMs) }, { "exitedAt", BsonNull.Value },
            { "durationSec", BsonNull.Value }, { "lastSeenAt", new BsonDateTime(Math.Max(seenMs, enteredMs)) }, { "isOpen", true },
        };
        try { await Visits.InsertOneAsync(doc, cancellationToken: ct); }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            logger.LogWarning("ZoneVisit {Id}: duplicate key (retry or another open visit); keeping the existing one", id);
            var open = await Visits.Find(new BsonDocument { { "tagId", tag }, { "isOpen", true } }).FirstOrDefaultAsync(ct);
            if (open == null) return;
            id = open["_id"].AsString;
        }
        _openVisit[tag] = id;
    }

    private async Task CloseVisitAsync(string tag, string visitId, long exitedMs, long seenMs, CancellationToken ct)
    {
        var visit = await Visits.Find(new BsonDocument("_id", visitId)).FirstOrDefaultAsync(ct);
        var entered = visit?.GetValue("enteredAt", BsonNull.Value) is { IsValidDateTime: true } e ? new DateTimeOffset(e.ToUniversalTime()).ToUnixTimeMilliseconds() : exitedMs;
        exitedMs = Math.Max(exitedMs, entered);
        await Visits.UpdateOneAsync(
            new BsonDocument { { "_id", visitId }, { "tagId", tag }, { "isOpen", true }, { "exitedAt", BsonNull.Value } },
            new BsonDocument
            {
                { "$set", new BsonDocument { { "exitedAt", new BsonDateTime(exitedMs) }, { "durationSec", (exitedMs - entered) / 1000.0 },
                    { "lastSeenAt", new BsonDateTime(Math.Max(seenMs, entered)) } } },
                { "$unset", new BsonDocument("isOpen", "") },
            }, cancellationToken: ct);
        _openVisit.TryRemove(tag, out _);
    }

    private async Task<string?> AssetOfAsync(string tag, CancellationToken ct)
    {
        if (_assets.TryGetValue(tag, out var cached)) return cached;
        var row = await database.GetCollection<BsonDocument>("TrackingTag")
            .Find(new BsonDocument("tag_id", tag)).Project(new BsonDocument("asset_registration_id", 1)).FirstOrDefaultAsync(ct);
        var asset = row?.GetValue("asset_registration_id", BsonNull.Value) is { IsString: true } s ? s.AsString : null;
        return _assets[tag] = asset;
    }

    // The 15 s resolver wrote an "out" document for the old device on every zone change; keep doing it (append only).
    private async Task WriteOutDocumentAsync(FastDecision d, CancellationToken ct)
    {
        var mapping = mappings.Value.FirstOrDefault(m => m.DeviceTriggerField != null);
        if (mapping == null) return;
        var doc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() }, { "timestamp", new BsonDateTime(d.DecidedMs) },
            { "meta", new BsonDocument { { "gatewayid", d.FromDevice }, { "tagid", d.Tag } } },
        };
        foreach (var f in mapping.Fields) doc[f.Name] = BsonNull.Value;
        var sinceMs = store.Current.TryGetValue(d.Tag, out var cur) ? cur.SinceMs : d.SinceMs;
        TagLocationResolver.SetTrigger(doc, mapping, DeviceTriggerTracker.Out, sinceMs, DateTimeOffset.FromUnixTimeMilliseconds(d.DecidedMs).UtcDateTime);
        var collection = await initializer.EnsureAsync(mapping, ct);
        await collection.InsertOneAsync(doc, cancellationToken: ct);
    }
}
