using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WorkerNetworkService.Services;
using WorkerNetworkService.Settings;

/// <summary>
/// `fast` mode: replays recorded fast-lane entries (JSONL, one {"gw","recv","r":[[tag,rssi,tsMs,kind,device,frame],..]} per line,
/// see export-fast-lane.py) through the real FastLocationEngine, once per parameter set, and reports per set and tag the number of
/// zone switches, flips (A -> B -> A within 60 s), signal losses and - with --truth (CSV: tag,zoneId,tapMs) - the delay from each
/// ground-truth tap to the first switch into that zone. Offline, read-only.
///   dotnet run -- fast --file entries.jsonl [--sets sets.json] [--truth truth.csv]
/// sets.json = JSON array of FastParams (Name, TauSeconds, LeadMarginDb, MinDwellSeconds, StaleSecondsGateway, ...); default = a grid.
/// </summary>
public static class FastReplay
{
    public static async Task RunAsync(string[] args, IMongoDatabase db)
    {
        string Arg(string n) { var i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : ""; }
        var file = Arg("--file");
        if (file == "") { Console.WriteLine("usage: fast --file entries.jsonl [--sets sets.json] [--truth truth.csv]"); return; }

        var lookup = new PlottedDeviceLookup(db, Options.Create(new LocationSettings { PlotCacheSeconds = 100000 }), NullLogger<PlottedDeviceLookup>.Instance);
        var (inZone, _) = await lookup.GetAsync(CancellationToken.None);

        var entries = new List<(long Recv, List<(string Tag, double Rssi, long Ts, bool Rep, string Dev, string Frame)> R)>();
        foreach (var line in File.ReadLines(file).Where(l => l.Length > 2))
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var recv = root.GetProperty("recv").GetInt64();
            var r = root.GetProperty("r");
            using var items = r.ValueKind == JsonValueKind.String ? JsonDocument.Parse(r.GetString()!) : null;
            var list = new List<(string, double, long, bool, string, string)>();
            foreach (var it in (items?.RootElement ?? r).EnumerateArray())
                list.Add((it[0].GetString()!, it[1].GetDouble(), it[2].GetInt64(), it[3].GetString() == "r", it[4].GetString() ?? root.GetProperty("gw").GetString()!, it[5].GetString() ?? ""));
            entries.Add((recv, list));
        }
        entries = entries.OrderBy(e => e.Recv).ToList();
        if (entries.Count == 0) { Console.WriteLine("no entries"); return; }
        Console.WriteLine($"{entries.Count} entries, {entries.Sum(e => e.R.Count)} readings, {(entries[^1].Recv - entries[0].Recv) / 1000.0:F0} s; plotted devices in a zone: {inZone.Count}");

        var sets = Arg("--sets") is { Length: > 0 } sf
            ? JsonSerializer.Deserialize<List<FastParams>>(File.ReadAllText(sf), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
            : Grid();
        var truth = Arg("--truth") is { Length: > 0 } tf
            ? File.ReadLines(tf).Skip(1).Select(l => l.Split(',')).Where(p => p.Length >= 3).Select(p => (Tag: p[0].Trim(), Zone: p[1].Trim(), Tap: long.Parse(p[2], CultureInfo.InvariantCulture))).ToList()
            : [];

        foreach (var p in sets)
        {
            var engine = new FastLocationEngine(p);
            var changes = new List<(long T, string Tag, string? Zone, FastDecisionKind Kind)>();
            long nextTick = entries[0].Recv;
            foreach (var e in entries)
            {
                foreach (var x in e.R) engine.Add(x.Tag, x.Dev, x.Rep, x.Frame, x.Rssi, x.Ts, e.Recv);
                for (; nextTick <= e.Recv; nextTick += 1000)
                    foreach (var d in engine.Evaluate(nextTick, inZone))
                        if (d.Kind != FastDecisionKind.DeviceChange) changes.Add((d.DecidedMs, d.Tag, d.ZoneId, d.Kind));
            }

            var zoneChanges = changes.Where(c => c.Kind == FastDecisionKind.ZoneChange).ToList();
            var flips = 0;
            foreach (var g in changes.Where(c => c.Kind != FastDecisionKind.SignalLost).GroupBy(c => c.Tag))
            {
                var list = g.OrderBy(c => c.T).ToList();
                for (var i = 2; i < list.Count; i++)
                    if (list[i].Zone == list[i - 2].Zone && list[i].T - list[i - 2].T <= 60000) flips++;
            }
            var delays = new List<double>();
            foreach (var t in truth)
            {
                var m = changes.Where(c => c.Tag == t.Tag && c.Zone == t.Zone && c.T >= t.Tap - 5000 && c.T <= t.Tap + 120000).OrderBy(c => c.T).FirstOrDefault();
                if (m.Tag != null) delays.Add((m.T - t.Tap) / 1000.0);
            }
            delays.Sort();
            var hours = Math.Max(0.01, (entries[^1].Recv - entries[0].Recv) / 3600000.0);
            Console.WriteLine($"{p.Name,-18} tau {p.TauSeconds,3} margin {p.LeadMarginDb,4} dwell {p.MinDwellSeconds,3} stale {p.StaleSecondsGateway}/{p.StaleSecondsRepeater}: " +
                $"switches {zoneChanges.Count} ({zoneChanges.Count / hours:F1}/h), flips {flips}, signal lost {changes.Count(c => c.Kind == FastDecisionKind.SignalLost)}" +
                (truth.Count > 0 ? $", truth matched {delays.Count}/{truth.Count}" + (delays.Count > 0 ? $", delay median {delays[(delays.Count - 1) / 2]:F1} s max {delays[^1]:F1} s" : "") : ""));
            if (args.Contains("--per-tag"))
                foreach (var g in zoneChanges.GroupBy(c => c.Tag).OrderBy(g => g.Key))
                    Console.WriteLine($"    {g.Key[^4..]}: {g.Count()} switches");
        }
    }

    private static List<FastParams> Grid()
    {
        var sets = new List<FastParams>();
        foreach (var tau in new[] { 2.0, 3.0 })
            foreach (var margin in new[] { 6.0, 8.0, 10.0 })
                foreach (var dwell in new[] { 1.0, 2.0 })
                    sets.Add(new FastParams { Name = $"t{tau}-m{margin}-d{dwell}", TauSeconds = tau, LeadMarginDb = margin, MinDwellSeconds = dwell });
        return sets;
    }
}
