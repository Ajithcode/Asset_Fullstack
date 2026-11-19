using System.Text.RegularExpressions;
using A360.Tracking.Api.Telemetry;
using A360.Tracking.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Alerts;

/// <summary>
/// Geofence &amp; Alerts page data: KPIs, one list of indoor (floor plan zones, Maps &amp; Floor Plans) and outdoor
/// (Geofence) monitored areas with their live figures, and the paged alert event history.
/// Monitored assets are unique tagged assets in the scope of an active zone / rule (one asset in many zones counts once).
/// </summary>
public sealed class GeofenceAlertsService(IMongoDatabase database, MapZoneLookup zoneLookup, RealTimeLocationService rtl, ILogger<GeofenceAlertsService> logger)
{
    /// <summary>Event types that belong to geofences even when no rule id is set.</summary>
    private static readonly BsonRegularExpression GeofenceEventType = new("geofence|zone|dwell|entry|exit|unauthori|location lost", "i");

    private IMongoCollection<AlertRule> Rules => database.GetCollection<AlertRule>("AlertRule");
    private IMongoCollection<Geofence> Geofences => database.GetCollection<Geofence>("Geofence");
    private IMongoCollection<IoTAlert> Alerts => database.GetCollection<IoTAlert>("IoTAlert");

    public async Task<GeofenceSummary> GetSummaryAsync(DateTime? todayFromUtc, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var todayFrom = todayFromUtc?.ToUniversalTime() ?? now.Date;
        var rules = await Rules.Find(r => !r.IsDeleted).ToListAsync(ct);
        var geofences = await Geofences.Find(g => !g.IsDeleted).ToListAsync(ct);
        var zones = await database.GetCollection<BsonDocument>("maps_zones").Find(Builders<BsonDocument>.Filter.Ne("is_deleted", true)).ToListAsync(ct);
        var zoneIds = zones.Select(z => S(z, "zone_id")).OfType<string>().ToList();
        var gateways = await zoneLookup.GatewaysByZoneAsync(zoneIds, ct);
        var assets = await TaggedAssetsAsync(ct);

        // Assets located in each floor plan zone now (same engine as Real-Time Location)
        var insideNow = new Dictionary<string, int>();
        try
        {
            var live = await rtl.GetOverviewAsync(null, null, ct);
            foreach (var a in live.Assets.Where(a => a.Freshness == "Live" && a.Location?.ZoneId is not null))
            {
                insideNow[a.Location!.ZoneId!] = insideNow.GetValueOrDefault(a.Location.ZoneId!) + 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Live zone occupancy unavailable");
        }

        var ruleById = rules.ToDictionary(r => r.Id);
        var alerts = await Alerts.Find(BaseFilter(rules.Select(r => r.Id)))
            .Project<IoTAlert>(Builders<IoTAlert>.Projection
                .Include(a => a.AlertId).Include(a => a.RaisedAt).Include(a => a.Status).Include(a => a.AlertRuleId).Include(a => a.GeofenceId)
                .Include(a => a.EventType).Include(a => a.AssetName).Include(a => a.AssetRegistrationId).Include(a => a.EscalatedAt))
            .ToListAsync(ct);
        string? TargetOf(IoTAlert a) => a.GeofenceId ?? (a.AlertRuleId is not null && ruleById.TryGetValue(a.AlertRuleId, out var r) ? r.GeofenceId : null);
        var alertsByTarget = alerts.Where(a => TargetOf(a) is not null).GroupBy(a => TargetOf(a)!).ToDictionary(g => g.Key, g => g.ToList());

        HashSet<string> Scope(IEnumerable<AlertRule> active, IEnumerable<string>? zoneGroups = null)
        {
            var keys = new HashSet<string>();
            foreach (var a in assets)
            {
                if (active.Any(r => AlertScope.InGroup(r.AssetGroup, a.Groups) && AlertScope.InAssets(r, a.Key, a.RegistrationId, a.Tag, a.Name))
                    || (zoneGroups?.Any(g => AlertScope.InGroup(g, a.Groups)) ?? false))
                {
                    keys.Add(a.Key);
                }
            }

            return keys;
        }

        var monitoredAll = new HashSet<string>();
        var targets = new List<GeofenceTarget>();

        foreach (var g in geofences)
        {
            var on = rules.Where(r => r.GeofenceId == g.Id).ToList();
            var activeRules = on.Where(r => r.Status == AlertRuleStatus.Active).ToList();
            var active = g.Status == GeofenceStatus.Active;
            var scope = active ? Scope(activeRules, g.AssetGroups) : [];
            monitoredAll.UnionWith(scope);
            targets.Add(Target(g.Id, "Outdoor", g.Name, null, g.GeofenceType, g.Category, g.SiteId, g.SiteName, null, null, null, null,
                g.Shape, null, g.Status ?? GeofenceStatus.Active, on.Count, activeRules.Count, scope.Count, null, null, g.CreatedBy, g.UpdatedAt ?? g.CreatedAt,
                alertsByTarget.GetValueOrDefault(g.Id)));
        }

        foreach (var z in zones)
        {
            var zoneId = S(z, "zone_id");
            if (zoneId is null)
            {
                continue;
            }

            var id = AlertNotifyDefaults.MapZonePrefix + zoneId;
            var on = rules.Where(r => r.MapZoneId == zoneId || r.GeofenceId == id).ToList();
            var activeRules = on.Where(r => r.Status == AlertRuleStatus.Active).ToList();
            var scope = Scope(activeRules);
            monitoredAll.UnionWith(scope);
            var location = z.TryGetValue("location", out var l) && l.IsBsonDocument ? l.AsBsonDocument : new BsonDocument();
            var area = z.TryGetValue("area_m2", out var am) && am.IsNumeric ? am.ToDouble() : (double?)null;
            targets.Add(Target(id, "Indoor", S(z, "name") ?? S(z, "code") ?? "Zone", S(z, "code"), S(z, "access_rule"), S(z, "zone_type"),
                S(location, "site_id"), S(location, "site_name"), S(location, "building_name"), S(location, "floor_name"), S(z, "floor_plan_id"), S(location, "location_path"),
                S(z, "shape"), area, activeRules.Count > 0 ? "Monitored" : "Not monitored", on.Count, activeRules.Count, scope.Count,
                gateways.GetValueOrDefault(zoneId)?.Count ?? 0, insideNow.GetValueOrDefault(zoneId), S(z, "created_by"),
                z.TryGetValue("updated_at", out var u) && u.IsValidDateTime ? u.ToUniversalTime() : null, alertsByTarget.GetValueOrDefault(id)));
        }

        var unresolved = IoTAlertStatus.Unresolved.ToHashSet();
        var kpis = new GeofenceKpis(
            ActiveGeofences: targets.Count(t => t.Status is GeofenceStatus.Active or "Monitored"),
            OutdoorActive: targets.Count(t => t.Kind == "Outdoor" && t.Status == GeofenceStatus.Active),
            IndoorMonitored: targets.Count(t => t.Kind == "Indoor" && t.Status == "Monitored"),
            MonitoredAssets: monitoredAll.Count,
            TaggedAssets: assets.Count,
            AlertsToday: alerts.Count(a => a.RaisedAt >= todayFrom),
            AlertsYesterday: alerts.Count(a => a.RaisedAt >= todayFrom.AddDays(-1) && a.RaisedAt < todayFrom),
            OpenAlerts: alerts.Count(a => unresolved.Contains(a.Status ?? IoTAlertStatus.Open)),
            Unacknowledged: alerts.Count(a => (a.Status ?? IoTAlertStatus.Open) == IoTAlertStatus.Open),
            Escalated: alerts.Count(a => a.EscalatedAt is not null && unresolved.Contains(a.Status ?? IoTAlertStatus.Open)));

        return new GeofenceSummary(now, kpis, targets.OrderBy(t => t.Kind).ThenBy(t => t.Name).ToList());

        static GeofenceTarget Target(string id, string kind, string name, string? code, string? type, string? category, string? siteId, string? siteName,
            string? building, string? floor, string? floorPlanId, string? path, string? shape, double? areaM2, string status, int rules, int activeRules,
            int monitored, int? gatewayCount, int? inside, string? createdBy, DateTime? modifiedAt, List<IoTAlert>? zoneAlerts)
        {
            var list = zoneAlerts ?? [];
            var open = list.Where(a => IoTAlertStatus.Unresolved.Contains(a.Status ?? IoTAlertStatus.Open)).ToList();
            var newest = list.Where(a => (a.Status ?? IoTAlertStatus.Open) == IoTAlertStatus.Open).MaxBy(a => a.RaisedAt);
            return new GeofenceTarget(id, kind, name, code, type, category, siteId, siteName, building, floor, floorPlanId, path, shape, areaM2, status,
                rules, activeRules, monitored, gatewayCount, inside, open.Count, list.Count == 0 ? null : list.Max(a => a.RaisedAt), createdBy, modifiedAt,
                newest is null ? null : new OpenAlertRef(newest.AlertId, newest.EventType, newest.AssetName, newest.RaisedAt));
        }
    }

    public async Task<GeofenceEventPage> GetEventsAsync(GeofenceEventQuery q, CancellationToken ct)
    {
        var rules = await Rules.Find(r => !r.IsDeleted).ToListAsync(ct);
        var ruleById = rules.ToDictionary(r => r.Id);
        var baseFilter = BaseFilter(rules.Select(r => r.Id));
        var parts = new List<FilterDefinition<IoTAlert>> { baseFilter };
        var f = Builders<IoTAlert>.Filter;

        if (q.From is not null) parts.Add(f.Gte(a => a.RaisedAt, q.From.Value.ToUniversalTime()));
        if (q.To is not null) parts.Add(f.Lt(a => a.RaisedAt, q.To.Value.ToUniversalTime()));
        if (!string.IsNullOrWhiteSpace(q.TargetId))
        {
            var target = q.TargetId.Trim();
            var targetRules = rules.Where(r => r.GeofenceId == target).Select(r => r.Id).ToList();
            parts.Add(f.Or(f.Eq(a => a.GeofenceId, target), f.In(a => a.AlertRuleId, targetRules)));
        }

        if (!string.IsNullOrWhiteSpace(q.EventType)) parts.Add(f.Eq(a => a.EventType, q.EventType.Trim()));
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            var statuses = q.Status.Trim() == "Unresolved" ? IoTAlertStatus.Unresolved.ToList() : [q.Status.Trim()];
            parts.Add(statuses.Contains(IoTAlertStatus.Open) ? f.Or(f.In(a => a.Status, statuses), f.Eq(a => a.Status, null)) : f.In(a => a.Status, statuses));
        }

        if (!string.IsNullOrWhiteSpace(q.Severity))
        {
            var sev = q.Severity.Trim();
            var sevRules = rules.Where(r => r.Severity == sev).Select(r => r.Id).ToList();
            parts.Add(f.Or(f.Eq(a => a.RuleSeverity, sev), f.And(f.Eq(a => a.RuleSeverity, null), f.In(a => a.AlertRuleId, sevRules))));
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var rx = new BsonRegularExpression(Regex.Escape(q.Search.Trim()), "i");
            parts.Add(f.Or(f.Regex(a => a.AlertId, rx), f.Regex(a => a.AssetName, rx), f.Regex(a => a.AssetRegistrationId, rx),
                f.Regex(a => a.Message, rx), f.Regex(a => a.GeofenceName, rx), f.Regex(a => a.AssignedTo, rx)));
        }

        var filter = f.And(parts);
        var pageSize = Math.Clamp(q.PageSize ?? 10, 1, 200);
        var total = (int)await Alerts.CountDocumentsAsync(filter, cancellationToken: ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Clamp(q.Page ?? 1, 1, totalPages);
        var rows = await Alerts.Find(filter).SortByDescending(a => a.RaisedAt).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        var eventTypes = (await Alerts.Distinct(a => a.EventType, baseFilter, cancellationToken: ct).ToListAsync(ct)).Where(t => !string.IsNullOrWhiteSpace(t)).OrderBy(t => t).ToList();

        var items = rows.Select(a =>
        {
            var rule = a.AlertRuleId is not null ? ruleById.GetValueOrDefault(a.AlertRuleId) : null;
            var status = a.Status ?? IoTAlertStatus.Open;
            return new GeofenceEventDto(
                a.AlertId, a.RaisedAt, a.AssetRegistrationId, a.AssetName, a.DeviceId, a.SiteName,
                a.GeofenceId ?? rule?.GeofenceId, a.GeofenceName ?? rule?.GeofenceName, a.EventType,
                a.RuleSeverity ?? rule?.Severity ?? a.Severity, a.Severity, status, a.Message, a.AlertRuleId, rule?.Name,
                a.DurationSeconds, a.AssignedTo, a.AssignedTo ?? a.ResolvedBy ?? a.AcknowledgedBy,
                a.AcknowledgedBy, a.AcknowledgedAt, a.ResolvedBy, a.ResolvedAt, a.EscalatedAt, a.EscalatedTo,
                (a.History ?? []).Select(h => new AlertHistoryDto(h.At, h.By, h.Action, h.Note)).ToList());
        }).ToList();

        return new GeofenceEventPage(items, total, page, pageSize, totalPages, eventTypes);
    }

    /// <summary>Alerts raised by a geofence rule, linked to a geofence, or with a geofence event type.</summary>
    private static FilterDefinition<IoTAlert> BaseFilter(IEnumerable<string> ruleIds)
    {
        var f = Builders<IoTAlert>.Filter;
        return f.And(
            f.Ne(a => a.IsDeleted, true),
            f.Or(f.In(a => a.AlertRuleId, ruleIds.ToList()), f.Ne(a => a.GeofenceId, null), f.Regex(a => a.EventType, GeofenceEventType)));
    }

    private sealed record TaggedAsset(string Key, string Name, string? RegistrationId, string? Tag, List<string> Groups);

    /// <summary>Assets with a tag in Tag Management (the ones a zone can detect), with their classification.</summary>
    private async Task<List<TaggedAsset>> TaggedAssetsAsync(CancellationToken ct)
    {
        var links = await database.GetCollection<BsonDocument>("TrackingTag")
            .Find(Builders<BsonDocument>.Filter.Ne("is_deleted", true) & Builders<BsonDocument>.Filter.Type("asset_registration_id", BsonType.String))
            .Project(Builders<BsonDocument>.Projection.Include("asset_registration_id"))
            .ToListAsync(ct);
        var keys = links.Select(l => l["asset_registration_id"].AsString.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (keys.Count == 0)
        {
            return [];
        }

        var regex = new BsonArray(keys.Select(k => new BsonRegularExpression($"^{Regex.Escape(k)}$", "i")));
        var registrations = await database.GetCollection<BsonDocument>("AssetRegistration")
            .Find(new BsonDocument
            {
                { "is_deleted", new BsonDocument("$ne", true) },
                { "$or", new BsonArray { new BsonDocument("asset_registration_id", new BsonDocument("$in", regex)), new BsonDocument("basic_information.asset_tag", new BsonDocument("$in", regex)) } }
            })
            .ToListAsync(ct);

        var result = new Dictionary<string, TaggedAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var reg = registrations.FirstOrDefault(r =>
                string.Equals(S(r, "asset_registration_id"), key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(S(r, "basic_information", "asset_tag"), key, StringComparison.OrdinalIgnoreCase));
            var regId = reg is null ? null : S(reg, "asset_registration_id");
            var id = regId ?? key;
            if (result.ContainsKey(id))
            {
                continue;
            }

            var groups = reg is null ? [] : new[] { S(reg, "classification", "category"), S(reg, "classification", "sub_category"), S(reg, "classification", "asset_type") }.OfType<string>().ToList();
            result[id] = new TaggedAsset(id, reg is null ? key : S(reg, "basic_information", "asset_name") ?? key, regId, reg is null ? key : S(reg, "basic_information", "asset_tag"), groups);
        }

        return result.Values.ToList();
    }

    private static string? S(BsonDocument doc, params string[] path)
    {
        BsonValue v = doc;
        foreach (var p in path)
        {
            if (!v.IsBsonDocument || !v.AsBsonDocument.TryGetValue(p, out v))
            {
                return null;
            }
        }

        return v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString : null;
    }
}

public sealed record GeofenceSummary(DateTime GeneratedAt, GeofenceKpis Kpis, List<GeofenceTarget> Targets);

/// <param name="ActiveGeofences">Enabled outdoor geofences + floor plan zones with at least one active rule.</param>
/// <param name="MonitoredAssets">Unique tagged assets in scope of an active geofence / rule.</param>
/// <param name="OpenAlerts">Not resolved or closed (New, Acknowledged, Assigned, Investigating).</param>
/// <param name="Unacknowledged">New alerts nobody acknowledged yet.</param>
public sealed record GeofenceKpis(int ActiveGeofences, int OutdoorActive, int IndoorMonitored, int MonitoredAssets, int TaggedAssets,
    int AlertsToday, int AlertsYesterday, int OpenAlerts, int Unacknowledged, int Escalated);

/// <param name="Id">Outdoor geofence id, or "zone:{maps zone id}" for a floor plan zone (the alert rule target).</param>
/// <param name="Kind">Indoor or Outdoor.</param>
/// <param name="Type">Outdoor: Allowed / Warning / Restricted. Indoor: the zone's access rule.</param>
/// <param name="Status">Outdoor: Active / Disabled. Indoor: Monitored (has an active rule) / Not monitored.</param>
/// <param name="InsideNow">Indoor: assets located in the zone now.</param>
public sealed record GeofenceTarget(string Id, string Kind, string Name, string? Code, string? Type, string? Category, string? SiteId, string? SiteName,
    string? BuildingName, string? FloorName, string? FloorPlanId, string? LocationPath, string? Shape, double? AreaM2, string Status,
    int Rules, int ActiveRules, int MonitoredAssets, int? Gateways, int? InsideNow, int ActiveAlerts, DateTime? LastEventAt,
    string? CreatedBy, DateTime? LastModifiedAt, OpenAlertRef? LatestOpenAlert);

public sealed record OpenAlertRef(string AlertId, string EventType, string AssetName, DateTime RaisedAt);

/// <param name="Status">An alert status, or "Unresolved" for every status before Resolved.</param>
public sealed record GeofenceEventQuery(int? Page, int? PageSize, DateTime? From, DateTime? To, string? TargetId, string? EventType, string? Status, string? Severity, string? Search);

public sealed record AlertHistoryDto(DateTime At, string? By, string Action, string? Note);

/// <param name="Severity">The rule's severity (Informational..Critical), else the alert's.</param>
/// <param name="Owner">Assignee, else who resolved / acknowledged it.</param>
public sealed record GeofenceEventDto(string AlertId, DateTime RaisedAt, string? AssetId, string AssetName, string? DeviceId, string? SiteName,
    string? TargetId, string? TargetName, string EventType, string Severity, string AlertSeverity, string Status, string? Message,
    string? RuleId, string? RuleName, int? DurationSeconds, string? AssignedTo, string? Owner, string? AcknowledgedBy, DateTime? AcknowledgedAt,
    string? ResolvedBy, DateTime? ResolvedAt, DateTime? EscalatedAt, string? EscalatedTo, List<AlertHistoryDto> History);

public sealed record GeofenceEventPage(List<GeofenceEventDto> Items, int Total, int Page, int PageSize, int TotalPages, List<string> EventTypes);
