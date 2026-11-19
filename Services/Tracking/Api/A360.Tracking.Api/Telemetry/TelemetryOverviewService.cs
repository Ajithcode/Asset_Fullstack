using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Telemetry;

/// <summary>
/// Tracking &amp; Telemetry page data, built from the live records (read-only):
/// NetworkTimeSeries (tag readings heard by gateways, written by NetworkerService), TrackingTag (tag → asset),
/// AssetRegistration (asset identity and operational status), IoTDevice (registered gateways / devices),
/// maps_plots (where each gateway is plotted: site / building / floor / zone) and IoTAlert (alerts).
/// BLE tags have no GPS: an asset's location is ESTIMATED as the zone of the gateway hearing its tag strongest.
/// </summary>
public sealed class TelemetryOverviewService(IMongoDatabase database)
{
    /// <summary>Heard within this long = Online; within <see cref="IdleAfter"/> = Idle; otherwise Offline.</summary>
    public static readonly TimeSpan OnlineWithin = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(10);
    /// <summary>Readings older than this are shown as stale on the telemetry cards.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    /// <summary>The location is the strongest gateway over this window (not just the newest reading).</summary>
    private static readonly TimeSpan LocationWindow = TimeSpan.FromMinutes(2);

    public static readonly IReadOnlyDictionary<string, (TimeSpan Span, int StepMinutes, string Label)> Ranges =
        new Dictionary<string, (TimeSpan, int, string)>
        {
            ["1h"] = (TimeSpan.FromHours(1), 1, "Last 1 hour"),
            ["24h"] = (TimeSpan.FromHours(24), 15, "Last 24 hours"),
            ["7d"] = (TimeSpan.FromDays(7), 120, "Last 7 days")
        };

    private IMongoCollection<BsonDocument> C(string name) => database.GetCollection<BsonDocument>(name);

    // ================================================================= overview

    /// <param name="eventLimit">Events returned with the overview (the table pages them through the events endpoint).</param>
    public async Task<TelemetryOverview> GetOverviewAsync(string range, string? siteId, CancellationToken ct, int eventLimit = 200)
    {
        var (span, _, label) = Ranges.TryGetValue(range, out var r) ? r : Ranges["24h"];
        var now = DateTime.UtcNow;
        var from = now - span;

        var tags = await LinkedTagsAsync(ct);
        var registrations = await RegistrationsAsync(tags.Select(t => t.AssetKey), ct);
        var gateways = await GatewaysAsync(ct);
        var plots = await PlottedGatewaysAsync(ct);

        // newest reading of every tag (any gateway) and the strongest gateway of the last couple of minutes
        var latest = await LatestPerTagAsync(now - TimeSpan.FromDays(7), ct);
        var strongest = await StrongestGatewayAsync(now - LocationWindow, ct);
        var gatewayLastSeen = await LatestPerGatewayAsync(now - TimeSpan.FromDays(7), ct);

        var alerts = await AlertsAsync(from, ct);
        var openAlerts = alerts.Where(a => a.Status == "Open").ToList();

        // ---- assets: one per linked asset (several tags fold into one asset)
        var assets = new List<TrackedAssetDto>();
        foreach (var group in tags.GroupBy(t => Resolve(registrations, t.AssetKey)?.RegistrationId ?? t.AssetKey, StringComparer.OrdinalIgnoreCase))
        {
            var reg = Resolve(registrations, group.First().AssetKey);
            var tagDtos = group.Select(t =>
            {
                latest.TryGetValue(t.TagId, out var l);
                return new AssetTagDto(t.TagId, t.TagType, t.Technology, l?.At, l?.Battery, l?.Rssi, l?.GatewayId, Connectivity(l?.At, now));
            }).ToList();

            // location: strongest gateway hearing any of the asset's tags in the last window, else the newest gateway
            var best = group.Select(t => strongest.TryGetValue(t.TagId, out var s) ? s : null).Where(s => s is not null)
                .OrderByDescending(s => s!.Rssi).FirstOrDefault();
            var newest = tagDtos.Where(t => t.LastSeenAt is not null).OrderByDescending(t => t.LastSeenAt).FirstOrDefault();
            var gatewayId = best?.GatewayId ?? newest?.GatewayId;
            plots.TryGetValue(gatewayId ?? string.Empty, out var plot);
            gateways.TryGetValue(gatewayId ?? string.Empty, out var gw);
            var location = gatewayId is null
                ? null
                : new AssetLocationDto(
                    gw?.DeviceId ?? gatewayId.ToUpperInvariant(), gw?.Name, plot?.ZoneId, plot?.ZoneName, plot?.FloorName, plot?.BuildingName,
                    plot?.SiteId, plot?.SiteName, plot?.FloorPlanId, plot?.LocationPath,
                    "Estimated", best is not null ? "Strongest gateway in the last 2 minutes" : "Last gateway that heard the tag",
                    plot?.ZoneName is not null ? "Zone level" : "Gateway range", best?.At ?? newest?.LastSeenAt);

            var lastSeen = tagDtos.Max(t => t.LastSeenAt);
            var battery = tagDtos.Where(t => t.Battery is not null).OrderByDescending(t => t.LastSeenAt).FirstOrDefault()?.Battery;
            var rssi = (best?.Rssi) ?? newest?.Rssi;
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { group.Key, group.First().AssetKey };
            if (reg?.AssetTag is not null) keys.Add(reg.AssetTag);
            var assetAlerts = openAlerts.Count(a => a.AssetId is not null && keys.Contains(a.AssetId));
            var connectivity = Connectivity(lastSeen, now);
            var deviceHealth = connectivity == "Offline" ? "Critical" : battery is < 20 ? "Attention" : connectivity == "Idle" ? "Attention" : "Healthy";

            assets.Add(new TrackedAssetDto(
                reg?.RegistrationId ?? group.Key, reg?.AssetTag, reg?.Name ?? group.Key, reg?.AssetType, reg?.Category,
                reg?.Status, reg?.Condition, plot?.SiteName ?? reg?.SiteName,
                string.Join(", ", tagDtos.Select(t => t.Technology).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct()),
                tagDtos, connectivity, lastSeen, location, location?.At, lastSeen, battery, rssi, assetAlerts,
                reg?.Condition, deviceHealth, plot?.SiteId ?? reg?.SiteId));
        }

        if (!string.IsNullOrWhiteSpace(siteId))
        {
            assets = assets.Where(a => a.SiteId == siteId || a.Location?.SiteId == siteId).ToList();
        }

        // ---- gateways (registered IoT devices and any gateway that reports)
        var gatewayRows = gateways.Values.Select(g =>
        {
            gatewayLastSeen.TryGetValue(g.GatewayKey, out var seen);
            plots.TryGetValue(g.GatewayKey, out var plot);
            var heard = latest.Values.Count(l => l.GatewayId == g.GatewayKey && l.At >= now - IdleAfter);
            return new GatewayDto(g.DeviceId, g.Name, g.Type, g.Model, Connectivity(seen, now), seen, heard,
                plot?.ZoneName, plot?.FloorName, plot?.FloorPlanId, plot is not null, plot?.SiteId ?? g.SiteId);
        }).ToList();
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            gatewayRows = gatewayRows.Where(g => g.SiteId == siteId).ToList();
        }

        // ---- KPIs
        var tagsOnline = assets.SelectMany(a => a.Tags).Count(t => t.Connectivity == "Online");
        var tagCount = assets.Sum(a => a.Tags.Count);
        var (messages, points) = await CountTelemetryAsync(from, now, ct);
        var (prevMessages, prevPoints) = await CountTelemetryAsync(from - span, from, ct);
        var activeAlerts = string.IsNullOrWhiteSpace(siteId) ? openAlerts : openAlerts.Where(a => a.SiteId == siteId).ToList();
        var kpis = new TelemetryKpis(
            assets.Count,
            new DeviceKpi(gatewayRows.Count + tagCount, gatewayRows.Count(g => g.Connectivity == "Online") + tagsOnline,
                gatewayRows.Count, gatewayRows.Count(g => g.Connectivity == "Online"), tagCount, tagsOnline),
            new AlertKpi(activeAlerts.Count, activeAlerts.Count(a => a.Severity == "Critical"), activeAlerts.Count(a => a.Severity == "Warning"),
                activeAlerts.Count(a => a.Severity is not ("Critical" or "Warning"))),
            new TelemetryCountKpi(messages, points, prevPoints, prevPoints == 0 ? null : Math.Round((points - prevPoints) * 100.0 / prevPoints, 1),
                label, "readings = measured values (RSSI, battery…); messages = gateway reports"));

        // ---- events: alerts in the range + estimated location changes
        var events = new List<TelemetryEventDto>();
        foreach (var a in alerts.Where(a => a.At >= from || a.Status == "Open"))
        {
            events.Add(new TelemetryEventDto(a.AlertId, a.At, CategoryOf(a.EventType), a.EventType, a.AssetId, a.AssetName, a.DeviceId,
                a.Message, a.Severity, a.Status, a.AcknowledgedAt, a.AcknowledgedBy));
        }

        var assetByTag = assets.SelectMany(a => a.Tags.Select(t => (t.TagId, Asset: a))).ToDictionary(x => x.TagId, x => x.Asset, StringComparer.OrdinalIgnoreCase);
        foreach (var move in await LocationChangesAsync(assetByTag.Keys.ToList(), from, span, ct))
        {
            if (!assetByTag.TryGetValue(move.TagId, out var asset)) continue;
            string Where(string gw) => plots.TryGetValue(gw, out var p) && p.ZoneName is not null ? $"{p.ZoneName}{(p.FloorName is null ? "" : " · " + p.FloorName)}" : $"gateway {gw.ToUpperInvariant()}";
            events.Add(new TelemetryEventDto($"LOC-{move.At:yyyyMMddHHmm}-{move.TagId[^4..]}", move.At, "Location",
                move.FromGateway is null ? "Location detected" : "Location changed", asset.AssetId, asset.Name, move.TagId.ToUpperInvariant(),
                move.FromGateway is null ? $"At {Where(move.ToGateway)}" : $"{Where(move.FromGateway)} → {Where(move.ToGateway)}",
                "Info", "Recorded", null, null));
        }

        return new TelemetryOverview(now, new RangeDto(range, label, from, now), kpis, assets.OrderBy(a => a.Name).ToList(),
            gatewayRows, events.OrderByDescending(e => e.At).Take(eventLimit).ToList());
    }

    // ================================================================= events (paged)

    /// <summary>
    /// The "Recent telemetry &amp; location events" table, one page at a time (newest first). Optional filters: asset ids
    /// (the page's asset filters) and category (Telemetry, Location, Connectivity, Device).
    /// </summary>
    public async Task<EventPage> GetEventsAsync(string range, string? siteId, int page, int pageSize, IReadOnlyCollection<string> assetIds, string? category, CancellationToken ct)
    {
        var overview = await GetOverviewAsync(range, siteId, ct, eventLimit: int.MaxValue);
        IEnumerable<TelemetryEventDto> rows = overview.Events;
        if (assetIds.Count > 0)
        {
            var ids = new HashSet<string>(assetIds, StringComparer.OrdinalIgnoreCase);
            rows = rows.Where(e => e.AssetId is not null && ids.Contains(e.AssetId));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            rows = rows.Where(e => string.Equals(e.Category, category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var list = rows.ToList();
        var size = Math.Clamp(pageSize, 1, 100);
        var pages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)size));
        var current = Math.Clamp(page, 1, pages);
        return new EventPage(list.Skip((current - 1) * size).Take(size).ToList(), list.Count, current, size, pages, overview.Range);
    }

    // ================================================================= trend

    public async Task<AssetTrend?> GetTrendAsync(string assetId, string range, CancellationToken ct)
    {
        var (span, step, label) = Ranges.TryGetValue(range, out var r) ? r : Ranges["24h"];
        var now = DateTime.UtcNow;
        var from = now - span;
        var tags = await LinkedTagsAsync(ct);
        var registrations = await RegistrationsAsync(tags.Select(t => t.AssetKey), ct);
        var mine = tags.Where(t =>
        {
            var reg = Resolve(registrations, t.AssetKey);
            return string.Equals(reg?.RegistrationId ?? t.AssetKey, assetId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.AssetKey, assetId, StringComparison.OrdinalIgnoreCase);
        }).Select(t => t.TagId).ToList();
        if (mine.Count == 0) return null;

        var metrics = new List<MetricSeries>();
        foreach (var (field, key, name, unit) in new[] { ("rssi", "rssi", "RSSI", "dBm"), ("battery", "battery", "Battery", "%") })
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    { "timestamp", new BsonDocument { { "$gte", from }, { "$lte", now } } },
                    { "meta.tagid", new BsonDocument("$in", new BsonArray(mine)) },
                    { field, new BsonDocument("$type", "number") }
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", new BsonDocument("$dateTrunc", new BsonDocument { { "date", "$timestamp" }, { "unit", "minute" }, { "binSize", step } }) },
                    { "avg", new BsonDocument("$avg", "$" + field) },
                    { "min", new BsonDocument("$min", "$" + field) },
                    { "max", new BsonDocument("$max", "$" + field) },
                    { "n", new BsonDocument("$sum", 1) }
                }),
                new BsonDocument("$sort", new BsonDocument("_id", 1))
            };
            var rows = await C("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
            if (rows.Count == 0) continue;

            var latestDoc = await C("NetworkTimeSeries")
                .Find(new BsonDocument { { "meta.tagid", new BsonDocument("$in", new BsonArray(mine)) }, { field, new BsonDocument("$type", "number") } })
                .Sort(new BsonDocument("timestamp", -1)).Limit(1).FirstOrDefaultAsync(ct);
            var latestAt = latestDoc?["timestamp"].ToUniversalTime();
            var latestValue = latestDoc is null ? (double?)null : latestDoc[field].ToDouble();
            var status = latestAt is null || now - latestAt > StaleAfter ? "Stale"
                : key == "battery" && latestValue < 20 ? "Warning"
                : key == "rssi" && latestValue < -90 ? "Warning" : "Normal";
            metrics.Add(new MetricSeries(key, name, unit, step,
                rows.Select(x => new TrendPoint(x["_id"].ToUniversalTime(), Math.Round(x["avg"].ToDouble(), 1), x["min"].ToDouble(), x["max"].ToDouble(), x["n"].ToInt32())).ToList(),
                latestValue, latestAt, latestDoc?["meta"]["tagid"].AsString.ToUpperInvariant(), status,
                key == "battery" ? 20 : key == "rssi" ? -90 : null,
                key == "battery" ? "Low battery below 20%" : key == "rssi" ? "Weak signal below −90 dBm" : null));
        }

        return new AssetTrend(assetId, new RangeDto(range, label, from, now), metrics);
    }

    // ================================================================= sources

    private sealed record TagLink(string TagId, string AssetKey, string? TagType, string? Technology);
    private sealed record Registration(string RegistrationId, string? AssetTag, string Name, string? AssetType, string? Category, string? Status, string? Condition, string? SiteId, string? SiteName);
    private sealed record Gateway(string GatewayKey, string DeviceId, string? Name, string? Type, string? Model, string? SiteId);
    private sealed record Plot(string? ZoneId, string? ZoneName, string? FloorName, string? BuildingName, string? SiteId, string? SiteName, string? FloorPlanId, string? LocationPath);
    private sealed record Reading(string GatewayId, DateTime At, double? Rssi, double? Battery);
    private sealed record AlertRow(string AlertId, DateTime At, string EventType, string? AssetId, string? AssetName, string? DeviceId, string? Message, string Severity, string Status, string? SiteId, DateTime? AcknowledgedAt, string? AcknowledgedBy);
    /// <summary>An estimated location change of a tag: the strongest gateway changed (confirmed over two slots).</summary>
    public sealed record Move(string TagId, DateTime At, string? FromGateway, string ToGateway);

    private async Task<List<TagLink>> LinkedTagsAsync(CancellationToken ct)
    {
        var rows = await C("TrackingTag").Find(new BsonDocument { { "is_deleted", new BsonDocument("$ne", true) }, { "asset_registration_id", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) } }).ToListAsync(ct);
        return rows.Where(r => Str(r, "tag_id") is not null)
            .Select(r => new TagLink(Str(r, "tag_id")!.ToLowerInvariant(), Str(r, "asset_registration_id")!.Trim(), Str(r, "tag_type"), Str(r, "technology") ?? "BLE"))
            .ToList();
    }

    private async Task<List<Registration>> RegistrationsAsync(IEnumerable<string> keys, CancellationToken ct)
    {
        var list = keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0) return [];
        var regex = new BsonArray(list.Select(k => new BsonRegularExpression($"^{Regex.Escape(k)}$", "i")));
        var rows = await C("AssetRegistration").Find(new BsonDocument
        {
            { "is_deleted", new BsonDocument("$ne", true) },
            { "$or", new BsonArray { new BsonDocument("asset_registration_id", new BsonDocument("$in", regex)), new BsonDocument("basic_information.asset_tag", new BsonDocument("$in", regex)) } }
        }).ToListAsync(ct);
        return rows.Select(r => new Registration(
            Str(r, "asset_registration_id") ?? string.Empty, Str(r, "basic_information", "asset_tag"),
            Str(r, "basic_information", "asset_name") ?? Str(r, "asset_registration_id") ?? "Asset",
            Str(r, "classification", "asset_type"), Str(r, "classification", "category"),
            Str(r, "basic_information", "status"), Str(r, "basic_information", "condition"),
            Str(r, "location_assignment", "location", "site_id"), Str(r, "location_assignment", "location", "site_name"))).ToList();
    }

    private static Registration? Resolve(List<Registration> regs, string key) =>
        regs.FirstOrDefault(r => string.Equals(r.RegistrationId, key, StringComparison.OrdinalIgnoreCase) || string.Equals(r.AssetTag, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Registered IoT devices, keyed by lower-case device id (the gateway id in the readings).</summary>
    private async Task<Dictionary<string, Gateway>> GatewaysAsync(CancellationToken ct)
    {
        var rows = await C("IoTDevice").Find(new BsonDocument("is_deleted", new BsonDocument("$ne", true))).ToListAsync(ct);
        return rows.Where(r => Str(r, "device_id") is not null)
            .Select(r => new Gateway(Str(r, "device_id")!.ToLowerInvariant(), Str(r, "device_id")!, Str(r, "device_name"), Str(r, "device_type"), Str(r, "model"), Str(r, "site_id")))
            .GroupBy(g => g.GatewayKey).ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>Where each gateway (device) is plotted on a floor plan, keyed by lower-case device id.</summary>
    private async Task<Dictionary<string, Plot>> PlottedGatewaysAsync(CancellationToken ct)
    {
        var rows = await C("maps_plots").Find(new BsonDocument { { "kind", "device" }, { "is_deleted", new BsonDocument("$ne", true) } }).ToListAsync(ct);
        var result = new Dictionary<string, Plot>();
        foreach (var r in rows)
        {
            var id = Str(r, "item_id")?.ToLowerInvariant();
            if (id is null || result.ContainsKey(id)) continue;
            result[id] = new Plot(Str(r, "zone_id"), Str(r, "zone_name") ?? Str(r, "location", "zone_name"), Str(r, "location", "floor_name"),
                Str(r, "location", "building_name"), Str(r, "location", "site_id"), Str(r, "location", "site_name"), Str(r, "floor_plan_id"), Str(r, "location", "location_path"));
        }

        return result;
    }

    private async Task<Dictionary<string, Reading>> LatestPerTagAsync(DateTime since, CancellationToken ct)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument { { "timestamp", new BsonDocument("$gte", since) }, { "meta.tagid", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) } }),
            new BsonDocument("$sort", new BsonDocument("timestamp", -1)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$toLower", "$meta.tagid") },
                { "gw", new BsonDocument("$first", new BsonDocument("$toLower", "$meta.gatewayid")) },
                { "at", new BsonDocument("$first", "$timestamp") },
                { "rssi", new BsonDocument("$first", "$rssi") }
            })
        };
        var rows = await C("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, new AggregateOptions { AllowDiskUse = true }, ct).ToListAsync(ct);

        // battery: the newest reading that carries one (not every message reports battery)
        var batteryPipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument { { "timestamp", new BsonDocument("$gte", since) }, { "battery", new BsonDocument("$type", "number") } }),
            new BsonDocument("$sort", new BsonDocument("timestamp", -1)),
            new BsonDocument("$group", new BsonDocument { { "_id", new BsonDocument("$toLower", "$meta.tagid") }, { "battery", new BsonDocument("$first", "$battery") } })
        };
        var batteries = (await C("NetworkTimeSeries").Aggregate<BsonDocument>(batteryPipeline, new AggregateOptions { AllowDiskUse = true }, ct).ToListAsync(ct))
            .Where(b => b["_id"].IsString).ToDictionary(b => b["_id"].AsString, b => Num(b, "battery"));

        return rows.Where(r => r["_id"].IsString).ToDictionary(r => r["_id"].AsString, r => new Reading(r["gw"].IsString ? r["gw"].AsString : string.Empty, r["at"].ToUniversalTime(), Num(r, "rssi"),
            batteries.TryGetValue(r["_id"].AsString, out var b) ? b : null));
    }

    private async Task<Dictionary<string, Reading>> StrongestGatewayAsync(DateTime since, CancellationToken ct)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument { { "timestamp", new BsonDocument("$gte", since) }, { "rssi", new BsonDocument("$type", "number") } }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument { { "tag", new BsonDocument("$toLower", "$meta.tagid") }, { "gw", new BsonDocument("$toLower", "$meta.gatewayid") } } },
                { "rssi", new BsonDocument("$avg", "$rssi") },
                { "at", new BsonDocument("$max", "$timestamp") }
            })
        };
        var rows = await C("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return rows.GroupBy(r => r["_id"]["tag"].AsString)
            .ToDictionary(g => g.Key, g =>
            {
                var best = g.OrderByDescending(x => x["rssi"].ToDouble()).First();
                return new Reading(best["_id"]["gw"].AsString, best["at"].ToUniversalTime(), Math.Round(best["rssi"].ToDouble(), 1), null);
            });
    }

    private async Task<Dictionary<string, DateTime>> LatestPerGatewayAsync(DateTime since, CancellationToken ct)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("timestamp", new BsonDocument("$gte", since))),
            new BsonDocument("$group", new BsonDocument { { "_id", new BsonDocument("$toLower", "$meta.gatewayid") }, { "at", new BsonDocument("$max", "$timestamp") } })
        };
        var rows = await C("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return rows.Where(r => r["_id"].IsString).ToDictionary(r => r["_id"].AsString, r => r["at"].ToUniversalTime());
    }

    /// <summary>Gateway reports (messages) and measured values (points: RSSI + battery) in a window.</summary>
    private async Task<(long Messages, long Points)> CountTelemetryAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("timestamp", new BsonDocument { { "$gte", from }, { "$lt", to } })),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "messages", new BsonDocument("$sum", 1) },
                { "points", new BsonDocument("$sum", new BsonDocument("$add", new BsonArray
                    {
                        new BsonDocument("$cond", new BsonArray { new BsonDocument("$isNumber", "$rssi"), 1, 0 }),
                        new BsonDocument("$cond", new BsonArray { new BsonDocument("$isNumber", "$battery"), 1, 0 })
                    })) }
            })
        };
        var row = await C("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, cancellationToken: ct).FirstOrDefaultAsync(ct);
        return row is null ? (0, 0) : (row["messages"].ToInt64(), row["points"].ToInt64());
    }

    private async Task<List<AlertRow>> AlertsAsync(DateTime from, CancellationToken ct)
    {
        var filter = new BsonDocument
        {
            { "is_deleted", new BsonDocument("$ne", true) },
            { "$or", new BsonArray { new BsonDocument("raised_at", new BsonDocument("$gte", from)), new BsonDocument("status", "Open") } }
        };
        var rows = await C("IoTAlert").Find(filter).Sort(new BsonDocument("raised_at", -1)).Limit(500).ToListAsync(ct);
        return rows.Select(r => new AlertRow(Str(r, "alert_id") ?? string.Empty, r.GetValue("raised_at", BsonNull.Value).IsBsonDateTime ? r["raised_at"].ToUniversalTime() : DateTime.MinValue,
            Str(r, "event_type") ?? "Alert", Str(r, "asset_registration_id"), Str(r, "asset_name"), Str(r, "device_id"), Str(r, "message"),
            Str(r, "severity") ?? "Info", Str(r, "status") ?? "Open", Str(r, "site_id"),
            r.GetValue("acknowledged_at", BsonNull.Value).IsBsonDateTime ? r["acknowledged_at"].ToUniversalTime() : null, Str(r, "acknowledged_by"))).ToList();
    }

    /// <summary>
    /// Estimated location changes of the given tags: the strongest gateway per 2-minute slot; a change is recorded
    /// when a different gateway is strongest for two slots in a row (ignores one-off flickers between gateways).
    /// </summary>
    public async Task<List<Move>> LocationChangesAsync(List<string> tagIds, DateTime from, TimeSpan span, CancellationToken ct)
    {
        if (tagIds.Count == 0) return [];
        var slot = span <= TimeSpan.FromHours(1) ? 1 : span <= TimeSpan.FromDays(1) ? 2 : 10;
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "timestamp", new BsonDocument("$gte", from) },
                { "meta.tagid", new BsonDocument("$in", new BsonArray(tagIds)) },
                { "rssi", new BsonDocument("$type", "number") }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "tag", new BsonDocument("$toLower", "$meta.tagid") },
                        { "gw", new BsonDocument("$toLower", "$meta.gatewayid") },
                        { "slot", new BsonDocument("$dateTrunc", new BsonDocument { { "date", "$timestamp" }, { "unit", "minute" }, { "binSize", slot } }) }
                    } },
                { "rssi", new BsonDocument("$avg", "$rssi") }
            })
        };
        var rows = await C("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, new AggregateOptions { AllowDiskUse = true }, ct).ToListAsync(ct);
        var moves = new List<Move>();
        foreach (var tag in rows.GroupBy(r => r["_id"]["tag"].AsString))
        {
            var slots = tag.GroupBy(r => r["_id"]["slot"].ToUniversalTime()).OrderBy(g => g.Key)
                .Select(g => (At: g.Key, Gw: g.OrderByDescending(x => x["rssi"].ToDouble()).First()["_id"]["gw"].AsString)).ToList();
            string? current = null;
            for (var i = 0; i < slots.Count; i++)
            {
                var gw = slots[i].Gw;
                if (gw == current) continue;
                var confirmed = i + 1 < slots.Count ? slots[i + 1].Gw == gw : i == 0;
                if (!confirmed && current is not null) continue;
                moves.Add(new Move(tag.Key, slots[i].At, current, gw));
                current = gw;
            }
        }

        return moves;
    }

    // ================================================================= helpers

    public static string Connectivity(DateTime? lastSeen, DateTime now) =>
        lastSeen is null ? "Offline" : now - lastSeen <= OnlineWithin ? "Online" : now - lastSeen <= IdleAfter ? "Idle" : "Offline";

    private static string CategoryOf(string eventType)
    {
        var t = eventType.ToLowerInvariant();
        return t.Contains("geofence") || t.Contains("dwell") || t.Contains("zone") || t.Contains("location") ? "Location"
            : t.Contains("offline") || t.Contains("signal") || t.Contains("connect") ? "Connectivity"
            : t.Contains("battery") || t.Contains("firmware") || t.Contains("tamper") || t.Contains("device") ? "Device"
            : "Telemetry";
    }

    private static string? Str(BsonDocument doc, params string[] path)
    {
        BsonValue v = doc;
        foreach (var p in path)
        {
            if (!v.IsBsonDocument || !v.AsBsonDocument.TryGetValue(p, out v)) return null;
        }

        return v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString : null;
    }

    private static double? Num(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var v) && v.IsNumeric ? Math.Round(v.ToDouble(), 1) : null;
}

// ================================================================= contracts

public sealed record RangeDto(string Key, string Label, DateTime From, DateTime To);

public sealed record TelemetryOverview(DateTime GeneratedAt, RangeDto Range, TelemetryKpis Kpis, List<TrackedAssetDto> Assets, List<GatewayDto> Gateways, List<TelemetryEventDto> Events);

/// <param name="TrackedAssets">Unique assets with at least one linked tag (not tags).</param>
public sealed record TelemetryKpis(int TrackedAssets, DeviceKpi Devices, AlertKpi Alerts, TelemetryCountKpi Telemetry);

/// <param name="Registered">Gateways / IoT devices plus tags; assets and devices are not one-to-one.</param>
public sealed record DeviceKpi(int Registered, int Online, int Gateways, int GatewaysOnline, int Tags, int TagsOnline);

public sealed record AlertKpi(int Active, int Critical, int Warning, int Info);

/// <param name="Points">Measured values (each RSSI or battery value is one point).</param>
/// <param name="Messages">Gateway reports; one message can carry several points.</param>
public sealed record TelemetryCountKpi(long Messages, long Points, long PreviousPoints, double? ChangePercent, string Period, string Definition);

public sealed record AssetTagDto(string TagId, string? TagType, string? Technology, DateTime? LastSeenAt, double? Battery, double? Rssi, string? GatewayId, string Connectivity);

/// <param name="Method">Estimated (BLE gateway zone) or Measured (GPS); BLE readings carry no coordinates.</param>
public sealed record AssetLocationDto(
    string GatewayId, string? GatewayName, string? ZoneId, string? ZoneName, string? FloorName, string? BuildingName,
    string? SiteId, string? SiteName, string? FloorPlanId, string? LocationPath, string Method, string Basis, string Accuracy, DateTime? At);

/// <param name="AssetHealth">Condition of the asset itself (Asset Registration), separate from the device health.</param>
/// <param name="DeviceHealth">Health of the tracking device(s): connectivity and battery.</param>
public sealed record TrackedAssetDto(
    string AssetId, string? AssetTag, string Name, string? AssetType, string? Category, string? OperationalStatus, string? Condition, string? SiteName,
    string Technology, List<AssetTagDto> Tags, string Connectivity, DateTime? LastSeenAt, AssetLocationDto? Location,
    DateTime? LastLocationAt, DateTime? LastTelemetryAt, double? Battery, double? Rssi, int ActiveAlerts,
    string? AssetHealth, string DeviceHealth, string? SiteId);

public sealed record GatewayDto(string DeviceId, string? Name, string? Type, string? Model, string Connectivity, DateTime? LastSeenAt, int TagsHeard,
    string? ZoneName, string? FloorName, string? FloorPlanId, bool Plotted, string? SiteId);

/// <param name="Category">Telemetry, Location, Connectivity or Device.</param>
public sealed record TelemetryEventDto(string EventId, DateTime At, string Category, string Event, string? AssetId, string? AssetName, string? SourceDevice,
    string? Reading, string Severity, string Status, DateTime? AcknowledgedAt, string? AcknowledgedBy);

/// <summary>One page of the events table.</summary>
public sealed record EventPage(List<TelemetryEventDto> Items, int Total, int Page, int PageSize, int TotalPages, RangeDto Range);

public sealed record AssetTrend(string AssetId, RangeDto Range, List<MetricSeries> Metrics);

public sealed record TrendPoint(DateTime At, double Avg, double Min, double Max, int Count);

/// <param name="Status">Normal, Warning or Stale (no reading for 10 minutes).</param>
public sealed record MetricSeries(string Key, string Label, string Unit, int StepMinutes, List<TrendPoint> Points,
    double? LatestValue, DateTime? LatestAt, string? Source, string Status, double? Threshold, string? ThresholdLabel);
