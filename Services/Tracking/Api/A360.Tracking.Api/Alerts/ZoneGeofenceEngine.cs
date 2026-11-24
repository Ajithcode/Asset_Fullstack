using A360.Repository.Sequences;
using A360.Tracking.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Alerts;

/// <summary>
/// Raises geofence alerts for floor plan zones from live tag readings (NetworkTimeSeries, written by NetworkerService).
/// An asset is inside a zone while one of its tags (Tag Management) is heard by a gateway plotted in that zone (Maps
/// &amp; Floor Plans) within the presence window. Every few seconds the engine compares who is inside each watched
/// zone with the last look (ZonePresence) and, per active alert rule on the zone, raises:
/// "Geofence Entry" / "Unauthorized Entry" (enters, after the rule's debounce), "Geofence Exit" (gone from the zone
/// for the debounce), "Location Lost" (gone and heard by no gateway at all) and "Dwell Exceeded" (inside longer than
/// the rule's dwell minutes, once per stay). Detection is separate from alerting: every movement is a location event
/// (Real-Time Location), only rule matches become alerts. The first look at a zone only records who is already inside.
/// New alerts get their desktop notifications scheduled (<see cref="AlertNotifications"/>); unacknowledged alerts are
/// escalated per the rule's escalation policy.
/// </summary>
public sealed class ZoneGeofenceEngine(
    IMongoDatabase database,
    MapZoneLookup zoneLookup,
    AlertNotifications notifications,
    ISequenceGenerator sequenceGenerator,
    IConfiguration configuration,
    ILogger<ZoneGeofenceEngine> logger) : BackgroundService
{
    public const string EventEntry = "Geofence Entry";
    public const string EventUnauthorized = "Unauthorized Entry";
    public const string EventExit = "Geofence Exit";
    public const string EventDwell = "Dwell Exceeded";
    public const string EventLost = "Location Lost";
    private const string EngineUser = "Geofence engine";

    private static readonly string[] EntryTriggers = [AlertRuleTrigger.Enters, AlertRuleTrigger.EntersOrExits, AlertRuleTrigger.UnauthorizedEntry];
    private static readonly string[] ExitTriggers = [AlertRuleTrigger.Exits, AlertRuleTrigger.EntersOrExits];

    private TimeSpan Tick => TimeSpan.FromSeconds(Math.Max(2, configuration.GetValue("Alerts:EngineSeconds", 5)));
    /// <summary>A tag counts as inside while a zone gateway heard it within this window (readings arrive every few seconds).</summary>
    private TimeSpan PresenceWindow => TimeSpan.FromSeconds(Math.Max(15, configuration.GetValue("Alerts:PresenceSeconds", 60)));

    private IMongoCollection<ZonePresenceDocument> Presence => database.GetCollection<ZonePresenceDocument>(ZonePresenceDocument.CollectionName);
    private IMongoCollection<WatchedZoneDocument> Watched => database.GetCollection<WatchedZoneDocument>(WatchedZoneDocument.CollectionName);
    private IMongoCollection<AlertRule> Rules => database.GetCollection<AlertRule>("AlertRule");
    private IMongoCollection<IoTAlert> Alerts => database.GetCollection<IoTAlert>("IoTAlert");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Alerts:EngineEnabled", true))
        {
            logger.LogInformation("Zone geofence engine is disabled (Alerts:EngineEnabled=false)");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                await EscalateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Zone geofence engine pass failed");
            }

            await Task.Delay(Tick, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    /// <summary>Zones already reported as missing, so a deleted zone logs one warning, not one per pass.</summary>
    private readonly HashSet<string> reportedMissingZones = [];

    private static TimeSpan Debounce(AlertRule rule) => TimeSpan.FromSeconds(Math.Max(0, rule.DebounceSeconds));

    /// <summary>
    /// Logs once (Warning) per zone that active rules watch but that no longer exists in maps_zones; such rules are
    /// skipped. A zone that comes back, or is no longer watched, is forgotten so a later deletion is reported again.
    /// </summary>
    private void WarnMissingZones(List<AlertRule> rules, List<string> requestedZoneIds, Dictionary<string, MapZoneRef> zones)
    {
        var missing = requestedZoneIds.Where(id => !zones.ContainsKey(id)).ToHashSet();
        reportedMissingZones.IntersectWith(missing);
        foreach (var zoneId in missing.Where(reportedMissingZones.Add))
        {
            var names = rules.Where(r => r.MapZoneId == zoneId).Select(r => $"'{r.Name}' ({r.Id})");
            logger.LogWarning(
                "Floor plan zone {ZoneId} no longer exists; skipping active alert rule(s) {Rules} until they are moved to another zone or disabled",
                zoneId, string.Join(", ", names));
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rules = (await Rules.Find(r => !r.IsDeleted && r.MapZoneId != null && r.Status == AlertRuleStatus.Active && r.CreateAlert).ToListAsync(ct))
            .Where(r => (r.EffectiveFrom is null || r.EffectiveFrom <= now) && (r.EffectiveTo is null || r.EffectiveTo >= now))
            .ToList();
        var requestedZoneIds = rules.Select(r => r.MapZoneId!).Distinct().ToList();

        // A rule whose zone was deleted in Maps & Floor Plans is skipped (reported once, see WarnMissingZones)
        var zones = await zoneLookup.GetManyAsync(requestedZoneIds, ct);
        WarnMissingZones(rules, requestedZoneIds, zones);
        var zoneIds = zones.Keys.ToList();

        // Zones no longer watched forget who was inside, so a rule added later starts from a fresh first look
        await Watched.DeleteManyAsync(w => !zoneIds.Contains(w.ZoneId), ct);
        await Presence.DeleteManyAsync(p => !zoneIds.Contains(p.ZoneId), ct);
        if (zoneIds.Count == 0)
        {
            return;
        }

        var gateways = await zoneLookup.GatewaysByZoneAsync(zones.Keys, ct);

        // Every tag heard by any gateway in the window: who is inside each zone, and whether a missing asset is elsewhere
        var heard = await HeardTagsAsync(null, now - PresenceWindow, ct);
        var assets = await AssetsOfTagsAsync(heard.Select(h => h.TagId).Distinct(), ct);
        var heardAnywhere = new Dictionary<string, (string Gateway, DateTime At)>();
        foreach (var h in heard)
        {
            if (assets.TryGetValue(h.TagId, out var a) && (!heardAnywhere.TryGetValue(a.Key, out var seen) || h.At > seen.At))
            {
                heardAnywhere[a.Key] = (h.GatewayId, h.At);
            }
        }

        var inside = new Dictionary<string, Dictionary<string, (AssetInfo Asset, string Gateway, string DeviceId, DateTime At)>>();
        foreach (var zoneId in zones.Keys)
        {
            var zoneGateways = gateways[zoneId].ToDictionary(g => g.GatewayId, g => g.DeviceId);
            var current = new Dictionary<string, (AssetInfo, string, string, DateTime)>();
            foreach (var h in heard.Where(h => zoneGateways.ContainsKey(h.GatewayId)))
            {
                if (!assets.TryGetValue(h.TagId, out var asset))
                {
                    continue;
                }

                if (!current.TryGetValue(asset.Key, out var seen) || h.At > seen.Item4)
                {
                    current[asset.Key] = (asset, h.GatewayId, zoneGateways[h.GatewayId], h.At);
                }
            }

            inside[zoneId] = current;
        }

        var watched = (await Watched.Find(w => zoneIds.Contains(w.ZoneId)).ToListAsync(ct)).Select(w => w.ZoneId).ToHashSet();
        var previous = (await Presence.Find(p => zoneIds.Contains(p.ZoneId)).ToListAsync(ct)).GroupBy(p => p.ZoneId).ToDictionary(g => g.Key, g => g.ToDictionary(p => p.AssetId));

        foreach (var (zoneId, current) in inside)
        {
            var zone = zones[zoneId];
            var before = previous.GetValueOrDefault(zoneId) ?? [];
            var zoneRules = rules.Where(r => r.MapZoneId == zoneId).ToList();
            var firstLook = !watched.Contains(zoneId);

            foreach (var (assetKey, entry) in current)
            {
                if (!before.TryGetValue(assetKey, out var stay))
                {
                    stay = new ZonePresenceDocument
                    {
                        Id = $"{zoneId}|{assetKey}",
                        ZoneId = zoneId,
                        AssetId = assetKey,
                        EnteredAt = firstLook ? entry.At : now,
                        Initial = firstLook
                    };
                }
                else if (stay.GoneSince is not null)
                {
                    if (stay.ExitAlertedRuleIds.Count > 0)
                    {
                        // An exit was already reported: this is a new stay
                        stay.EnteredAt = now;
                        stay.Initial = false;
                        stay.EntryAlertedRuleIds.Clear();
                        stay.DwellAlertedRuleIds.Clear();
                        stay.ExitAlertedRuleIds.Clear();
                    }

                    // Back within the debounce: the flicker is ignored
                    stay.GoneSince = null;
                }

                stay.AssetName = entry.Asset.Name;
                stay.RegistrationId = entry.Asset.RegistrationId;
                stay.AssetGroups = entry.Asset.Groups;
                stay.LastSeenAt = entry.At;
                stay.GatewayId = entry.Gateway;

                // Entry: once per stay and rule, after the rule's debounce
                if (!stay.Initial)
                {
                    foreach (var rule in zoneRules.Where(r => EntryTriggers.Contains(r.Trigger) && !stay.EntryAlertedRuleIds.Contains(r.Id)))
                    {
                        if (now - stay.EnteredAt < Debounce(rule))
                        {
                            continue;
                        }

                        stay.EntryAlertedRuleIds.Add(rule.Id);
                        if (rule.Trigger == AlertRuleTrigger.UnauthorizedEntry)
                        {
                            // An asset whose class is unknown is not authorized
                            if (!rule.AuthorizedGroups.Any(g => AlertScope.InGroup(g, entry.Asset.Groups, unknownMatches: false)))
                            {
                                await RaiseAsync(rule, zone, entry.Asset, entry.DeviceId, EventUnauthorized,
                                    $"{entry.Asset.Label} entered {zone.Label} without authorization (allowed: {string.Join(", ", rule.AuthorizedGroups)})", null, ct);
                            }

                            continue;
                        }

                        await RaiseAsync(rule, zone, entry.Asset, entry.DeviceId, EventEntry, $"{entry.Asset.Label} entered {zone.Label}", null, ct);
                    }
                }

                // Dwell: once per stay and rule
                foreach (var rule in zoneRules.Where(r => r.Trigger == AlertRuleTrigger.RemainsIn && r.DwellMinutes is > 0))
                {
                    if (stay.DwellAlertedRuleIds.Contains(rule.Id) || now - stay.EnteredAt < TimeSpan.FromMinutes(rule.DwellMinutes!.Value))
                    {
                        continue;
                    }

                    stay.DwellAlertedRuleIds.Add(rule.Id);
                    var seconds = (int)(now - stay.EnteredAt).TotalSeconds;
                    await RaiseAsync(rule, zone, entry.Asset, entry.DeviceId, EventDwell,
                        $"{entry.Asset.Label} has been in {zone.Label} for {Math.Round(seconds / 60.0)} min (limit {rule.DwellMinutes} min)", seconds, ct);
                }

                await Presence.ReplaceOneAsync(p => p.Id == stay.Id, stay, new ReplaceOptions { IsUpsert = true }, ct);
            }

            foreach (var (assetKey, stay) in before)
            {
                if (current.ContainsKey(assetKey))
                {
                    continue;
                }

                stay.GoneSince ??= now;
                var goneFor = now - stay.GoneSince.Value;
                var stayLength = stay.GoneSince.Value - stay.EnteredAt;
                var elsewhere = heardAnywhere.TryGetValue(assetKey, out var other) ? other.Gateway : null;
                var asset = new AssetInfo(assetKey, stay.AssetName ?? assetKey, stay.RegistrationId, stay.AssetGroups ?? []);
                var device = gateways[zoneId].FirstOrDefault(g => g.GatewayId == stay.GatewayId).DeviceId ?? stay.GatewayId;
                var handlers = zoneRules.Where(r => ExitTriggers.Contains(r.Trigger) || r.Trigger == AlertRuleTrigger.LocationLost).ToList();

                foreach (var rule in handlers.Where(r => !stay.ExitAlertedRuleIds.Contains(r.Id)))
                {
                    if (goneFor < Debounce(rule))
                    {
                        continue;
                    }

                    stay.ExitAlertedRuleIds.Add(rule.Id);
                    if (rule.Trigger == AlertRuleTrigger.LocationLost)
                    {
                        if (elsewhere is null)
                        {
                            await RaiseAsync(rule, zone, asset, device, EventLost,
                                $"{asset.Label} is no longer detected by any gateway (last seen in {zone.Label})", (int)stayLength.TotalSeconds, ct);
                        }

                        continue;
                    }

                    // An exit pairs with a confirmed stay: a stay shorter than the debounce never raised its entry either
                    var confirmed = stay.Initial || stay.EntryAlertedRuleIds.Contains(rule.Id) || stayLength >= Debounce(rule);
                    if (confirmed)
                    {
                        var where = elsewhere is null ? "no longer detected by any gateway" : $"now near gateway {elsewhere}";
                        await RaiseAsync(rule, zone, asset, device, EventExit, $"{asset.Label} left {zone.Label} ({where})", (int)stayLength.TotalSeconds, ct);
                    }
                }

                var settled = handlers.Count == 0 ? TimeSpan.Zero : handlers.Max(Debounce);
                if (goneFor >= settled)
                {
                    await Presence.DeleteOneAsync(p => p.Id == stay.Id, ct);
                    foreach (var rule in zoneRules.Where(r => r.AutoResolveOnExit))
                    {
                        await AutoResolveAsync(rule, asset, zone, ct);
                    }
                }
                else
                {
                    await Presence.ReplaceOneAsync(p => p.Id == stay.Id, stay, cancellationToken: ct);
                }
            }

            if (firstLook)
            {
                await Watched.ReplaceOneAsync(w => w.ZoneId == zoneId, new WatchedZoneDocument { ZoneId = zoneId, StartedAt = now }, new ReplaceOptions { IsUpsert = true }, ct);
            }
        }
    }

    private async Task RaiseAsync(AlertRule rule, MapZoneRef zone, AssetInfo asset, string? deviceId, string eventType, string message, int? durationSeconds, CancellationToken ct)
    {
        if (!InCondition(rule) || !InGroup(rule.AssetGroup, asset) || !InAssetScope(rule, asset))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var assetId = asset.RegistrationId ?? asset.Key;

        // Cooldown: one alert per asset, rule and event within the window
        if (rule.CooldownMinutes > 0)
        {
            var since = now.AddMinutes(-rule.CooldownMinutes);
            var recent = await Alerts.Find(a => a.AlertRuleId == rule.Id && a.AssetRegistrationId == assetId && a.EventType == eventType && a.RaisedAt >= since && !a.IsDeleted)
                .AnyAsync(ct);
            if (recent)
            {
                return;
            }
        }

        var next = await sequenceGenerator.GetNextValueAsync("IoTAlert", ct);
        var alert = new IoTAlert
        {
            Id = ObjectId.GenerateNewId().ToString(),
            AlertId = $"AL{next:D6}",
            RaisedAt = now,
            AssetRegistrationId = assetId,
            AssetName = asset.Name,
            DeviceId = deviceId,
            SiteId = zone.SiteId,
            SiteName = zone.SiteName,
            EventType = eventType,
            Severity = rule.Severity switch
            {
                AlertRuleSeverity.Informational or AlertRuleSeverity.Low => IoTAlertSeverity.Info,
                AlertRuleSeverity.Medium => IoTAlertSeverity.Warning,
                _ => IoTAlertSeverity.Critical
            },
            RuleSeverity = rule.Severity,
            Message = $"{message} · rule \"{rule.Name}\"",
            AlertRuleId = rule.Id,
            GeofenceId = rule.GeofenceId,
            GeofenceName = zone.Label,
            DurationSeconds = durationSeconds,
            Status = IoTAlertStatus.Open,
            History = [new IoTAlertHistoryEntry { At = now, By = EngineUser, Action = "Raised", Note = $"Rule \"{rule.Name}\"" }],
            CreatedBy = EngineUser,
            CreatedAt = now,
            ClientId = rule.ClientId,
            TenantId = rule.TenantId,
            IsDeleted = false
        };

        await Alerts.InsertOneAsync(alert, cancellationToken: ct);
        await notifications.ScheduleAsync(alert, rule, ct);
        logger.LogInformation("{AlertId} {EventType}: {Message}", alert.AlertId, eventType, alert.Message);
    }

    /// <summary>The rule's still-open alerts for the asset are resolved once it left the zone.</summary>
    private async Task AutoResolveAsync(AlertRule rule, AssetInfo asset, MapZoneRef zone, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var assetId = asset.RegistrationId ?? asset.Key;
        var open = IoTAlertStatus.Unresolved.ToList();
        await Alerts.UpdateManyAsync(
            a => a.AlertRuleId == rule.Id && a.AssetRegistrationId == assetId && open.Contains(a.Status!) && !a.IsDeleted,
            Builders<IoTAlert>.Update
                .Set(a => a.Status, IoTAlertStatus.Resolved)
                .Set(a => a.ResolvedAt, now)
                .Set(a => a.ResolvedBy, EngineUser)
                .Set(a => a.UpdatedAt, now)
                .Push(a => a.History, new IoTAlertHistoryEntry { At = now, By = EngineUser, Action = "Auto-resolved", Note = $"{asset.Label} left {zone.Label}" }),
            cancellationToken: ct);
    }

    /// <summary>Open alerts not acknowledged within the rule's escalation time are escalated once (history + a new desktop notification).</summary>
    private async Task EscalateAsync(CancellationToken ct)
    {
        var rules = await Rules.Find(r => !r.IsDeleted && r.EscalateAfterMinutes != null && r.EscalateAfterMinutes > 0).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var rule in rules)
        {
            var cutoff = now.AddMinutes(-rule.EscalateAfterMinutes!.Value);
            var due = await Alerts.Find(a => a.AlertRuleId == rule.Id && a.Status == IoTAlertStatus.Open && a.EscalatedAt == null && a.RaisedAt <= cutoff && !a.IsDeleted)
                .Limit(50).ToListAsync(ct);
            foreach (var alert in due)
            {
                var to = rule.EscalateTo ?? "Escalation group";
                await Alerts.UpdateOneAsync(a => a.Id == alert.Id, Builders<IoTAlert>.Update
                    .Set(a => a.EscalatedAt, now)
                    .Set(a => a.EscalatedTo, to)
                    .Set(a => a.UpdatedAt, now)
                    .Push(a => a.History, new IoTAlertHistoryEntry { At = now, By = "Escalation policy", Action = "Escalated", Note = $"Not acknowledged within {rule.EscalateAfterMinutes} min - escalated to {to}" }),
                    cancellationToken: ct);

                await notifications.ScheduleAsync(new IoTAlert
                {
                    AlertId = alert.AlertId,
                    AssetName = alert.AssetName,
                    EventType = $"ESCALATED to {to} - {alert.EventType}",
                    Message = alert.Message,
                    ClientId = alert.ClientId,
                    TenantId = alert.TenantId
                }, rule, ct);
                logger.LogInformation("{AlertId} escalated to {To}", alert.AlertId, to);
            }
        }
    }

    /// <summary>
    /// Working hours / custom schedule in the sites' time zone (Alerts:TimeZone; working hours Alerts:WorkStart / WorkEnd,
    /// Monday to Friday).
    /// </summary>
    private bool InCondition(AlertRule rule)
    {
        var condition = rule.Condition;
        if (condition == AlertRuleCondition.AllHours || string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["Alerts:TimeZone"] ?? "Asia/Dubai");
        }
        catch
        {
            zone = TimeZoneInfo.Utc;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        if (condition == AlertRuleCondition.Custom)
        {
            var day = local.DayOfWeek.ToString()[..3];
            if (!rule.ScheduleDays.Contains(day, StringComparer.OrdinalIgnoreCase)
                || !TimeSpan.TryParse(rule.ScheduleFrom, out var from) || !TimeSpan.TryParse(rule.ScheduleTo, out var to))
            {
                return false;
            }

            var t = local.TimeOfDay;
            return from <= to ? t >= from && t < to : t >= from || t < to;
        }

        var start = TimeSpan.TryParse(configuration["Alerts:WorkStart"], out var s) ? s : TimeSpan.FromHours(8);
        var end = TimeSpan.TryParse(configuration["Alerts:WorkEnd"], out var e) ? e : TimeSpan.FromHours(18);
        var working = local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && local.TimeOfDay >= start && local.TimeOfDay < end;
        return condition == AlertRuleCondition.WorkingHours ? working : !working;
    }

    /// <summary>Specific assets on the rule (registration id, asset tag or name); none = every asset.</summary>
    private static bool InAssetScope(AlertRule rule, AssetInfo asset) => AlertScope.InAssets(rule, asset.Key, asset.RegistrationId, asset.Name);

    private static bool InGroup(string group, AssetInfo asset) => AlertScope.InGroup(group, asset.Groups);

    private sealed record Heard(string TagId, string GatewayId, DateTime At);

    /// <summary>Newest reading per (tag, gateway) since <paramref name="since"/>, from the given gateways (null = all).</summary>
    private async Task<List<Heard>> HeardTagsAsync(HashSet<string>? gatewayIds, DateTime since, CancellationToken ct)
    {
        if (gatewayIds is { Count: 0 })
        {
            return [];
        }

        var match = new BsonDocument
        {
            { "timestamp", new BsonDocument("$gte", since) },
            { "meta.tagid", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) }
        };
        if (gatewayIds is not null)
        {
            match["meta.gatewayid"] = new BsonDocument("$in", new BsonArray(gatewayIds));
        }

        var pipeline = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument { { "tag", new BsonDocument("$toLower", "$meta.tagid") }, { "gw", new BsonDocument("$toLower", "$meta.gatewayid") } } },
                { "at", new BsonDocument("$max", "$timestamp") }
            })
        };

        var rows = await database.GetCollection<BsonDocument>("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return rows
            .Where(r => r["_id"]["tag"].IsString && r["_id"]["gw"].IsString)
            .Select(r => new Heard(r["_id"]["tag"].AsString, r["_id"]["gw"].AsString, r["at"].ToUniversalTime())).ToList();
    }

    private sealed record AssetInfo(string Key, string Name, string? RegistrationId, List<string> Groups)
    {
        public string Label => RegistrationId is null || RegistrationId == Name ? Name : $"{Name} ({RegistrationId})";
    }

    /// <summary>Tag (lower case) → its asset via Tag Management; the tag link holds the registration id or the asset tag.</summary>
    private async Task<Dictionary<string, AssetInfo>> AssetsOfTagsAsync(IEnumerable<string> tagIds, CancellationToken ct)
    {
        var tags = tagIds.ToHashSet();
        var result = new Dictionary<string, AssetInfo>();
        if (tags.Count == 0)
        {
            return result;
        }

        var links = await database.GetCollection<BsonDocument>("TrackingTag")
            .Find(Builders<BsonDocument>.Filter.Ne("is_deleted", true) & Builders<BsonDocument>.Filter.Ne("asset_registration_id", BsonNull.Value))
            .Project(Builders<BsonDocument>.Projection.Include("tag_id").Include("asset_registration_id"))
            .ToListAsync(ct);
        var tagToKey = links
            .Where(l => l.GetValue("tag_id", BsonNull.Value).IsString && l.GetValue("asset_registration_id", BsonNull.Value).IsString)
            .Select(l => (Tag: l["tag_id"].AsString.ToLowerInvariant(), Key: l["asset_registration_id"].AsString.Trim()))
            .Where(l => tags.Contains(l.Tag) && l.Key.Length > 0)
            .GroupBy(l => l.Tag)
            .ToDictionary(g => g.Key, g => g.First().Key);
        if (tagToKey.Count == 0)
        {
            return result;
        }

        var keys = tagToKey.Values.Distinct().ToList();
        var regex = new BsonArray(keys.Select(k => new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(k)}$", "i")));
        var registrations = await database.GetCollection<BsonDocument>("AssetRegistration")
            .Find(new BsonDocument
            {
                { "is_deleted", new BsonDocument("$ne", true) },
                { "$or", new BsonArray { new BsonDocument("asset_registration_id", new BsonDocument("$in", regex)), new BsonDocument("basic_information.asset_tag", new BsonDocument("$in", regex)) } }
            })
            .ToListAsync(ct);

        string? Str(BsonDocument d, params string[] path)
        {
            BsonValue v = d;
            foreach (var p in path)
            {
                if (!v.IsBsonDocument || !v.AsBsonDocument.TryGetValue(p, out v))
                {
                    return null;
                }
            }

            return v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString : null;
        }

        foreach (var (tag, key) in tagToKey)
        {
            var reg = registrations.FirstOrDefault(r =>
                string.Equals(Str(r, "asset_registration_id"), key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Str(r, "basic_information", "asset_tag"), key, StringComparison.OrdinalIgnoreCase));
            var registrationId = reg is null ? null : Str(reg, "asset_registration_id");
            var groups = reg is null
                ? []
                : new[] { Str(reg, "classification", "category"), Str(reg, "classification", "sub_category"), Str(reg, "classification", "asset_type") }
                    .Where(g => g is not null).Select(g => g!).ToList();
            result[tag] = new AssetInfo(registrationId ?? key, reg is null ? key : Str(reg, "basic_information", "asset_name") ?? key, registrationId, groups);
        }

        return result;
    }
}
