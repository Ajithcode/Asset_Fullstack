namespace WorkerNetworkService.Services;

/// <summary>Tuning of one fast-resolver variant. All values are time based (seconds / dB), not "number of windows".</summary>
public class FastParams
{
    public string Name { get; set; } = "default";

    /// <summary>EWMA time constant: a sample gets weight 1-exp(-dt/Tau), dt = seconds since the device's previous sample of the tag.</summary>
    public double TauSeconds { get; set; } = 3;

    /// <summary>A challenger must be this many dB stronger (EWMA + device offset) than the current device ...</summary>
    public double LeadMarginDb { get; set; } = 8;

    /// <summary>... continuously for this long before the tag switches.</summary>
    public double MinDwellSeconds { get; set; } = 2;

    /// <summary>A device with no new sample for this long no longer counts (gateway: scans every second, long gaps are rare).</summary>
    public double StaleSecondsGateway { get; set; } = 12;

    /// <summary>Same for repeaters, whose relay frames carry a tag only every 4-5 s (p95 ~24 s, max ~37 s measured).</summary>
    public double StaleSecondsRepeater { get; set; } = 45;

    /// <summary>Per-device stale timeout (device id -> seconds), wins over the two defaults.</summary>
    public Dictionary<string, double> StaleOverrides { get; set; } = new();

    /// <summary>When the current device is stale, the challenger must be at least this strong (raw EWMA, dBm) and have MinSamples samples.</summary>
    public double TakeoverMinRssiDbm { get; set; } = -75;

    public int MinSamples { get; set; } = 2;

    /// <summary>No sample from any device for this long: the tag has "No location" (the visit is closed at its last-seen time).</summary>
    public double SignalLossSeconds { get; set; } = 120;

    /// <summary>Per-device RSSI offset in dB (added to the EWMA before comparing), to level gateways and repeaters.</summary>
    public Dictionary<string, double> RssiOffsets { get; set; } = new();

    /// <summary>Readings scanned more than this long before the tag's last decision are ignored (backlog / replays).</summary>
    public double LateReadingSeconds { get; set; } = 5;

    /// <summary>If a reading's scan time differs from the receive time by more than this, the receive time is used instead.</summary>
    public double MaxClockSkewSeconds { get; set; } = 5;

    public FastParams Clone() => (FastParams)MemberwiseClone();
}

public enum FastDecisionKind { Assign, ZoneChange, DeviceChange, SignalLost }

/// <summary>One decision of the fast resolver. Times are Unix milliseconds.</summary>
public record FastDecision(
    string Tag, FastDecisionKind Kind, string? Device, string? ZoneId, string? ZoneName, string? FloorPlanId,
    string? FromDevice, string? FromZoneId, long DecidedMs, long SinceMs, double? Rssi,
    long LastSampleTsMs, long LastSampleRecvMs, long LastSampleReadMs, long LastSeenMs, IReadOnlyDictionary<string, double> Values);

/// <summary>
/// Per tag + device EWMA of the RSSI and the zone decision on top of it. Pure logic (no Redis / Mongo), so the live worker,
/// the shadow runner and tools/ReplayHarness run the same code. Feed readings with <see cref="Add"/>, call
/// <see cref="Evaluate"/> about once a second (and after each batch).
/// Rules: only devices plotted in a zone compete; value = EWMA + offset; the tag switches when a fresh challenger beats the
/// current device by LeadMarginDb for MinDwellSeconds (or the current device is stale and the challenger passes the takeover
/// guard); first assignment = strongest fresh device; nothing heard from anyone for SignalLossSeconds = SignalLost.
/// Repeater relay frames that the repeater simply re-advertises (same frame hash) are not new samples.
/// </summary>
public sealed class FastLocationEngine(FastParams p)
{
    private sealed class Dev
    {
        public double Ewma;
        public long LastTsMs;
        public long LastRecvMs;
        public long LastReadMs;
        public bool Repeater;
        public string? LastFrame;
        public int Samples;
    }

    private sealed class Tag
    {
        public Dictionary<string, Dev> Devices = new();
        public string? Device, ZoneId, ZoneName, FloorPlanId;
        public long SinceMs;
        public string? Pending;
        public long PendingSinceMs;
        public long LastDecisionMs;
        public long LastAnyRecvMs;
        public long LastAnyTsMs;
        public double? Rssi;
        public long SeededAtMs;
    }

    private readonly Dictionary<string, Tag> _tags = new();
    public FastParams Params => p;

    private Tag Get(string tag) => _tags.TryGetValue(tag, out var t) ? t : _tags[tag] = new Tag();

    /// <summary>Current device / zone of a tag (null device = no location yet).</summary>
    public (string? Device, string? ZoneId, long SinceMs)? Current(string tag) =>
        _tags.TryGetValue(tag, out var t) ? (t.Device, t.ZoneId, t.SinceMs) : null;

    /// <summary>Takes over a known state (restart, or switching a warm shadow engine to live): the evidence buffers are kept.</summary>
    public void Seed(string tag, string? device, string? zoneId, string? zoneName, string? floorPlanId, long sinceMs, long lastSeenMs, long seededAtMs)
    {
        var t = Get(tag);
        t.Device = device; t.ZoneId = zoneId; t.ZoneName = zoneName; t.FloorPlanId = floorPlanId;
        t.SinceMs = sinceMs; t.Pending = null; t.SeededAtMs = seededAtMs;
        if (device != null && t.LastAnyRecvMs == 0) t.LastAnyRecvMs = lastSeenMs;
    }

    /// <summary>Adds one reading. Returns false when it was ignored (re-advertised relay frame, out of order, too late).</summary>
    public bool Add(string tag, string device, bool repeater, string? frame, double rssi, long tsMs, long recvMs, long readMs = 0)
    {
        var t = Get(tag);
        var ts = Math.Abs(tsMs - recvMs) > p.MaxClockSkewSeconds * 1000 ? recvMs : tsMs;
        if (t.LastDecisionMs > 0 && ts < t.LastDecisionMs - (long)(p.LateReadingSeconds * 1000)) return false;

        if (!t.Devices.TryGetValue(device, out var d)) t.Devices[device] = d = new Dev { Repeater = repeater, Ewma = rssi, LastTsMs = ts - 1000 };
        if (repeater && frame is { Length: > 0 } && frame == d.LastFrame) return false; // same relay frame again: old data
        if (ts < d.LastTsMs) return false;                                                // out of order for this device
        d.LastFrame = frame;

        var staleMs = (long)(StaleSeconds(device, repeater) * 1000);
        if (d.Samples == 0 || recvMs - d.LastRecvMs > 3 * staleMs)
            d.Ewma = rssi;
        else
        {
            var dt = Math.Max(0.05, (ts - d.LastTsMs) / 1000.0);
            d.Ewma += (1 - Math.Exp(-dt / p.TauSeconds)) * (rssi - d.Ewma);
        }
        d.LastTsMs = ts; d.LastRecvMs = recvMs; d.LastReadMs = readMs; d.Repeater = repeater; d.Samples++;
        t.LastAnyRecvMs = Math.Max(t.LastAnyRecvMs, recvMs);
        t.LastAnyTsMs = Math.Max(t.LastAnyTsMs, ts);
        return true;
    }

    private double StaleSeconds(string device, bool repeater) =>
        p.StaleOverrides.TryGetValue(device, out var s) ? s : repeater ? p.StaleSecondsRepeater : p.StaleSecondsGateway;

    private double Offset(string device) => p.RssiOffsets.TryGetValue(device, out var o) ? o : 0;

    /// <summary>Evidence per device right now (EWMA + offset, only fresh devices plotted in a zone) - for logs and calibration.</summary>
    public Dictionary<string, double> Values(string tag, long nowMs, IReadOnlyDictionary<string, PlottedDevice> inZone)
    {
        var values = new Dictionary<string, double>();
        if (!_tags.TryGetValue(tag, out var t)) return values;
        foreach (var (id, d) in t.Devices)
            if (Fresh(id, d, nowMs) && inZone.ContainsKey(id)) values[id] = Math.Round(d.Ewma + Offset(id), 1);
        return values;
    }

    private bool Fresh(string id, Dev d, long nowMs) => nowMs - d.LastRecvMs <= StaleSeconds(id, d.Repeater) * 1000;

    public IEnumerable<string> Tags => _tags.Keys;

    /// <summary>Tags that have a current device, with the time anything was last heard from them and the current device's EWMA.</summary>
    public IEnumerable<(string Tag, string Device, long LastSeenMs, double? Rssi)> Seen() =>
        _tags.Where(kv => kv.Value.Device != null).Select(kv => (kv.Key, kv.Value.Device!, kv.Value.LastAnyRecvMs, kv.Value.Rssi));

    /// <summary>Decides for every tag at nowMs; returns the decisions (zone / device changes, first assignment, signal loss).</summary>
    public List<FastDecision> Evaluate(long nowMs, IReadOnlyDictionary<string, PlottedDevice> inZone)
    {
        var decisions = new List<FastDecision>();
        foreach (var (tag, t) in _tags)
        {
            if (t.Devices.Count == 0) continue;
            var fresh = t.Devices.Where(kv => Fresh(kv.Key, kv.Value, nowMs) && inZone.ContainsKey(kv.Key)).ToList();
            var values = fresh.ToDictionary(kv => kv.Key, kv => kv.Value.Ewma + Offset(kv.Key));

            // no current device: strongest fresh device wins (needs MinSamples samples or a strong sample)
            if (t.Device == null)
            {
                var first = fresh.Where(kv => kv.Value.Samples >= p.MinSamples || kv.Value.Ewma >= p.TakeoverMinRssiDbm)
                    .OrderByDescending(kv => values[kv.Key]).ThenBy(kv => kv.Key, StringComparer.Ordinal).FirstOrDefault();
                if (first.Key != null)
                    decisions.Add(Apply(tag, t, inZone[first.Key], FastDecisionKind.Assign, nowMs, first.Value.Ewma, values));
                continue;
            }

            // restart / cut-over: the seeded device has not been heard yet (a repeater needs up to a stale period) - hold the
            // zone until it has evidence or the grace (one repeater stale period) is over, so a weak gateway reading cannot take it
            if (!t.Devices.ContainsKey(t.Device) && nowMs - t.SeededAtMs < p.StaleSecondsRepeater * 1000) { t.Pending = null; continue; }

            var curFresh = t.Devices.TryGetValue(t.Device, out var cur) && Fresh(t.Device, cur, nowMs) && inZone.ContainsKey(t.Device);
            var best = fresh.Where(kv => kv.Key != t.Device)
                .OrderByDescending(kv => values[kv.Key]).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => ((string Id, Dev D)?)(kv.Key, kv.Value)).FirstOrDefault();

            var want = false;
            if (best is { } b)
                want = curFresh
                    ? values[b.Id] >= values[t.Device] + p.LeadMarginDb
                    : b.D.Ewma >= p.TakeoverMinRssiDbm && b.D.Samples >= p.MinSamples;

            if (want)
            {
                var id = best!.Value.Id;
                if (t.Pending != id) { t.Pending = id; t.PendingSinceMs = nowMs; }
                if (nowMs - t.PendingSinceMs >= p.MinDwellSeconds * 1000)
                {
                    var next = inZone[id];
                    var kind = string.Equals(next.ZoneId, t.ZoneId, StringComparison.Ordinal) ? FastDecisionKind.DeviceChange : FastDecisionKind.ZoneChange;
                    decisions.Add(Apply(tag, t, next, kind, nowMs, best.Value.D.Ewma, values));
                }
                continue;
            }
            t.Pending = null;

            if (curFresh) { t.Rssi = cur!.Ewma; continue; }

            // current device silent and nobody else qualifies: signal loss after SignalLossSeconds without any sample
            if (fresh.Count == 0 && nowMs - t.LastAnyRecvMs >= p.SignalLossSeconds * 1000)
            {
                decisions.Add(new FastDecision(tag, FastDecisionKind.SignalLost, null, null, null, null, t.Device, t.ZoneId, nowMs, t.SinceMs,
                    null, t.LastAnyTsMs, t.LastAnyRecvMs, t.LastAnyRecvMs, t.LastAnyRecvMs, values));
                t.Device = null; t.ZoneId = null; t.ZoneName = null; t.FloorPlanId = null; t.Pending = null; t.LastDecisionMs = nowMs;
            }
        }
        return decisions;
    }

    private FastDecision Apply(string tag, Tag t, PlottedDevice next, FastDecisionKind kind, long nowMs, double rssi,
        Dictionary<string, double> values)
    {
        var fromDevice = t.Device; var fromZone = t.ZoneId;
        var zoneChanged = kind != FastDecisionKind.DeviceChange;
        t.Device = next.DeviceId; t.ZoneId = next.ZoneId; t.ZoneName = next.ZoneName; t.FloorPlanId = next.FloorPlanId;
        if (zoneChanged) t.SinceMs = nowMs;
        t.Pending = null; t.LastDecisionMs = nowMs; t.Rssi = rssi;
        var d = t.Devices[next.DeviceId];
        return new FastDecision(tag, kind, next.DeviceId, next.ZoneId, next.ZoneName, next.FloorPlanId, fromDevice, fromZone, nowMs,
            t.SinceMs, rssi, d.LastTsMs, d.LastRecvMs, d.LastReadMs, t.LastAnyRecvMs, values);
    }

    /// <summary>Plot edits (zone renamed / moved without a device change) are not decisions; keep the labels in step.</summary>
    public void RefreshLabels(IReadOnlyDictionary<string, PlottedDevice> inZone)
    {
        foreach (var t in _tags.Values)
            if (t.Device != null && inZone.TryGetValue(t.Device, out var plot) && plot.ZoneId == t.ZoneId)
            { t.ZoneName = plot.ZoneName; t.FloorPlanId = plot.FloorPlanId; }
    }
}
