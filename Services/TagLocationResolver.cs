using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Single source of truth for "which device / zone is a tag in".
/// Each 15 s window's stored RSSI (max per device+tag) is one sample. Per tag: median of the last RssiWindowCount
/// windows per device, a device needs samples in >= RssiMinWindows of them, only devices plotted in a zone compete,
/// and the tag only switches device when a new one is >= SwitchMarginDb stronger for SwitchConsecutiveWindows windows.
/// Documents: readings of the resolved device get device_trigger "in" + zone_in; readings of other devices get
/// device_trigger null (still stored: Online status and "assets in range" need them). When the ZONE changes, an extra
/// "out" document for the old device is written (zone_in, zone_out, time_spent in minutes).
/// State: Redis hash {Stream}:loc_state (JSON per tag, survives restarts, AOF) and, as the read model for the APIs,
/// Mongo TagLocationState (_id = tag id). A missing Redis entry is seeded from Mongo so a restart or Redis loss
/// does not create a zone_out/zone_in pair; only the RSSI buffers refill.
/// </summary>
public class TagLocationResolver(
    IConnectionMultiplexer redis,
    IMongoDatabase database,
    PlottedDeviceLookup plots,
    IOptions<LocationSettings> options,
    ILogger<TagLocationResolver> logger)
{
    private const string GatewayKey = "gatewayid";
    private const string TagKey = "tagid";

    public sealed class Sample { public long T { get; set; } public double R { get; set; } }

    public sealed class TagState
    {
        public string? Device { get; set; }
        public string? ZoneId { get; set; }
        public string? ZoneName { get; set; }
        public string? FloorPlanId { get; set; }
        public long SinceMs { get; set; }
        public string? Pending { get; set; }
        public int PendingCount { get; set; }
        public long LastWindow { get; set; }
        public double? Rssi { get; set; }
        public double? Battery { get; set; }
        public string? Status { get; set; }
        public Dictionary<string, List<Sample>> Samples { get; set; } = new();
    }

    public record Result(List<BsonDocument> Documents, Dictionary<string, TagState> Changes);

    private LocationSettings S => options.Value;
    private IMongoCollection<BsonDocument> StateCollection => database.GetCollection<BsonDocument>(S.StateCollection);
    private bool _seeded;

    public async Task<Result> ApplyAsync(List<BsonDocument> documents, StreamMapping mapping, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = StateKey(mapping);
        if (!_seeded) await SeedIfEmptyAsync(db, key);

        var (inZone, allPlotted) = await plots.GetAsync(ct);

        // Devices themselves (repeater beacons) are not tags to locate.
        documents = documents.OrderBy(Time).ToList();
        var tagIds = documents.Select(TagId).Where(t => !allPlotted.Contains(t)).Distinct().ToArray();
        var stored = tagIds.Length == 0 ? [] : await db.HashGetAsync(key, tagIds.Select(t => (RedisValue)t).ToArray());
        var states = new Dictionary<string, TagState>();
        for (var i = 0; i < tagIds.Length; i++)
            states[tagIds[i]] = Parse(stored[i]) ?? await FromMongoAsync(tagIds[i], ct) ?? new TagState();

        var changes = new Dictionary<string, TagState>();
        var result = new List<BsonDocument>(documents.Count);

        foreach (var window in documents.GroupBy(Time))
        {
            var t = new DateTimeOffset(window.Key).ToUnixTimeMilliseconds();
            foreach (var tag in window.GroupBy(TagId))
            {
                if (!states.TryGetValue(tag.Key, out var st))
                {
                    foreach (var d in tag) { SetTrigger(d, mapping, null, null, null); result.Add(d); }
                    continue;
                }

                var outDoc = Resolve(st, tag.ToList(), t, window.Key, inZone, mapping);
                if (outDoc != null) result.Add(outDoc);
                foreach (var d in tag)
                {
                    var atResolved = st.Device != null && string.Equals(GatewayOf(d), st.Device, StringComparison.Ordinal);
                    SetTrigger(d, mapping, atResolved ? DeviceTriggerTracker.In : null, atResolved ? st.SinceMs : null, null);
                    result.Add(d);
                }
                changes[tag.Key] = st;
            }
        }

        return new Result(result, changes);
    }

    /// <summary>
    /// Log-only run of the 15 s logic while the fast resolver is live: same decisions, but on copies of the documents, with its own
    /// Redis state hash (seeded from TagLocationState per tag) and NO Mongo state / time-series / alert writes. Every zone change it
    /// would have made goes to the shadow log collection as variant "slow15".
    /// </summary>
    public async Task ShadowApplyAsync(List<BsonDocument> documents, StreamMapping mapping, string logCollection, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = StateKey(mapping) + ":shadow";
        var (inZone, allPlotted) = await plots.GetAsync(ct);

        var docs = documents.Select(d => d.DeepClone().AsBsonDocument).OrderBy(Time).ToList();
        var tagIds = docs.Select(TagId).Where(t => !allPlotted.Contains(t)).Distinct().ToArray();
        if (tagIds.Length == 0) return;
        var stored = await db.HashGetAsync(key, tagIds.Select(t => (RedisValue)t).ToArray());
        var states = new Dictionary<string, TagState>();
        for (var i = 0; i < tagIds.Length; i++)
            states[tagIds[i]] = Parse(stored[i]) ?? await FromMongoAsync(tagIds[i], ct) ?? new TagState();

        var log = new List<BsonDocument>();
        foreach (var window in docs.GroupBy(Time))
        {
            var t = new DateTimeOffset(window.Key).ToUnixTimeMilliseconds();
            foreach (var tag in window.GroupBy(TagId))
            {
                if (!states.TryGetValue(tag.Key, out var st)) continue;
                var oldDevice = st.Device; var oldZone = st.ZoneId;
                Resolve(st, tag.ToList(), t, window.Key, inZone, mapping);
                if (st.Device != oldDevice && st.Device != null)
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    log.Add(new BsonDocument
                    {
                        { "at", new BsonDateTime(now) }, { "atMs", now }, { "tag", tag.Key }, { "variant", "slow15" }, { "type", "decision" },
                        { "kind", oldDevice == null ? "First" : st.ZoneId != oldZone ? "ZoneChange" : "DeviceChange" },
                        { "fromZone", oldZone is null ? BsonNull.Value : oldZone }, { "toZone", st.ZoneId is null ? BsonNull.Value : st.ZoneId },
                        { "fromDevice", oldDevice is null ? BsonNull.Value : oldDevice }, { "device", st.Device },
                        { "windowMs", t },
                    });
                }
            }
        }
        await db.HashSetAsync(key, states.Select(c => new HashEntry(c.Key, JsonSerializer.Serialize(c.Value))).ToArray());
        if (log.Count > 0)
            await database.GetCollection<BsonDocument>(logCollection).InsertManyAsync(log, new InsertManyOptions { IsOrdered = false }, ct);
    }

    /// <summary>Back from the fast resolver: forget the cached state so it is seeded again from Mongo TagLocationState (current device / zone).</summary>
    public async Task ResetFromMongoAsync(StreamMapping mapping)
    {
        await redis.GetDatabase().KeyDeleteAsync(new RedisKey[] { StateKey(mapping), StateKey(mapping) + ":shadow" });
        _seeded = false;
    }

    /// <summary>Persist after the insert succeeded (Redis state + Mongo read model).</summary>
    public async Task SaveAsync(StreamMapping mapping, Dictionary<string, TagState> changes, CancellationToken ct)
    {
        if (changes.Count == 0) return;
        await redis.GetDatabase().HashSetAsync(StateKey(mapping),
            changes.Select(c => new HashEntry(c.Key, JsonSerializer.Serialize(c.Value))).ToArray());

        var writes = changes.Select(c => new ReplaceOneModel<BsonDocument>(
            new BsonDocument("_id", c.Key), ToDocument(c.Key, c.Value)) { IsUpsert = true }).ToList();
        await StateCollection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, ct);
    }

    // ---- resolution --------------------------------------------------------------------------------------------

    // internal: tools/ReplayHarness compiles this file and replays stored readings through it
    internal BsonDocument? Resolve(TagState st, List<BsonDocument> docs, long t, DateTime windowTime,
        IReadOnlyDictionary<string, PlottedDevice> inZone, StreamMapping mapping)
    {
        // 1. add this window's samples (max per device), keep the tag's last-seen info
        foreach (var g in docs.GroupBy(GatewayOf))
        {
            var rssis = g.Select(d => Num(d.GetValue("rssi", BsonNull.Value))).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (rssis.Count == 0) continue;
            if (!st.Samples.TryGetValue(g.Key, out var list)) st.Samples[g.Key] = list = [];
            var max = rssis.Max();
            var existing = list.FirstOrDefault(s => s.T == t);
            if (existing != null) existing.R = Math.Max(existing.R, max);
            else list.Add(new Sample { T = t, R = max });
        }

        // 2. forget what is older than the window range (and devices with nothing left)
        var span = S.WindowSeconds * 1000L;
        var cutoff = t - (S.RssiWindowCount * span - span / 2);
        foreach (var dev in st.Samples.Keys.ToList())
        {
            st.Samples[dev] = st.Samples[dev].Where(s => s.T > cutoff && s.T <= t + span).OrderBy(s => s.T).ToList();
            if (st.Samples[dev].Count == 0) st.Samples.Remove(dev);
        }

        // last reading details (any device): newest by rssi in this window
        var best = docs.OrderByDescending(d => Num(d.GetValue("rssi", BsonNull.Value)) ?? double.MinValue).First();
        st.Battery = Num(best.GetValue("battery", BsonNull.Value)) ?? st.Battery;
        st.Status = best.GetValue("status", BsonNull.Value) is { IsString: true } s ? s.AsString : st.Status;

        // late or repeated window: samples merged, decision already taken
        if (t <= st.LastWindow) return null;
        st.LastWindow = t;

        // 3. candidates: plotted in a zone and enough windows
        var medians = new Dictionary<string, double>();
        foreach (var (dev, list) in st.Samples)
            if (inZone.ContainsKey(dev) && list.Count >= S.RssiMinWindows)
                medians[dev] = Median(list.Select(x => x.R).ToList());

        // first assignment: strongest qualifying device, no hysteresis
        if (st.Device == null)
        {
            if (medians.Count == 0) return null;
            var first = medians.OrderByDescending(m => m.Value).ThenBy(m => m.Key, StringComparer.Ordinal).First();
            Assign(st, inZone[first.Key], t);
            st.Rssi = first.Value;
            return null;
        }

        var current = st.Device;
        st.Samples.TryGetValue(current, out var own);
        var ownLatest = own is { Count: > 0 } ? own.OrderBy(x => x.T).Last() : null;

        // a) fast path: a plotted device heard in THIS window is >= FastSwitchMarginDb stronger than the current device's latest
        //    sample (or the current device has no sample in range at all): switch after one window, before it has RssiMinWindows
        //    samples. A gap in the current device's own readings (repeaters are heard every other window) is NOT a reason by
        //    itself: it is compared with its latest sample in range.
        var fast = st.Samples
            .Where(kv => kv.Key != current && inZone.ContainsKey(kv.Key))
            .Select(kv => (Device: kv.Key, Sample: kv.Value.FirstOrDefault(x => x.T == t)))
            .Where(x => x.Sample != null && (ownLatest == null || x.Sample.R >= ownLatest.R + S.FastSwitchMarginDb)
                        && CanTakeOver(st.Samples[x.Device], x.Sample.R))
            .OrderByDescending(x => x.Sample!.R).ThenBy(x => x.Device, StringComparer.Ordinal)
            .FirstOrDefault();
        if (fast.Device != null)
            return Switch(st, inZone[fast.Device], fast.Sample!.R, docs, mapping, windowTime, t);

        // b) silence: no sample of the current device for CurrentDeviceSilenceSeconds and a challenger qualifies: switch now
        var top = medians.Where(m => m.Key != current).OrderByDescending(m => m.Value).ThenBy(m => m.Key, StringComparer.Ordinal).Select(m => ((string Key, double Value)?)(m.Key, m.Value)).FirstOrDefault();
        var silent = ownLatest == null || ownLatest.T <= t - S.CurrentDeviceSilenceSeconds * 1000L;
        if (silent && top is { } silentBest && CanTakeOver(st.Samples[silentBest.Key], st.Samples[silentBest.Key].OrderBy(x => x.T).Last().R))
            return Switch(st, inZone[silentBest.Key], silentBest.Value, docs, mapping, windowTime, t);

        // c) normal: the current device keeps its place while it has any sample in range (compared with its own median, even
        //    with fewer than RssiMinWindows); a qualified challenger must be SwitchMarginDb stronger for SwitchConsecutiveWindows
        var currentMedian = medians.TryGetValue(current, out var cm) ? cm
            : own is { Count: > 0 } ? Median(own.Select(x => x.R).ToList())
            : double.NegativeInfinity;
        if (top is { } challenger && challenger.Value >= currentMedian + S.SwitchMarginDb)
        {
            st.PendingCount = st.Pending == challenger.Key ? st.PendingCount + 1 : 1;
            st.Pending = challenger.Key;
        }
        else
        {
            st.Pending = null;
            st.PendingCount = 0;
        }

        if (st.Pending != null && st.PendingCount >= S.SwitchConsecutiveWindows)
            return Switch(st, inZone[st.Pending], medians[st.Pending], docs, mapping, windowTime, t);

        st.Rssi = medians.TryGetValue(current, out var cur) ? cur : st.Rssi;

        // keep zone label in step with plot edits (same device, zone renamed/moved): no event
        if (inZone.TryGetValue(current, out var plot) && plot.ZoneId == st.ZoneId)
        {
            st.ZoneName = plot.ZoneName;
            st.FloorPlanId = plot.FloorPlanId;
        }
        return null;
    }

    // Weak-sample guard for the fast path and the silence takeover: the challenger needs TakeoverMinSamples samples in the
    // range, or a single sample at or above TakeoverStrongRssiDbm. One weak reading must not take a tag from a silent repeater.
    private bool CanTakeOver(List<Sample> challenger, double latestRssi) =>
        challenger.Count >= S.TakeoverMinSamples || latestRssi >= S.TakeoverStrongRssiDbm;

    // Moves the tag to another device. A different ZONE also closes the stay: "out" document for the old device.
    private static BsonDocument? Switch(TagState st, PlottedDevice next, double rssi, List<BsonDocument> docs,
        StreamMapping mapping, DateTime windowTime, long t)
    {
        BsonDocument? outDoc = null;
        var oldDevice = st.Device!;
        var oldSince = st.SinceMs;
        var zoneChanged = !string.Equals(next.ZoneId, st.ZoneId, StringComparison.Ordinal);
        if (zoneChanged)
            outDoc = OutDocument(docs[0], mapping, oldDevice, oldSince, windowTime);

        st.Device = next.DeviceId;
        if (zoneChanged)
        {
            st.ZoneId = next.ZoneId;
            st.ZoneName = next.ZoneName;
            st.FloorPlanId = next.FloorPlanId;
            st.SinceMs = t;
        }
        st.Pending = null;
        st.PendingCount = 0;
        st.Rssi = rssi;
        return outDoc;
    }

    private static void Assign(TagState st, PlottedDevice d, long t)
    {
        st.Device = d.DeviceId;
        st.ZoneId = d.ZoneId;
        st.ZoneName = d.ZoneName;
        st.FloorPlanId = d.FloorPlanId;
        st.SinceMs = t;
        st.Pending = null;
        st.PendingCount = 0;
    }

    private static double Median(List<double> v)
    {
        v.Sort();
        var n = v.Count;
        return n % 2 == 1 ? v[n / 2] : (v[n / 2 - 1] + v[n / 2]) / 2.0;
    }

    // ---- documents ---------------------------------------------------------------------------------------------

    private static BsonDocument OutDocument(BsonDocument doc, StreamMapping mapping, string oldDevice, long sinceMs, DateTime time)
    {
        var meta = doc["meta"].AsBsonDocument.DeepClone().AsBsonDocument;
        meta[GatewayKey] = oldDevice;
        var outDoc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "timestamp", doc["timestamp"] },
            { "meta", meta },
        };
        foreach (var f in mapping.Fields)
            outDoc[f.Name] = BsonNull.Value;
        SetTrigger(outDoc, mapping, DeviceTriggerTracker.Out, sinceMs, time);
        return outDoc;
    }

    // trigger null -> device_trigger null, no zone fields (a reading of a device that is not the resolved one)
    internal static void SetTrigger(BsonDocument doc, StreamMapping mapping, string? trigger, long? zoneInMs, DateTime? zoneOut)
    {
        var field = mapping.DeviceTriggerField!;
        doc[field] = trigger == null ? BsonNull.Value : trigger;
        doc.Remove(DeviceTriggerTracker.ZoneIn);
        doc.Remove(DeviceTriggerTracker.ZoneOut);
        doc.Remove(DeviceTriggerTracker.TimeSpent);
        if (trigger == null || zoneInMs is not { } ms) return;

        var zoneIn = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        var index = doc.IndexOfName(field);
        BsonValue timeSpent = zoneOut is { } o ? Math.Round((o - zoneIn).TotalMinutes, 2) : BsonNull.Value;
        doc.InsertAt(++index, new BsonElement(DeviceTriggerTracker.ZoneIn, new BsonDateTime(zoneIn)));
        doc.InsertAt(++index, new BsonElement(DeviceTriggerTracker.ZoneOut, zoneOut is { } z ? new BsonDateTime(z) : BsonNull.Value));
        doc.InsertAt(++index, new BsonElement(DeviceTriggerTracker.TimeSpent, timeSpent));
    }

    private static BsonDocument ToDocument(string tag, TagState s) => new()
    {
        { "_id", tag },
        { "device_id", s.Device is null ? BsonNull.Value : s.Device },
        { "zone_id", s.ZoneId is null ? BsonNull.Value : s.ZoneId },
        { "zone_name", s.ZoneName is null ? BsonNull.Value : s.ZoneName },
        { "floor_plan_id", s.FloorPlanId is null ? BsonNull.Value : s.FloorPlanId },
        { "since", s.Device is null ? BsonNull.Value : new BsonDateTime(s.SinceMs) },
        { "updated_at", new BsonDateTime(s.LastWindow) },
        { "rssi", s.Rssi is { } r ? r : BsonNull.Value },
        { "battery", s.Battery is { } b ? b : BsonNull.Value },
        { "status", s.Status is null ? BsonNull.Value : s.Status },
        { "pending_device", s.Pending is null ? BsonNull.Value : s.Pending },
        { "pending_count", s.PendingCount },
    };

    // ---- state -------------------------------------------------------------------------------------------------

    private static string StateKey(StreamMapping m) => $"{m.Stream}:loc_state";

    private static TagState? Parse(RedisValue v)
    {
        if (v.IsNullOrEmpty) return null;
        try { return JsonSerializer.Deserialize<TagState>(v.ToString()); }
        catch (JsonException) { return null; }
    }

    private async Task<TagState?> FromMongoAsync(string tag, CancellationToken ct)
    {
        var d = await StateCollection.Find(new BsonDocument("_id", tag)).FirstOrDefaultAsync(ct);
        return d is null ? null : FromDocument(d);
    }

    private static TagState FromDocument(BsonDocument d)
    {
        string? Str(string n) => d.GetValue(n, BsonNull.Value) is { IsString: true } s ? s.AsString : null;
        return new TagState
        {
            Device = Str("device_id"),
            ZoneId = Str("zone_id"),
            ZoneName = Str("zone_name"),
            FloorPlanId = Str("floor_plan_id"),
            SinceMs = d.GetValue("since", BsonNull.Value) is { IsValidDateTime: true } s ? s.ToUniversalTime().Let(x => new DateTimeOffset(x).ToUnixTimeMilliseconds()) : 0,
            LastWindow = d.GetValue("updated_at", BsonNull.Value) is { IsValidDateTime: true } u ? new DateTimeOffset(u.ToUniversalTime()).ToUnixTimeMilliseconds() : 0,
            Rssi = Num(d.GetValue("rssi", BsonNull.Value)),
            Battery = Num(d.GetValue("battery", BsonNull.Value)),
            Status = Str("status"),
        };
    }

    // Restart safety: empty / missing loc_state -> take every tag's device and zone from Mongo (buffers start empty).
    private async Task SeedIfEmptyAsync(IDatabase db, string key)
    {
        _seeded = true;
        if (await db.HashLengthAsync(key) > 0) return;
        var rows = await StateCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        if (rows.Count == 0) return;
        await db.HashSetAsync(key, rows.Select(r => new HashEntry(r["_id"].AsString, JsonSerializer.Serialize(FromDocument(r)))).ToArray());
        logger.LogInformation("loc_state was empty: seeded {Count} tags from {Collection}", rows.Count, S.StateCollection);
    }

    private static double? Num(BsonValue v) => v.IsNumeric ? v.ToDouble() : null;
    private static DateTime Time(BsonDocument d) => d["timestamp"].ToUniversalTime();
    private static string TagId(BsonDocument d) => d["meta"][TagKey].ToString()!.ToLowerInvariant();
    private static string GatewayOf(BsonDocument d) => d["meta"][GatewayKey].ToString()!.ToLowerInvariant();
}

internal static class FnExt { public static R Let<T, R>(this T v, Func<T, R> f) => f(v); }
