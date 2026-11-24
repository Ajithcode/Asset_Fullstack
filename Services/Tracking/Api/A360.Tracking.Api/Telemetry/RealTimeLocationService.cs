using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Telemetry;

/// <summary>
/// Real-Time Location page: where each tracked asset is, whether it is moving, how fresh and how reliable that
/// location is, its recent zone trail and location events. Shares the location engine of Tracking &amp; Telemetry
/// (<see cref="TelemetryOverviewService"/>): BLE proximity, location = zone of the gateway hearing the tag strongest.
/// </summary>
/// <remarks>
/// Definitions (BLE proximity has no coordinates, so no metre accuracy is invented):
/// - Freshness: Live (heard ≤ 2 min), Stale (≤ 10 min), Offline (older), Unknown (never heard).
/// - Movement: Moving (zone changed in the last 5 min), Stationary (same zone for 5 min while live), Unknown otherwise.
///   Moving + Stationary + Unknown = assets in view.
/// - Accuracy: "Zone level" when the gateway is plotted in a zone, "Gateway range" otherwise.
/// - Confidence: from how clearly one gateway hears the tag over the others in the last 2 min —
///   High (one gateway, or ≥ 8 dB stronger than the next), Medium (≥ 3 dB), Low (closer / weak signal), Unknown (not live).
/// </remarks>
public sealed class RealTimeLocationService(IMongoDatabase database, TelemetryOverviewService telemetry)
{
    private static readonly TimeSpan MovingWithin = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ConfidenceWindow = TimeSpan.FromMinutes(2);

    public static readonly IReadOnlyDictionary<string, TimeSpan> TrailRanges = new Dictionary<string, TimeSpan>
    {
        ["30m"] = TimeSpan.FromMinutes(30),
        ["1h"] = TimeSpan.FromHours(1),
        ["24h"] = TimeSpan.FromHours(24),
        ["7d"] = TimeSpan.FromDays(7)
    };

    private IMongoCollection<BsonDocument> C(string name) => database.GetCollection<BsonDocument>(name);

    // ================================================================= overview

    /// <param name="floorPlanId">Limits "assets in view" to one floor plan (the map shown); all when empty.</param>
    public async Task<RtlOverview> GetOverviewAsync(string? siteId, string? floorPlanId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var overview = await telemetry.GetOverviewAsync("1h", siteId, ct, eventLimit: 0);
        var tagIds = overview.Assets.SelectMany(a => a.Tags.Select(t => t.TagId)).ToList();
        var recentMoves = await telemetry.LocationChangesAsync(tagIds, now - MovingWithin - TimeSpan.FromMinutes(5), MovingWithin, ct);
        var hearing = await HearingAsync(tagIds, now - ConfidenceWindow, ct);
        var expected = await ExpectedLocationsAsync(overview.Assets.Select(a => a.AssetId), ct);
        var alerts = await LocationAlertsAsync(ct);

        var rows = new List<RtlAsset>();
        foreach (var a in overview.Assets)
        {
            var tags = a.Tags.Select(t => t.TagId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var freshness = a.LastSeenAt is null ? "Unknown" : a.Connectivity switch { "Online" => "Live", "Idle" => "Stale", _ => "Offline" };
            var lastMove = recentMoves.Where(m => tags.Contains(m.TagId) && m.FromGateway is not null).OrderByDescending(m => m.At).FirstOrDefault();
            var movement = freshness != "Live" ? "Unknown" : lastMove is not null && now - lastMove.At <= MovingWithin ? "Moving" : "Stationary";

            // confidence: margin of the strongest gateway over the next one, over the asset's tags
            var heard = hearing.Where(h => tags.Contains(h.TagId)).OrderByDescending(h => h.Rssi).ToList();
            var gatewaysHearing = heard.Select(h => h.GatewayId).Distinct().Count();
            double? margin = heard.Count >= 2 ? Math.Round(heard[0].Rssi - heard.First(h => h.GatewayId != heard[0].GatewayId).Rssi, 1) : null;
            var confidence = freshness != "Live" || heard.Count == 0 ? "Unknown"
                : heard[0].Rssi < -90 ? "Low"
                : gatewaysHearing == 1 || margin >= 8 ? "High"
                : margin >= 3 ? "Medium" : "Low";
            var confidenceBasis = confidence == "Unknown" ? "No live readings in the last 2 minutes"
                : gatewaysHearing == 1 ? $"Only one gateway hears it ({heard[0].Rssi:0} dBm)"
                : $"Strongest gateway {margin:0.#} dB above the next of {gatewaysHearing} hearing it";

            expected.TryGetValue(a.AssetId, out var exp);
            var comparison = Compare(a.Location, exp);
            var assetAlerts = alerts.Count(x => x.AssetId is not null && (string.Equals(x.AssetId, a.AssetId, StringComparison.OrdinalIgnoreCase) || string.Equals(x.AssetId, a.AssetTag, StringComparison.OrdinalIgnoreCase)));

            rows.Add(new RtlAsset(
                a.AssetId, a.AssetTag, a.Name, a.AssetType, a.OperationalStatus, a.Technology,
                a.Tags.FirstOrDefault()?.TagId.ToUpperInvariant(), a.Tags.Select(t => t.TagId.ToUpperInvariant()).ToList(),
                a.Location, freshness, movement, lastMove?.At, a.LastLocationAt,
                LocationSource(a.Technology), a.Location is null ? "Unknown" : a.Location.Accuracy, confidence, confidenceBasis,
                gatewaysHearing, margin, a.Rssi, assetAlerts, exp, comparison.Status, comparison.Note));
        }

        if (!string.IsNullOrWhiteSpace(floorPlanId))
        {
            rows = rows.Where(r => r.Location?.FloorPlanId == floorPlanId).ToList();
        }

        var inView = rows.Select(r => r.AssetId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kpis = new RtlKpis(rows.Count, rows.Count(r => r.Movement == "Moving"), rows.Count(r => r.Movement == "Stationary"), rows.Count(r => r.Movement == "Unknown"),
            alerts.Count(x => x.Status == "Open" && x.AssetId is not null && (inView.Contains(x.AssetId) || rows.Any(r => string.Equals(r.AssetTag, x.AssetId, StringComparison.OrdinalIgnoreCase)))));

        return new RtlOverview(now, kpis, rows.OrderBy(r => r.Name).ToList(), overview.Gateways);
    }

    // ================================================================= trail

    /// <summary>Zone-by-zone trail (BLE proximity: zone transitions, not a precise path).</summary>
    public async Task<RtlTrail?> GetTrailAsync(string assetId, string range, CancellationToken ct)
    {
        var span = TrailRanges.TryGetValue(range, out var s) ? s : TrailRanges["30m"];
        var now = DateTime.UtcNow;
        var overview = await telemetry.GetOverviewAsync("1h", null, ct, eventLimit: 0);
        var asset = overview.Assets.FirstOrDefault(a => string.Equals(a.AssetId, assetId, StringComparison.OrdinalIgnoreCase) || string.Equals(a.AssetTag, assetId, StringComparison.OrdinalIgnoreCase));
        if (asset is null) return null;

        var tagIds = asset.Tags.Select(t => t.TagId).ToList();
        var moves = (await telemetry.LocationChangesAsync(tagIds, now - span, span, ct)).OrderBy(m => m.At).ToList();
        var plots = await PlotsAsync(ct);
        var segments = new List<TrailSegment>();
        for (var i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            // a tag-to-tag duplicate (several tags on one asset) is folded into the running segment
            if (segments.Count > 0 && string.Equals(segments[^1].GatewayId, m.ToGateway, StringComparison.OrdinalIgnoreCase)) continue;
            var to = i + 1 < moves.Count ? moves[i + 1].At : (asset.LastSeenAt ?? now);
            plots.TryGetValue(m.ToGateway, out var p);
            if (segments.Count > 0) segments[^1] = segments[^1] with { To = m.At, Minutes = Math.Round((m.At - segments[^1].From).TotalMinutes, 1) };
            segments.Add(new TrailSegment(m.ToGateway.ToUpperInvariant(), p?.ZoneName, p?.FloorName, m.At, to, Math.Round((to - m.At).TotalMinutes, 1), m.FromGateway is null));
        }

        var zoneChanges = Math.Max(0, segments.Count(x => !x.Initial));
        var byZone = segments.GroupBy(x => x.ZoneName ?? $"Gateway {x.GatewayId}")
            .Select(g => new ZoneTime(g.Key, Math.Round(g.Sum(x => x.Minutes), 1))).OrderByDescending(z => z.Minutes).ToList();
        return new RtlTrail(asset.AssetId, asset.Name, range, now - span, now, segments, zoneChanges, byZone,
            "BLE proximity gives zone transitions, not a precise path; distance travelled is not available.");
    }

    // ================================================================= events (paged)

    /// <summary>Location events (newest first): zone entries / exits from the estimated zone changes, geofence alerts, and stale / lost locations.</summary>
    public async Task<RtlEventPage> GetEventsAsync(string range, string? siteId, int page, int pageSize, string? assetId, string? eventType, CancellationToken ct)
    {
        var (span, _, label) = TelemetryOverviewService.Ranges.TryGetValue(range, out var r) ? r : TelemetryOverviewService.Ranges["24h"];
        var now = DateTime.UtcNow;
        var overview = await telemetry.GetOverviewAsync("1h", siteId, ct, eventLimit: 0);
        var plots = await PlotsAsync(ct);
        string Zone(string gw) => plots.TryGetValue(gw, out var p) && p.ZoneName is not null ? p.ZoneName : $"Gateway {gw.ToUpperInvariant()}";
        string? Floor(string gw) => plots.TryGetValue(gw, out var p) ? p.FloorName : null;

        var events = new List<RtlEvent>();
        var assetByTag = overview.Assets.SelectMany(a => a.Tags.Select(t => (t.TagId, Asset: a))).ToDictionary(x => x.TagId, x => x.Asset, StringComparer.OrdinalIgnoreCase);
        foreach (var m in await telemetry.LocationChangesAsync(assetByTag.Keys.ToList(), now - span, span, ct))
        {
            if (!assetByTag.TryGetValue(m.TagId, out var a)) continue;
            var src = m.TagId.ToUpperInvariant();
            if (m.FromGateway is not null)
            {
                var floorChange = Floor(m.FromGateway) is { } f1 && Floor(m.ToGateway) is { } f2 && f1 != f2;
                events.Add(new RtlEvent($"EXT-{m.At:yyyyMMddHHmm}-{src[^4..]}", m.At, a.AssetId, a.Name, "Zone Exit", Zone(m.FromGateway), Floor(m.FromGateway),
                    $"Left {Zone(m.FromGateway)} for {Zone(m.ToGateway)}", "Info", src, "Recorded"));
                events.Add(new RtlEvent($"ENT-{m.At:yyyyMMddHHmm}-{src[^4..]}", m.At, a.AssetId, a.Name, floorChange ? "Floor Change" : "Zone Entry", Zone(m.ToGateway), Floor(m.ToGateway),
                    $"Entered from {Zone(m.FromGateway)}", "Info", src, "Recorded"));
            }
            else
            {
                events.Add(new RtlEvent($"LOC-{m.At:yyyyMMddHHmm}-{src[^4..]}", m.At, a.AssetId, a.Name, "Location Detected", Zone(m.ToGateway), Floor(m.ToGateway),
                    "First location in this period", "Info", src, "Recorded"));
            }
        }

        // assets whose location is no longer fresh
        foreach (var a in overview.Assets.Where(a => a.LastSeenAt is not null && a.Connectivity != "Online" && a.LastSeenAt >= now - span))
        {
            var lost = a.Connectivity == "Offline";
            var at = a.LastSeenAt!.Value + (lost ? TelemetryOverviewService.IdleAfter : TelemetryOverviewService.OnlineWithin);
            events.Add(new RtlEvent($"{(lost ? "LST" : "STL")}-{at:yyyyMMddHHmm}-{a.AssetId[^Math.Min(4, a.AssetId.Length)..]}", at, a.AssetId, a.Name,
                lost ? "Location Lost" : "Stale Location", a.Location?.ZoneName, a.Location?.FloorName,
                $"No reading since {a.LastSeenAt:HH:mm} UTC", lost ? "Warning" : "Info", a.Tags.FirstOrDefault()?.TagId.ToUpperInvariant(), "Active"));
        }

        // geofence / location alerts (acknowledge and escalate in Geofence & Alerts)
        foreach (var x in await LocationAlertsAsync(ct, now - span))
        {
            events.Add(new RtlEvent(x.AlertId, x.At, x.AssetId, x.AssetName, x.EventType, x.Zone, null, x.Message, x.Severity, x.DeviceId, x.Status));
        }

        IEnumerable<RtlEvent> rows = events;
        if (!string.IsNullOrWhiteSpace(assetId)) rows = rows.Where(e => string.Equals(e.AssetId, assetId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(eventType)) rows = rows.Where(e => string.Equals(e.EventType, eventType.Trim(), StringComparison.OrdinalIgnoreCase));
        var list = rows.OrderByDescending(e => e.At).ToList();
        var size = Math.Clamp(pageSize, 1, 100);
        var pages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)size));
        var current = Math.Clamp(page, 1, pages);
        var types = events.Select(e => e.EventType).Distinct().OrderBy(t => t).ToList();
        return new RtlEventPage(list.Skip((current - 1) * size).Take(size).ToList(), list.Count, current, size, pages, label, types);
    }

    // ================================================================= sources

    private sealed record Heard(string TagId, string GatewayId, double Rssi);
    private sealed record Plot(string? ZoneName, string? FloorName);
    private sealed record AlertRow(string AlertId, DateTime At, string? AssetId, string? AssetName, string EventType, string? Message, string Severity, string Status, string? DeviceId, string? Zone);

    private async Task<List<Heard>> HearingAsync(List<string> tagIds, DateTime since, CancellationToken ct)
    {
        if (tagIds.Count == 0) return [];
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument { { "timestamp", new BsonDocument("$gte", since) }, { "meta.tagid", new BsonDocument("$in", new BsonArray(tagIds)) }, { "rssi", new BsonDocument("$type", "number") }, { "device_trigger", new BsonDocument("$ne", "out") } }),
            new BsonDocument("$group", new BsonDocument { { "_id", new BsonDocument { { "tag", new BsonDocument("$toLower", "$meta.tagid") }, { "gw", new BsonDocument("$toLower", "$meta.gatewayid") } } }, { "rssi", new BsonDocument("$avg", "$rssi") } })
        };
        var rows = await C("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return rows.Select(x => new Heard(x["_id"]["tag"].AsString, x["_id"]["gw"].AsString, Math.Round(x["rssi"].ToDouble(), 1))).ToList();
    }

    /// <summary>The location registered on the asset (Asset Registration › Location), i.e. its expected location.</summary>
    private async Task<Dictionary<string, ExpectedLocation>> ExpectedLocationsAsync(IEnumerable<string> assetIds, CancellationToken ct)
    {
        var ids = assetIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var regex = new BsonArray(ids.Select(k => new BsonRegularExpression($"^{Regex.Escape(k)}$", "i")));
        var rows = await C("AssetRegistration").Find(new BsonDocument { { "is_deleted", new BsonDocument("$ne", true) }, { "asset_registration_id", new BsonDocument("$in", regex) } })
            .Project(Builders<BsonDocument>.Projection.Include("asset_registration_id").Include("location_assignment.location")).ToListAsync(ct);
        var result = new Dictionary<string, ExpectedLocation>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var id = r.GetValue("asset_registration_id", BsonNull.Value);
            if (!id.IsString) continue;
            var loc = r.TryGetValue("location_assignment", out var la) && la.IsBsonDocument && la.AsBsonDocument.TryGetValue("location", out var l) && l.IsBsonDocument ? l.AsBsonDocument : null;
            if (loc is null) continue;
            string? S(string f) => loc.TryGetValue(f, out var v) && v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString : null;
            result[id.AsString] = new ExpectedLocation(S("zone_id"), S("zone_name"), S("floor_id"), S("floor_name"), S("building_name"), S("site_name"), S("location_path"));
        }

        return result;
    }

    private static (string Status, string Note) Compare(AssetLocationDto? live, ExpectedLocation? expected)
    {
        if (expected is null || (expected.ZoneName is null && expected.FloorName is null)) return ("Not set", "No location registered on the asset");
        if (live is null || live.ZoneName is null && live.FloorName is null) return ("Unknown", "No live zone to compare");
        if (expected.ZoneName is not null && live.ZoneName is not null)
        {
            return string.Equals(expected.ZoneName, live.ZoneName, StringComparison.OrdinalIgnoreCase) && (expected.FloorName is null || string.Equals(expected.FloorName, live.FloorName, StringComparison.OrdinalIgnoreCase))
                ? ("Match", "Detected in its registered zone")
                : ("Mismatch", $"Registered in {expected.ZoneName}, detected in {live.ZoneName}");
        }

        return string.Equals(expected.FloorName, live.FloorName, StringComparison.OrdinalIgnoreCase)
            ? ("Match", "Detected on its registered floor")
            : ("Mismatch", $"Registered on {expected.FloorName}, detected on {live.FloorName}");
    }

    private static string LocationSource(string technology) =>
        technology.Contains("BLE", StringComparison.OrdinalIgnoreCase) ? "BLE Proximity"
        : technology.Contains("RFID", StringComparison.OrdinalIgnoreCase) ? "RFID Reader"
        : technology.Contains("GPS", StringComparison.OrdinalIgnoreCase) ? "GPS / GNSS"
        : string.IsNullOrWhiteSpace(technology) ? "Unknown" : technology;

    private async Task<Dictionary<string, Plot>> PlotsAsync(CancellationToken ct)
    {
        var rows = await C("maps_plots").Find(new BsonDocument { { "kind", "device" }, { "is_deleted", new BsonDocument("$ne", true) } }).ToListAsync(ct);
        var result = new Dictionary<string, Plot>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var id = r.GetValue("item_id", BsonNull.Value);
            if (!id.IsString || result.ContainsKey(id.AsString)) continue;
            string? S(BsonDocument d, string f) => d.TryGetValue(f, out var v) && v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString : null;
            var loc = r.TryGetValue("location", out var l) && l.IsBsonDocument ? l.AsBsonDocument : new BsonDocument();
            result[id.AsString] = new Plot(S(r, "zone_name") ?? S(loc, "zone_name"), S(loc, "floor_name"));
        }

        return result;
    }

    /// <summary>Location-related IoT alerts (geofence entry / exit / dwell, restricted zone…).</summary>
    private async Task<List<AlertRow>> LocationAlertsAsync(CancellationToken ct, DateTime? since = null)
    {
        var filter = new BsonDocument
        {
            { "is_deleted", new BsonDocument("$ne", true) },
            { "event_type", new BsonRegularExpression("geofence|zone|dwell|location|restricted", "i") }
        };
        if (since is not null) filter["raised_at"] = new BsonDocument("$gte", since.Value);
        else filter["status"] = "Open";
        var rows = await C("IoTAlert").Find(filter).Sort(new BsonDocument("raised_at", -1)).Limit(1000).ToListAsync(ct);
        string? S(BsonDocument d, string f) => d.TryGetValue(f, out var v) && v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString : null;
        return rows.Select(x =>
        {
            var message = S(x, "message");
            var zone = message is null ? null : Regex.Match(message, @"(?:entered|left|in) (.+?) ·").Groups[1].Value;
            return new AlertRow(S(x, "alert_id") ?? string.Empty, x.GetValue("raised_at", BsonNull.Value).IsBsonDateTime ? x["raised_at"].ToUniversalTime() : DateTime.MinValue,
                S(x, "asset_registration_id"), S(x, "asset_name"), S(x, "event_type") ?? "Location alert", message, S(x, "severity") ?? "Info", S(x, "status") ?? "Open", S(x, "device_id"),
                string.IsNullOrWhiteSpace(zone) ? null : zone);
        }).ToList();
    }
}

// ================================================================= contracts

public sealed record RtlOverview(DateTime GeneratedAt, RtlKpis Kpis, List<RtlAsset> Assets, List<GatewayDto> Gateways);

/// <param name="Unknown">Movement unknown: location stale, offline or never reported. Moving + Stationary + Unknown = AssetsInView.</param>
public sealed record RtlKpis(int AssetsInView, int Moving, int Stationary, int Unknown, int LocationAlerts);

public sealed record ExpectedLocation(string? ZoneId, string? ZoneName, string? FloorId, string? FloorName, string? BuildingName, string? SiteName, string? LocationPath);

/// <param name="Freshness">Live, Stale, Offline or Unknown.</param>
/// <param name="Movement">Moving, Stationary or Unknown.</param>
/// <param name="Accuracy">Zone level / Gateway range (BLE proximity reports no metre accuracy).</param>
/// <param name="Confidence">High, Medium, Low or Unknown (see <see cref="RealTimeLocationService"/>).</param>
/// <param name="Expected">Location registered on the asset; compared with the device-derived location.</param>
/// <param name="ComparisonStatus">Match, Mismatch, Unknown or Not set.</param>
public sealed record RtlAsset(
    string AssetId, string? AssetTag, string Name, string? AssetType, string? OperationalStatus, string Technology,
    string? Tracker, List<string> Tags, AssetLocationDto? Location, string Freshness, string Movement, DateTime? LastZoneChangeAt, DateTime? LastValidLocationAt,
    string LocationSource, string Accuracy, string Confidence, string ConfidenceBasis, int GatewaysHearing, double? SignalMarginDb, double? Rssi,
    int ActiveLocationAlerts, ExpectedLocation? Expected, string ComparisonStatus, string ComparisonNote);

public sealed record TrailSegment(string GatewayId, string? ZoneName, string? FloorName, DateTime From, DateTime To, double Minutes, bool Initial);

public sealed record ZoneTime(string Zone, double Minutes);

public sealed record RtlTrail(string AssetId, string Name, string Range, DateTime From, DateTime To, List<TrailSegment> Segments, int ZoneChanges, List<ZoneTime> TimeInZones, string Note);

/// <param name="Status">Recorded (derived location event), Active (stale / lost now) or the alert status (Open, Acknowledged…).</param>
public sealed record RtlEvent(string EventId, DateTime At, string? AssetId, string? AssetName, string EventType, string? Zone, string? Floor, string? Details, string Severity, string? Source, string Status);

public sealed record RtlEventPage(List<RtlEvent> Items, int Total, int Page, int PageSize, int TotalPages, string RangeLabel, List<string> EventTypes);
