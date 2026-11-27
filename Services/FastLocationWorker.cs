using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Reads the NetworkServer fast lane (Redis stream, consumer group, XACK after processing) and runs the fast resolver:
/// one <see cref="FastLocationEngine"/> per variant (variant 0 = Location:Fast:Params, the one that can go live).
///   Shadow (Location:Fast:Shadow): every decision of every variant goes to the shadow log (Mongo LocationShadowLog), plus the
///     15 s resolver's real changes ("official", seen as TagLocationState changes) and a snapshot of the per-device evidence every
///     SnapshotSeconds. Nothing else is written: no TagLocationState, ZoneVisit, alerts.
///   Live (Location:FastMode or the Redis flag = 1): the primary variant's decisions are applied by <see cref="FastStateWriter"/>.
/// The flag is re-read every 5 s, so the cut-over (and the way back) needs no restart.
/// </summary>
public class FastLocationWorker(
    IConnectionMultiplexer redis,
    IMongoDatabase database,
    PlottedDeviceLookup plots,
    FastStateWriter writer,
    FastModeFlag flag,
    FastStateStore store,
    TagLocationResolver slowResolver,
    IOptions<LocationSettings> location,
    IOptions<FastLocationSettings> fastOptions,
    IOptions<RedisSettings> redisSettings,
    IOptions<List<StreamMapping>> mappings,
    ILogger<FastLocationWorker> logger) : BackgroundService
{
    private FastLocationSettings F => fastOptions.Value;
    private readonly List<FastLocationEngine> _engines = [];
    private readonly List<BsonDocument> _log = [];
    private readonly Dictionary<string, string?> _official = new();
    private bool _officialPrimed;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _engines.Add(new FastLocationEngine(F.Params));
        foreach (var v in F.Variants) _engines.Add(new FastLocationEngine(v.Apply(F.Params)));
        logger.LogInformation("Fast resolver: variants {Variants}; shadow={Shadow}; live={Live}", string.Join(", ", _engines.Select(e => $"{e.Params.Name}(margin {e.Params.LeadMarginDb} dB, dwell {e.Params.MinDwellSeconds} s)")), F.Shadow, flag.Active);

        var db = redis.GetDatabase();
        var consumer = redisSettings.Value.ConsumerName + "-fast";
        var readPosition = "0";
        var started = false;
        long lastFlagCheck = 0, lastEval = 0, lastTouch = 0, lastSnap = 0, lastOfficial = 0;
        var wasActive = flag.Active;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = Now();
                if (now - lastFlagCheck > 5000) { lastFlagCheck = now; await CheckFlagAsync(db, ct); }
                var active = flag.Active;
                if (!active && !F.Shadow) { started = false; await Task.Delay(1000, ct); continue; }

                if (!started)
                {
                    await writer.EnsureIndexesAsync(ct);
                    await SeedAsync(ct);
                    if (active) await writer.ReconcileVisitsAsync(ct);
                    try { await db.StreamCreateConsumerGroupAsync(F.Stream, F.Group, "$", createStream: true); }
                    catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP")) { }
                    started = true; wasActive = active;
                }
                if (active != wasActive) { await OnModeChangedAsync(active, ct); wasActive = active; }

                var entries = await db.StreamReadGroupAsync(F.Stream, F.Group, consumer, readPosition, F.BatchSize);
                if (entries.Length == 0 && readPosition == "0") readPosition = ">";
                var readMs = Now();
                foreach (var e in entries) Ingest(e, readMs);

                now = Now();
                if (entries.Length > 0 || now - lastEval >= F.EvalIntervalMs)
                {
                    if (now - lastEval >= 200) { lastEval = now; await EvaluateAsync(now, active, ct); }
                }
                if (entries.Length > 0)
                    await db.StreamAcknowledgeAsync(F.Stream, F.Group, entries.Select(e => e.Id).ToArray());

                if (active && now - lastTouch >= F.LastSeenThrottleSeconds * 1000)
                {
                    lastTouch = now;
                    await writer.TouchAsync(_engines[0].Seen().Where(s => now - s.LastSeenMs < 60_000).Select(s => (s.Tag, s.LastSeenMs, s.Rssi)), ct);
                }
                if (F.Shadow && !active)
                {
                    if (now - lastOfficial >= 1000) { lastOfficial = now; await PollOfficialAsync(now, ct); }
                    if (now - lastSnap >= F.SnapshotSeconds * 1000) { lastSnap = now; await SnapshotAsync(now, ct); }
                }
                await FlushLogAsync(ct);

                if (entries.Length == 0) await Task.Delay(50, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (RedisServerException ex) when (ex.Message.StartsWith("NOGROUP")) { started = false; readPosition = "0"; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fast resolver error; retrying");
                readPosition = "0";
                await Task.Delay(2000, ct);
            }
        }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private async Task CheckFlagAsync(IDatabase db, CancellationToken ct)
    {
        var v = await db.StringGetAsync(F.FlagKey);
        var want = v.IsNullOrEmpty ? location.Value.FastMode : v.ToString() is "1" or "true" or "True";
        if (want != flag.Active)
        {
            logger.LogWarning("Fast mode flag changed: {Old} -> {New} (source: {Source})", flag.Active, want, v.IsNullOrEmpty ? "config" : "redis key");
            flag.Active = want;
        }
    }

    // Everyone starts from the 15 s resolver's current state (device / zone / since), evidence buffers start empty.
    private async Task SeedAsync(CancellationToken ct)
    {
        var rows = await writer.LoadStateAsync(ct);
        foreach (var r in rows)
        {
            foreach (var e in _engines) e.Seed(r.Tag, r.Device, r.Zone, r.ZoneName, r.Floor, r.SinceMs, r.SeenMs);
            if (r.Device != null) store.Current[r.Tag] = (r.Device, r.SinceMs);
            _official[r.Tag] = r.Zone;
        }
        _officialPrimed = true;
        logger.LogInformation("Fast resolver seeded with {Count} tags from TagLocationState", rows.Count);
    }

    private async Task OnModeChangedAsync(bool active, CancellationToken ct)
    {
        if (active)
        {
            // cut-over: the primary engine continues from what TagLocationState says right now, then owns it
            await SeedAsync(ct);
            await writer.ReconcileVisitsAsync(ct);
            logger.LogWarning("FAST MODE LIVE: the fast resolver now writes TagLocationState / ZoneVisit");
        }
        else
        {
            var mapping = mappings.Value.FirstOrDefault(m => m.DeviceTriggerField != null);
            if (mapping != null) await slowResolver.ResetFromMongoAsync(mapping);
            logger.LogWarning("FAST MODE OFF: the 15 s resolver resumes (re-seeded from TagLocationState)");
        }
    }

    private void Ingest(StreamEntry entry, long readMs)
    {
        if (entry.IsNull || entry.Values.Length == 0) return;
        string? gw = null, r = null; long recv = readMs;
        foreach (var v in entry.Values)
        {
            if (v.Name == "gw") gw = v.Value; else if (v.Name == "r") r = v.Value; else if (v.Name == "recv" && long.TryParse(v.Value, out var x)) recv = x;
        }
        if (r == null) return;
        using var doc = JsonDocument.Parse(r);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var tag = item[0].GetString()!; var rssi = item[1].GetDouble(); var ts = item[2].GetInt64();
            var repeater = item[3].GetString() == "r"; var device = item[4].GetString() ?? gw!; var frame = item[5].GetString();
            foreach (var e in _engines) e.Add(tag, device, repeater, frame, rssi, ts, recv, readMs);
        }
    }

    private async Task EvaluateAsync(long now, bool active, CancellationToken ct)
    {
        var (inZone, _) = await plots.GetAsync(ct);
        for (var i = 0; i < _engines.Count; i++)
        {
            var engine = _engines[i];
            engine.RefreshLabels(inZone);
            foreach (var d in engine.Evaluate(now, inZone))
            {
                if (F.Shadow && !active) _log.Add(LogDocument(engine.Params.Name, "decision", d, now));
                if (i == 0 && active)
                {
                    await writer.ApplyAsync(d, now, ct);
                    logger.LogInformation("Fast decision {Tag}: {Kind} {From} -> {To} (device {Device}, {Delay} ms after the sample)",
                        d.Tag, d.Kind, d.FromZoneId, d.ZoneId, d.Device, d.DecidedMs - d.LastSampleTsMs);
                }
            }
        }
    }

    private static BsonDocument LogDocument(string variant, string type, FastDecision d, long now) => new()
    {
        { "at", new BsonDateTime(now) }, { "atMs", now }, { "tag", d.Tag }, { "variant", variant }, { "type", type }, { "kind", d.Kind.ToString() },
        { "fromZone", d.FromZoneId is null ? BsonNull.Value : d.FromZoneId }, { "toZone", d.ZoneId is null ? BsonNull.Value : d.ZoneId },
        { "fromDevice", d.FromDevice is null ? BsonNull.Value : d.FromDevice }, { "device", d.Device is null ? BsonNull.Value : d.Device },
        { "rssi", d.Rssi is { } r ? r : BsonNull.Value }, { "deviceTsMs", d.LastSampleTsMs }, { "recvMs", d.LastSampleRecvMs },
        { "readMs", d.LastSampleReadMs }, { "decidedMs", d.DecidedMs },
        { "values", new BsonDocument(d.Values.Select(kv => new BsonElement(kv.Key, kv.Value))) },
    };

    // The 15 s resolver's real changes: a zone_id change in TagLocationState (observed within ~1 s).
    private async Task PollOfficialAsync(long now, CancellationToken ct)
    {
        var rows = await writer.LoadStateAsync(ct);
        foreach (var r in rows)
        {
            if (_officialPrimed && (!_official.TryGetValue(r.Tag, out var old) || old != r.Zone))
                _log.Add(new BsonDocument
                {
                    { "at", new BsonDateTime(now) }, { "atMs", now }, { "tag", r.Tag }, { "variant", "official" }, { "type", "official" },
                    { "kind", r.Zone == null ? "SignalLost" : "ZoneChange" }, { "fromZone", _official.TryGetValue(r.Tag, out var o) && o != null ? o : BsonNull.Value },
                    { "toZone", r.Zone is null ? BsonNull.Value : r.Zone }, { "device", r.Device is null ? BsonNull.Value : r.Device },
                    { "decidedMs", r.SinceMs }, { "observedMs", now },
                });
            _official[r.Tag] = r.Zone;
        }
        _officialPrimed = true;
    }

    // Per tag: the fresh evidence per device (EWMA + offset) of the primary variant - for tuning and offset calibration.
    private async Task SnapshotAsync(long now, CancellationToken ct)
    {
        var (inZone, _) = await plots.GetAsync(ct);
        var e = _engines[0];
        foreach (var tag in e.Tags)
        {
            var values = e.Values(tag, now, inZone);
            if (values.Count == 0) continue;
            _log.Add(new BsonDocument
            {
                { "at", new BsonDateTime(now) }, { "atMs", now }, { "tag", tag }, { "variant", e.Params.Name }, { "type", "snap" },
                { "device", e.Current(tag)?.Device is { } dev ? dev : BsonNull.Value },
                { "values", new BsonDocument(values.Select(kv => new BsonElement(kv.Key, kv.Value))) },
            });
        }
    }

    private async Task FlushLogAsync(CancellationToken ct)
    {
        if (_log.Count == 0) return;
        var batch = _log.ToList(); _log.Clear();
        await database.GetCollection<BsonDocument>(F.ShadowLogCollection).InsertManyAsync(batch, new InsertManyOptions { IsOrdered = false }, ct);
    }
}
