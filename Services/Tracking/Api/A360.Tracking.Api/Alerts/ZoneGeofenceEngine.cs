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
/// "Geofence Entry" (enters), "Geofence Exit" (no zone gateway heard it for the window) and "Dwell Exceeded"
/// (inside longer than the rule's dwell minutes, once per stay). The first look at a zone only records who is already
/// inside. New alerts get their desktop notifications scheduled (<see cref="AlertNotifications"/>).
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
    public const string EventExit = "Geofence Exit";
    public const string EventDwell = "Dwell Exceeded";

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

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var rules = await Rules.Find(r => !r.IsDeleted && r.MapZoneId != null && r.Status == AlertRuleStatus.Active && r.CreateAlert).ToListAsync(ct);
        var zoneIds = rules.Select(r => r.MapZoneId!).Distinct().ToList();

        // Zones no longer watched forget who was inside, so a rule added later starts from a fresh first look
        await Watched.DeleteManyAsync(w => !zoneIds.Contains(w.ZoneId), ct);
        await Presence.DeleteManyAsync(p => !zoneIds.Contains(p.ZoneId), ct);
        if (rules.Count == 0)
        {
            return;
        }
        var zones = await zoneLookup.GetManyAsync(zoneIds, ct);
        var gateways = await zoneLookup.GatewaysByZoneAsync(zones.Keys, ct);
        var now = DateTime.UtcNow;

        // Who is inside each zone now
        var heard = await HeardTagsAsync(gateways.Values.SelectMany(g => g.Select(x => x.GatewayId)).ToHashSet(), now - PresenceWindow, ct);
        var assets = await AssetsOfTagsAsync(heard.Select(h => h.TagId).Distinct(), ct);
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
                if (before.TryGetValue(assetKey, out var stay))
                {
                    stay.LastSeenAt = entry.At;
                    stay.GatewayId = entry.Gateway;
                    // Dwell: once per stay and rule
                    foreach (var rule in zoneRules.Where(r => r.Trigger == AlertRuleTrigger.RemainsIn && r.DwellMinutes is > 0))
                    {
                        if (stay.DwellAlertedRuleIds.Contains(rule.Id) || now - stay.EnteredAt < TimeSpan.FromMinutes(rule.DwellMinutes!.Value))
                        {
                            continue;
                        }

                        stay.DwellAlertedRuleIds.Add(rule.Id);
                        var minutes = (int)Math.Round((now - stay.EnteredAt).TotalMinutes);
                        await RaiseAsync(rule, zone, entry.Asset, entry.DeviceId, EventDwell, $"{entry.Asset.Label} has been in {zone.Label} for {minutes} min (limit {rule.DwellMinutes} min)", ct);
                    }

                    await Presence.ReplaceOneAsync(p => p.Id == stay.Id, stay, cancellationToken: ct);
                    continue;
                }

                var presence = new ZonePresenceDocument
                {
                    Id = $"{zoneId}|{assetKey}",
                    ZoneId = zoneId,
                    AssetId = assetKey,
                    AssetName = entry.Asset.Name,
                    GatewayId = entry.Gateway,
                    EnteredAt = firstLook ? entry.At : now,
                    LastSeenAt = entry.At
                };
                await Presence.ReplaceOneAsync(p => p.Id == presence.Id, presence, new ReplaceOptions { IsUpsert = true }, ct);
                if (firstLook)
                {
                    continue;
                }

                foreach (var rule in zoneRules.Where(r => r.Trigger is AlertRuleTrigger.Enters or AlertRuleTrigger.EntersOrExits))
                {
                    await RaiseAsync(rule, zone, entry.Asset, entry.DeviceId, EventEntry, $"{entry.Asset.Label} entered {zone.Label}", ct);
                }
            }

            foreach (var (assetKey, stay) in before)
            {
                if (current.ContainsKey(assetKey))
                {
                    continue;
                }

                await Presence.DeleteOneAsync(p => p.Id == stay.Id, ct);
                var asset = new AssetInfo(assetKey, stay.AssetName ?? assetKey, null, []);
                foreach (var rule in zoneRules.Where(r => r.Trigger is AlertRuleTrigger.Exits or AlertRuleTrigger.EntersOrExits))
                {
                    var device = gateways[zoneId].FirstOrDefault(g => g.GatewayId == stay.GatewayId).DeviceId ?? stay.GatewayId;
                    await RaiseAsync(rule, zone, asset, device, EventExit, $"{asset.Label} left {zone.Label}", ct);
                }
            }

            if (firstLook)
            {
                await Watched.ReplaceOneAsync(w => w.ZoneId == zoneId, new WatchedZoneDocument { ZoneId = zoneId, StartedAt = now }, new ReplaceOptions { IsUpsert = true }, ct);
            }
        }
    }

    private async Task RaiseAsync(AlertRule rule, MapZoneRef zone, AssetInfo asset, string? deviceId, string eventType, string message, CancellationToken ct)
    {
        if (!InCondition(rule.Condition) || !InGroup(rule.AssetGroup, asset))
        {
            return;
        }

        var next = await sequenceGenerator.GetNextValueAsync("IoTAlert", ct);
        var now = DateTime.UtcNow;
        var alert = new IoTAlert
        {
            Id = ObjectId.GenerateNewId().ToString(),
            AlertId = $"AL{next:D6}",
            RaisedAt = now,
            AssetRegistrationId = asset.RegistrationId ?? asset.Key,
            AssetName = asset.Name,
            DeviceId = deviceId,
            SiteId = zone.SiteId,
            SiteName = zone.SiteName,
            EventType = eventType,
            Severity = rule.Severity switch
            {
                AlertRuleSeverity.Low => IoTAlertSeverity.Info,
                AlertRuleSeverity.Medium => IoTAlertSeverity.Warning,
                _ => IoTAlertSeverity.Critical
            },
            Message = $"{message} · rule \"{rule.Name}\"",
            AlertRuleId = rule.Id,
            Status = IoTAlertStatus.Open,
            CreatedBy = "Geofence engine",
            CreatedAt = now,
            ClientId = rule.ClientId,
            TenantId = rule.TenantId,
            IsDeleted = false
        };

        await Alerts.InsertOneAsync(alert, cancellationToken: ct);
        await notifications.ScheduleAsync(alert, rule, ct);
        logger.LogInformation("{AlertId} {EventType}: {Message}", alert.AlertId, eventType, alert.Message);
    }

    /// <summary>Working hours in the sites' time zone (Alerts:TimeZone, Alerts:WorkStart / WorkEnd, Monday to Friday).</summary>
    private bool InCondition(string condition)
    {
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
        var start = TimeSpan.TryParse(configuration["Alerts:WorkStart"], out var s) ? s : TimeSpan.FromHours(8);
        var end = TimeSpan.TryParse(configuration["Alerts:WorkEnd"], out var e) ? e : TimeSpan.FromHours(18);
        var working = local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && local.TimeOfDay >= start && local.TimeOfDay < end;
        return condition == AlertRuleCondition.WorkingHours ? working : !working;
    }

    /// <summary>"All assets" matches every asset; another group matches the asset's category, sub-category or type.</summary>
    private static bool InGroup(string group, AssetInfo asset)
    {
        if (string.IsNullOrWhiteSpace(group) || group.Trim().StartsWith("All", StringComparison.OrdinalIgnoreCase) || asset.Groups.Count == 0)
        {
            return true;
        }

        static string Norm(string v) => v.Trim().TrimEnd('s', 'S').ToLowerInvariant();
        var wanted = Norm(group);
        return asset.Groups.Any(g => Norm(g) == wanted || Norm(g).Contains(wanted) || wanted.Contains(Norm(g)));
    }

    private sealed record Heard(string TagId, string GatewayId, DateTime At);

    /// <summary>Newest reading per (tag, gateway) from the given gateways since <paramref name="since"/>.</summary>
    private async Task<List<Heard>> HeardTagsAsync(HashSet<string> gatewayIds, DateTime since, CancellationToken ct)
    {
        if (gatewayIds.Count == 0)
        {
            return [];
        }

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "timestamp", new BsonDocument("$gte", since) },
                { "meta.gatewayid", new BsonDocument("$in", new BsonArray(gatewayIds)) },
                { "meta.tagid", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument { { "tag", new BsonDocument("$toLower", "$meta.tagid") }, { "gw", new BsonDocument("$toLower", "$meta.gatewayid") } } },
                { "at", new BsonDocument("$max", "$timestamp") }
            })
        };

        var rows = await database.GetCollection<BsonDocument>("NetworkTimeSeries").Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return rows.Select(r => new Heard(r["_id"]["tag"].AsString, r["_id"]["gw"].AsString, r["at"].ToUniversalTime())).ToList();
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
