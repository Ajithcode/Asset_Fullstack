using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using WorkerNetworkService.Services;
using WorkerNetworkService.Settings;

// Offline, read-only. See README.md. Never prints the connection string.
var mode = args.Length > 0 ? args[0] : "replay";
var fromUtc = DateTime.Parse(Arg("--from", "2026-11-26T11:17:00Z"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
var toUtc = DateTime.UtcNow;
var uri = Environment.GetEnvironmentVariable("MONGO_URI") ?? throw new InvalidOperationException("MONGO_URI not set");
var db = new MongoClient(uri).GetDatabase(Environment.GetEnvironmentVariable("MONGO_DB") ?? "Asset360Dev");

if (mode == "fast") { await FastReplay.RunAsync(args, db); return; }

string Arg(string name, string fallback) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
string Utc(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("HH:mm:ss");
string Ist(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.AddHours(5.5).ToString("HH:mm:ss");
string Both(long ms) => $"{Utc(ms)}Z / {Ist(ms)} IST";
long Ms(DateTime d) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

List<(string Tag, Move Move)> MissedLast = new();

// ---- data ---------------------------------------------------------------------------------------------------------
var lookup = new PlottedDeviceLookup(db, Options.Create(new LocationSettings { PlotCacheSeconds = 100000 }), NullLogger<PlottedDeviceLookup>.Instance);
var (inZone, allPlotted) = await lookup.GetAsync(CancellationToken.None);
string ZoneName(string? z) => inZone.Values.FirstOrDefault(d => d.ZoneId == z)?.ZoneName ?? z ?? "-";
string DevName(string d) => $"{d[^3..]}({ZoneName(inZone.TryGetValue(d, out var p) ? p.ZoneId : null)})";

var readFrom = mode == "live" ? fromUtc.AddMinutes(-10) : fromUtc;
var docs = await db.GetCollection<BsonDocument>("GatewayTimeSeries").Find(new BsonDocument
{
    { "timestamp", new BsonDocument("$gte", readFrom) }, { "meta.tagid", new BsonDocument("$regex", "^c30000a3f5") }, { "rssi", new BsonDocument("$type", "number") }
}).Sort(new BsonDocument("timestamp", 1)).ToListAsync();
Console.WriteLine($"readings: {docs.Count} (GatewayTimeSeries, tags only) from {readFrom:HH:mm:ss}Z to now; plotted devices in a zone: {string.Join(", ", inZone.Keys.Select(DevName))}");
if (docs.Count == 0) return;

string TagOf(BsonDocument d) => d["meta"]["tagid"].AsString.ToLowerInvariant();
string DevOf(BsonDocument d) => d["meta"]["gatewayid"].AsString.ToLowerInvariant();
long TOf(BsonDocument d) => Ms(d["timestamp"].ToUniversalTime());

// tag -> window -> device -> max rssi (reference for the estimator and the RSSI tables)
var rssi = new Dictionary<string, SortedDictionary<long, Dictionary<string, double>>>();
foreach (var d in docs)
{
    var w = rssi.TryGetValue(TagOf(d), out var x) ? x : rssi[TagOf(d)] = new();
    if (!w.TryGetValue(TOf(d), out var devs)) w[TOf(d)] = devs = new();
    var v = d["rssi"].ToDouble();
    devs[DevOf(d)] = devs.TryGetValue(DevOf(d), out var o) ? Math.Max(o, v) : v;
}
var tags = rssi.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

// ---- reference "actual move" per tag, from the RSSI only ------------------------------------------------------------
var moves = tags.ToDictionary(t => t, t => Moves(rssi[t]));
List<Move> Moves(SortedDictionary<long, Dictionary<string, double>> w)
{
    var ts = w.Keys.ToList();
    var det = new List<(long T, string? Zone)>();
    for (var i = 0; i < ts.Count; i++)
    {
        var zoneBest = new Dictionary<string, double>();
        foreach (var (dev, plot) in inZone)
            for (var j = i; j >= 0 && ts[j] > ts[i] - 30000; j--)
                if (w[ts[j]].TryGetValue(dev, out var v))
                {
                    zoneBest[plot.ZoneId] = zoneBest.TryGetValue(plot.ZoneId, out var b) ? Math.Max(b, v) : v;
                    break;
                }
        var ranked = zoneBest.OrderByDescending(z => z.Value).ToList();
        det.Add((ts[i], ranked.Count == 0 ? null : ranked.Count == 1 || ranked[0].Value >= ranked[1].Value + 6 ? ranked[0].Key : null));
    }

    var result = new List<Move>();
    string? stable = null;
    for (var i = 0; i < det.Count; i++)
    {
        if (det[i].Zone is null) continue;
        if (stable is null) { stable = det[i].Zone; continue; }
        if (det[i].Zone == stable) continue;
        var ahead = det.Where(x => x.T >= det[i].T && x.T <= det[i].T + 45000 && x.Zone is not null).ToList();
        if (ahead.Count >= 2 && ahead.All(x => x.Zone == det[i].Zone))
        {
            result.Add(new Move(det[i].T, det[i].Zone!));
            stable = det[i].Zone;
        }
    }
    return result;
}

// ---- replay ---------------------------------------------------------------------------------------------------------
List<Switch> Replay(LocationSettings s, out List<Switch> deviceOnly)
{
    var resolver = new TagLocationResolver(null!, null!, null!, Options.Create(s), NullLogger<TagLocationResolver>.Instance);
    var mapping = new StreamMapping { DeviceTriggerField = "device_trigger" };
    var states = new Dictionary<string, TagLocationResolver.TagState>();
    var zoneSwitches = new List<Switch>();
    deviceOnly = new();
    foreach (var window in docs.Where(d => TOf(d) >= Ms(fromUtc)).GroupBy(TOf).OrderBy(g => g.Key))
        foreach (var tag in window.GroupBy(TagOf))
        {
            if (!states.TryGetValue(tag.Key, out var st)) states[tag.Key] = st = new TagLocationResolver.TagState();
            var (bd, bz) = (st.Device, st.ZoneId);
            resolver.Resolve(st, tag.ToList(), window.Key, DateTimeOffset.FromUnixTimeMilliseconds(window.Key).UtcDateTime, inZone, mapping);
            if (bd is null || st.Device == bd) continue;
            (st.ZoneId != bz ? zoneSwitches : deviceOnly).Add(new Switch(tag.Key, window.Key, bd, st.Device!, bz!, st.ZoneId!));
        }
    return zoneSwitches;
}

// match switches to reference moves; flips = switch back to the previous zone within 5 min
Summary Analyse(List<Switch> sw)
{
    List<(string Tag, Move Move)> missedList;
    var delays = new List<double>();
    int missed = 0, spurious = 0, flips = 0, matched = 0;
    missedList = new();
    var used = new HashSet<Switch>();
    foreach (var t in tags)
    {
        var mine = sw.Where(x => x.Tag == t).OrderBy(x => x.T).ToList();
        var mv = moves[t];
        for (var i = 0; i < mv.Count; i++)
        {
            var end = i + 1 < mv.Count ? mv[i + 1].T : mv[i].T + 600000;
            var hit = mine.FirstOrDefault(x => x.ToZone == mv[i].Zone && x.T >= mv[i].T - 30000 && x.T < end && !used.Contains(x));
            if (hit is null) { missed++; missedList.Add((t, mv[i])); continue; }
            used.Add(hit); matched++; delays.Add((hit.T - mv[i].T) / 1000.0);
        }
        spurious += mine.Count(x => !used.Contains(x));
        for (var i = 1; i < mine.Count; i++)
            if (mine[i].ToZone == mine[i - 1].FromZone && mine[i].T - mine[i - 1].T <= 300000) flips++;
    }
    MissedLast = missedList;
    return new Summary(matched, missed, spurious, flips, delays);
}

if (mode == "replay")
{
    LocationSettings Set7(int minSamples, double strong) => new() { RssiWindowCount = 5, RssiMinWindows = 2, SwitchConsecutiveWindows = 1, SwitchMarginDb = 6, CurrentDeviceSilenceSeconds = 60, FastSwitchMarginDb = 15, TakeoverMinSamples = minSamples, TakeoverStrongRssiDbm = strong };
    var sets = new (string Name, LocationSettings S)[]
    {
        ("1 old current (w3 min2 cons1 margin6 silence30 fast15, no guard)", new() { RssiWindowCount = 3, RssiMinWindows = 2, SwitchConsecutiveWindows = 1, SwitchMarginDb = 6, CurrentDeviceSilenceSeconds = 30, FastSwitchMarginDb = 15, TakeoverMinSamples = 1 }),
        ("7  no guard   (w5 min2 cons1 margin6 silence60 fast15)", Set7(1, -75)),
        ("7g -75 dBm    (guard: 2 samples or one >= -75)", Set7(2, -75)),
        ("7g -70 dBm    (guard: 2 samples or one >= -70)", Set7(2, -70)),
        ("7g -65 dBm    (guard: 2 samples or one >= -65)", Set7(2, -65)),
        ("7g -60 dBm    (guard: 2 samples or one >= -60)", Set7(2, -60)),
    };
    var span = (Ms(toUtc) - Ms(fromUtc)) / 60000.0;
    Console.WriteLine($"\nreplay window: {fromUtc:HH:mm:ss}Z .. now ({span:F0} min). Reference moves (RSSI-estimated, set-independent): {moves.Values.Sum(m => m.Count)}");
    foreach (var t in tags) foreach (var m in moves[t]) Console.WriteLine($"   ref move {t[^4..]} -> {ZoneName(m.Zone)} at {Both(m.T)}");
    foreach (var (name, s) in sets)
    {
        var sw = Replay(s, out var devOnly);
        var a = Analyse(sw);
        Console.WriteLine($"\n=== set {name}");
        Console.WriteLine("  zone switches per trolley: " + string.Join(" ", tags.Select(t => $"{t[^3..]}:{sw.Count(x => x.Tag == t)}")) + $"   (total {sw.Count}, same-zone device switches {devOnly.Count})");
        var sorted = a.Delays.OrderBy(x => x).ToList();
        Console.WriteLine($"  flips (back to previous zone within 5 min): {a.Flips}; matched moves {a.Matched}/{a.Matched + a.Missed}, missed {a.Missed}, spurious switches {a.Spurious}");
        if (Arg("--show", "0") != "0" && name.StartsWith(Arg("--show", "0")))
        {
            var shown = 0;
            foreach (var g in sw.GroupBy(x => x.Tag))
            {
                var m = g.OrderBy(x => x.T).ToList();
                for (var i = 1; i < m.Count && shown < 4; i++)
                    if (m[i].ToZone == m[i - 1].FromZone && m[i].T - m[i - 1].T <= 300000) { Console.WriteLine("  FLIP example (first switch then the reversal):"); PrintSwitch(m[i - 1]); PrintSwitch(m[i]); shown++; }
            }
        }
        foreach (var (label, lo, hi) in new[] { ("11:17-11:36 (before the 11:37-11:42 moves)", "11:17:00", "11:36:00"), ("11:36-11:50 (moves happened)", "11:36:00", "11:50:30"), ("11:50:30-now (nothing moved)", "11:50:30", "23:59:59") })
        {
            var l = TimeSpan.Parse(lo); var h = TimeSpan.Parse(hi);
            var part = sw.Where(x => { var tod = DateTimeOffset.FromUnixTimeMilliseconds(x.T).UtcDateTime.TimeOfDay; return tod >= l && tod < h; }).ToList();
            var fl = 0;
            foreach (var g in part.GroupBy(x => x.Tag)) { var m = g.OrderBy(x => x.T).ToList(); for (var i = 1; i < m.Count; i++) if (m[i].ToZone == m[i - 1].FromZone && m[i].T - m[i - 1].T <= 300000) fl++; }
            Console.WriteLine($"    {label}: {part.Count} switches, {fl} flips");
        }
        foreach (var (mt, mm) in MissedLast) Console.WriteLine($"  MISSED reference move: {mt[^4..]} -> {ZoneName(mm.Zone)} at {Both(mm.T)}");
        foreach (var x in sw.Where(x => TimeSpan.FromMilliseconds(x.T % 86400000) >= TimeSpan.Parse("11:50:30")))
            Console.WriteLine($"  switch after 11:50:30: {x.Tag[^4..]} {ZoneName(x.FromZone)} -> {ZoneName(x.ToZone)} at {Both(x.T)}");
        Console.WriteLine(sorted.Count == 0 ? "  delay: n/a" : $"  delay vs RSSI-estimated move: median {sorted[sorted.Count / 2]:F0} s, max {sorted[^1]:F0} s, all: {string.Join(", ", sorted.Select(x => x.ToString("F0")))}");
    }
}
else
{
    // real switches of the running worker: "out" documents since --from
    var outs = await db.GetCollection<BsonDocument>("NetworkTimeSeries").Find(new BsonDocument { { "device_trigger", "out" }, { "timestamp", new BsonDocument("$gte", fromUtc) } }).Sort(new BsonDocument("timestamp", 1)).ToListAsync();
    var real = new List<Switch>();
    foreach (var o in outs)
    {
        var tag = TagOf(o); var t = TOf(o);
        var next = await db.GetCollection<BsonDocument>("NetworkTimeSeries").Find(new BsonDocument { { "meta.tagid", o["meta"]["tagid"] }, { "device_trigger", "in" }, { "timestamp", new BsonDocument("$gte", o["timestamp"]) } }).Sort(new BsonDocument("timestamp", 1)).Limit(1).FirstOrDefaultAsync();
        var from = DevOf(o); var to = next is null ? "?" : DevOf(next);
        var fz = inZone.TryGetValue(from, out var f) ? f.ZoneId : "?"; var tz = inZone.TryGetValue(to, out var tt) ? tt.ZoneId : "?";
        real.Add(new Switch(tag, t, from, to, fz, tz));
    }
    var a = Analyse(real);
    Console.WriteLine($"\nREAL zone switches since {fromUtc:HH:mm:ss}Z: {real.Count}; per trolley: " + string.Join(" ", tags.Select(t => $"{t[^3..]}:{real.Count(x => x.Tag == t)}")));
    Console.WriteLine($"flips (reversed within 5 min): {a.Flips}; reference moves in the same period: {tags.Sum(t => moves[t].Count(m => m.T >= Ms(fromUtc) - 30000))}; matched {a.Matched}, missed {a.Missed}, switches without a clear reference move {a.Spurious}");
    foreach (var s in real) PrintSwitch(s);
}

void PrintSwitch(Switch s)
{
        var mv = moves[s.Tag].Where(m => m.Zone == s.ToZone && m.T >= s.T - 600000 && m.T <= s.T + 30000).OrderByDescending(m => m.T).FirstOrDefault();
        Console.WriteLine($"\n- trolley {s.Tag[^4..]}: {ZoneName(s.FromZone)} -> {ZoneName(s.ToZone)} ({s.FromDev[^3..]} -> {s.ToDev[^3..]})");
        Console.WriteLine($"    estimated move: {(mv is null ? "no clear RSSI move found" : Both(mv.T))}");
        Console.WriteLine($"    switch (window of zone_in/zone_out): {Both(s.T)}" + (mv is null ? "" : $"   delay {(s.T - mv.T) / 1000.0:F0} s"));
        Console.WriteLine("    RSSI per window (dBm)   time UTC / IST        old " + s.FromDev[^3..] + "   new " + s.ToDev[^3..] + "   other devices");
        foreach (var kv in rssi[s.Tag].Where(k => k.Key >= s.T - 90000 && k.Key <= s.T + 45000))
        {
            string V(string d) => kv.Value.TryGetValue(d, out var v) ? v.ToString("F0") : "-";
            var others = string.Join(" ", kv.Value.Where(x => x.Key != s.FromDev && x.Key != s.ToDev && inZone.ContainsKey(x.Key)).Select(x => $"{x.Key[^3..]}:{x.Value:F0}"));
            Console.WriteLine($"      {Utc(kv.Key)}Z / {Ist(kv.Key)}{(kv.Key == s.T ? "  <== switch" : "")}   {V(s.FromDev),4}   {V(s.ToDev),4}   {others}");
        }
}

record Move(long T, string Zone);
record Switch(string Tag, long T, string FromDev, string ToDev, string FromZone, string ToZone);
record Summary(int Matched, int Missed, int Spurious, int Flips, List<double> Delays);
