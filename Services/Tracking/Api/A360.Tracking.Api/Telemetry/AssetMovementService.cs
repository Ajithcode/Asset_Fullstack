using System.Text.RegularExpressions;
using A360.Tracking.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Telemetry;

/// <summary>
/// Asset movement history: every tag mapped to an asset in Tag Management (TrackingTag with asset_registration_id) is matched
/// to its NetworkTimeSeries readings (meta.tagid = the tag id, or the tag's value / MAC without separators, ignoring case).
/// Each gateway (meta.gatewayid) that heard the tag counts once — a tag heard by two gateways counts 2 — and each gateway is
/// placed in its zone through maps_plots. The history is the readings in time order with consecutive readings in the same
/// zone (or the same gateway, when it is not plotted) folded into one visit; every change of visit is one movement.
/// </summary>
public sealed class AssetMovementService(IMongoDatabase database)
{
    private const string TagsCollection = "TrackingTag";
    private const string ReadingsCollection = "NetworkTimeSeries";
    private const string PlotsCollection = "maps_plots";

    /// <summary>Readings are bucketed per tag, gateway and minute before building the history.</summary>
    private const int BucketMinutes = 1;

    private IMongoCollection<BsonDocument> C(string name) => database.GetCollection<BsonDocument>(name);

    /// <param name="assetId">Asset registration id or asset code; all mapped assets when empty.</param>
    /// <param name="tagId">Only this tag (tag id or MAC / value).</param>
    /// <param name="technology">Only tags of this technology (BLE, Passive RFID, Active RFID, RFID = both RFID kinds, QR Code, Barcode, NFC).</param>
    /// <param name="includeHistory">Return the visit-by-visit history of each asset, not only the counts.</param>
    public async Task<AssetMovementPage> GetAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? assetId,
        string? tagId,
        string? technology,
        string? search,
        bool includeHistory,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var tags = await MappedTagsAsync(assetId, tagId, technology, ct);
        if (tags.Count == 0)
        {
            return new AssetMovementPage(fromUtc, toUtc, 0, page, pageSize, 0, ByTechnology([]), []);
        }

        // every key a reading may carry for a tag → the tag
        var tagByKey = new Dictionary<string, MappedTag>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            foreach (var key in tag.Keys)
            {
                tagByKey.TryAdd(key, tag);
            }
        }

        var buckets = await BucketsAsync(tagByKey.Keys.ToList(), fromUtc, toUtc, ct);
        var plots = await PlotsAsync(buckets.Select(b => b.GatewayId).Distinct().ToList(), ct);

        var assets = new List<AssetMovement>();
        foreach (var asset in tags.GroupBy(t => t.AssetRegistrationId, StringComparer.OrdinalIgnoreCase))
        {
            var first = asset.First();
            var keys = asset.SelectMany(t => t.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var readings = buckets.Where(b => keys.Contains(b.TagKey)).OrderBy(b => b.From).ThenBy(b => b.GatewayId).ToList();

            var gateways = readings
                .GroupBy(b => b.GatewayId, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    plots.TryGetValue(g.Key, out var p);
                    return new GatewaySeen(
                        g.Key, p?.GatewayName, p?.ZoneId, p?.ZoneName, p?.ZoneCode, p?.FloorName,
                        g.Min(b => b.From), g.Max(b => b.To), g.Sum(b => b.Count));
                })
                .OrderBy(g => g.FirstSeen)
                .ToList();

            var history = Visits(readings, plots);
            var zones = gateways.Select(g => g.ZoneId ?? g.ZoneName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var last = history.LastOrDefault();

            // each tag on its own: which gateways heard it and how often it changed zone
            var tagCounts = asset
                .Select(t =>
                {
                    var tagKeys = t.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var tagReadings = readings.Where(b => tagKeys.Contains(b.TagKey)).ToList();
                    return new TagMovement(
                        t.TagId,
                        t.Technology,
                        tagReadings.Select(b => b.GatewayId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        Math.Max(0, Visits(tagReadings, plots).Count - 1),
                        tagReadings.Sum(b => b.Count),
                        tagReadings.Count == 0 ? null : tagReadings.Max(b => b.To),
                        tagReadings.Select(b => b.GatewayId).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
                })
                .ToList();

            assets.Add(new AssetMovement(
                first.AssetRegistrationId,
                first.AssetCode,
                first.AssetName,
                asset.Select(t => t.TagId).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                asset.Select(t => t.Technology).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                tagCounts,
                gateways.Count,
                zones,
                Math.Max(0, history.Count - 1),
                readings.Sum(r => r.Count),
                readings.Count == 0 ? null : readings[0].From,
                readings.Count == 0 ? null : readings.Max(r => r.To),
                last is null ? null : new CurrentLocation(last.GatewayIds, last.ZoneId, last.ZoneName, last.ZoneCode, last.FloorName, last.To),
                gateways,
                includeHistory ? history : null));
        }

        IEnumerable<AssetMovement> filtered = assets;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            filtered = filtered.Where(a =>
                Contains(a.AssetRegistrationId, s) || Contains(a.AssetCode, s) || Contains(a.AssetName, s)
                || a.TagIds.Any(t => Contains(t, s)) || a.Gateways.Any(g => Contains(g.GatewayId, s) || Contains(g.ZoneName, s)));
        }

        // assets that moved the most first, then the ones heard most recently
        var ordered = filtered
            .OrderByDescending(a => a.GatewayCount)
            .ThenByDescending(a => a.MovementCount)
            .ThenByDescending(a => a.LastSeenAt)
            .ThenBy(a => a.AssetName)
            .ToList();

        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new AssetMovementPage(
            fromUtc, toUtc, ordered.Count, page, pageSize, (int)Math.Ceiling(ordered.Count / (double)pageSize), ByTechnology(ordered), items);
    }

    /// <summary>
    /// Counts per tag technology over all matching assets (not only the page): every technology of Tag Management is listed,
    /// with zeros when none of its tags is mapped. A gateway that heard tags of two technologies counts once in each.
    /// </summary>
    private static List<TechnologyMovement> ByTechnology(List<AssetMovement> assets)
    {
        var tags = assets.SelectMany(a => a.Tags.Select(t => (Asset: a.AssetRegistrationId, Tag: t))).ToList();
        var names = TagTechnology.All
            .Concat(tags.Select(x => x.Tag.Technology).Where(t => !TagTechnology.All.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return names.Select(name =>
            {
                var of = tags.Where(x => string.Equals(x.Tag.Technology, name, StringComparison.OrdinalIgnoreCase)).ToList();
                return new TechnologyMovement(
                    name,
                    of.Select(x => x.Asset).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    of.Count,
                    of.Count(x => x.Tag.ReadingCount > 0),
                    of.SelectMany(x => x.Tag.GatewayIds).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    of.Sum(x => x.Tag.MovementCount),
                    of.Sum(x => x.Tag.ReadingCount));
            })
            .ToList();
    }

    /// <summary>Folds the time-ordered readings into visits: consecutive readings in the same zone (or same unplotted gateway).</summary>
    private static List<MovementVisit> Visits(List<Bucket> readings, Dictionary<string, Plot> plots)
    {
        var visits = new List<MovementVisit>();
        foreach (var r in readings)
        {
            plots.TryGetValue(r.GatewayId, out var p);
            var place = p?.ZoneId ?? p?.ZoneName ?? $"gw:{r.GatewayId}";

            if (visits.Count > 0 && string.Equals(visits[^1].PlaceKey, place, StringComparison.OrdinalIgnoreCase))
            {
                var v = visits[^1];
                var gatewayIds = v.GatewayIds.Contains(r.GatewayId, StringComparer.OrdinalIgnoreCase) ? v.GatewayIds : [.. v.GatewayIds, r.GatewayId];
                var to = r.To > v.To ? r.To : v.To;
                visits[^1] = v with { GatewayIds = gatewayIds, To = to, Minutes = Math.Round((to - v.From).TotalMinutes, 1), Readings = v.Readings + r.Count };
                continue;
            }

            visits.Add(new MovementVisit(
                place,
                visits.Count == 0 ? null : visits[^1].ZoneName ?? visits[^1].GatewayIds[0],
                [r.GatewayId],
                p?.ZoneId,
                p?.ZoneName,
                p?.ZoneCode,
                p?.FloorName,
                r.From,
                r.To,
                Math.Round((r.To - r.From).TotalMinutes, 1),
                r.Count));
        }

        return visits;
    }

    /// <summary>Tags mapped to an asset in Tag Management, with every id a reading may carry for them (lower case).</summary>
    private async Task<List<MappedTag>> MappedTagsAsync(string? assetId, string? tagId, string? technology, CancellationToken ct)
    {
        var filter = new BsonDocument
        {
            { "is_deleted", new BsonDocument("$ne", true) },
            { "asset_registration_id", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) }
        };

        if (!string.IsNullOrWhiteSpace(assetId))
        {
            var exact = new BsonRegularExpression($"^{Regex.Escape(assetId.Trim())}$", "i");
            filter["$or"] = new BsonArray { new BsonDocument("asset_registration_id", exact), new BsonDocument("asset_code", exact) };
        }

        if (!string.IsNullOrWhiteSpace(technology))
        {
            // "RFID" covers Passive and Active RFID
            var wanted = technology.Trim();
            filter["technology"] = string.Equals(wanted, "RFID", StringComparison.OrdinalIgnoreCase)
                ? new BsonDocument("$in", new BsonArray { TagTechnology.PassiveRfid, TagTechnology.ActiveRfid })
                : new BsonRegularExpression($"^{Regex.Escape(wanted)}$", "i");
        }

        var rows = await C(TagsCollection).Find(filter).ToListAsync(ct);
        var tags = new List<MappedTag>();
        foreach (var r in rows)
        {
            var id = Str(r, "tag_id");
            var assetRegistrationId = Str(r, "asset_registration_id");
            if (id is null || assetRegistrationId is null) continue;

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id.Trim().ToLowerInvariant(), Normalize(id) };
            if (Str(r, "value") is { } value)
            {
                keys.Add(value.Trim().ToLowerInvariant());
                keys.Add(Normalize(value));
            }

            if (!string.IsNullOrWhiteSpace(tagId))
            {
                var wanted = tagId.Trim();
                if (!keys.Contains(wanted.ToLowerInvariant()) && !keys.Contains(Normalize(wanted))) continue;
            }

            tags.Add(new MappedTag(
                id, Str(r, "technology") ?? "Unknown", assetRegistrationId.Trim(), Str(r, "asset_code"), Str(r, "asset_name"),
                keys.Where(k => k.Length > 0).ToList()));
        }

        return tags;
    }

    /// <summary>Readings of the tags in the period, grouped per tag, gateway and minute (first / last time and count).</summary>
    private async Task<List<Bucket>> BucketsAsync(List<string> tagKeys, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var notBlank = new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" });
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "timestamp", new BsonDocument { { "$gte", fromUtc }, { "$lt", toUtc } } },
                { "meta.tagid", new BsonDocument("$in", new BsonArray(tagKeys)) },
                { "meta.gatewayid", notBlank }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "tag", new BsonDocument("$toLower", "$meta.tagid") },
                        { "gw", new BsonDocument("$toLower", "$meta.gatewayid") },
                        { "slot", new BsonDocument("$dateTrunc", new BsonDocument { { "date", "$timestamp" }, { "unit", "minute" }, { "binSize", BucketMinutes } }) }
                    } },
                { "from", new BsonDocument("$min", "$timestamp") },
                { "to", new BsonDocument("$max", "$timestamp") },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var rows = await C(ReadingsCollection)
            .Aggregate<BsonDocument>(pipeline, new AggregateOptions { AllowDiskUse = true }, ct)
            .ToListAsync(ct);

        return rows
            .Where(r => r["_id"]["tag"].IsString && r["_id"]["gw"].IsString)
            .Select(r => new Bucket(r["_id"]["tag"].AsString, r["_id"]["gw"].AsString, r["from"].ToUniversalTime(), r["to"].ToUniversalTime(), r["count"].ToInt32()))
            .ToList();
    }

    /// <summary>Where each gateway is plotted (maps_plots, kind device, by device_id or item_id), keyed by lower-case gateway id.</summary>
    private async Task<Dictionary<string, Plot>> PlotsAsync(List<string> gatewayIds, CancellationToken ct)
    {
        var result = new Dictionary<string, Plot>(StringComparer.OrdinalIgnoreCase);
        if (gatewayIds.Count == 0) return result;

        var rows = await C(PlotsCollection)
            .Find(new BsonDocument { { "kind", "device" }, { "is_deleted", new BsonDocument("$ne", true) } })
            .Sort(new BsonDocument { { "updated_at", -1 }, { "created_at", -1 }, { "_id", -1 } })
            .ToListAsync(ct);

        var wanted = gatewayIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var key = (Str(r, "device_id") ?? Str(r, "item_id"))?.ToLowerInvariant();
            if (key is null || !wanted.Contains(key) || result.ContainsKey(key)) continue;

            result[key] = new Plot(
                Str(r, "name"),
                Str(r, "zone_id") ?? Str(r, "location", "zone_id"),
                Str(r, "zone_name") ?? Str(r, "location", "zone_name"),
                Str(r, "zone_code") ?? Str(r, "location", "zone_code"),
                Str(r, "location", "floor_name"));
        }

        return result;
    }

    // "C3:00:00:99:13:8F" -> "c3000099138f", the format the network server writes tag ids in.
    private static string Normalize(string id) => id.Replace(":", "").Replace("-", "").Trim().ToLowerInvariant();

    private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private static string? Str(BsonDocument doc, params string[] path)
    {
        BsonValue v = doc;
        foreach (var p in path)
        {
            if (!v.IsBsonDocument || !v.AsBsonDocument.TryGetValue(p, out v)) return null;
        }

        return v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString : null;
    }

    private sealed record MappedTag(string TagId, string Technology, string AssetRegistrationId, string? AssetCode, string? AssetName, List<string> Keys);
    private sealed record Bucket(string TagKey, string GatewayId, DateTime From, DateTime To, int Count);
    private sealed record Plot(string? GatewayName, string? ZoneId, string? ZoneName, string? ZoneCode, string? FloorName);
}

/// <param name="ByTechnology">Counts per tag technology over all matching assets (every page).</param>
public sealed record AssetMovementPage(
    DateTime From, DateTime To, int Total, int Page, int PageSize, int TotalPages, List<TechnologyMovement> ByTechnology, List<AssetMovement> Items);

/// <param name="Assets">Assets with at least one mapped tag of this technology.</param>
/// <param name="Tags">Mapped tags of this technology.</param>
/// <param name="TagsHeard">Of those, tags with at least one reading in the period.</param>
/// <param name="GatewayCount">Distinct gateways that heard tags of this technology.</param>
/// <param name="MovementCount">Zone-to-zone movements of tags of this technology, summed over the tags.</param>
public sealed record TechnologyMovement(
    string Technology, int Assets, int Tags, int TagsHeard, int GatewayCount, int MovementCount, int ReadingCount);

/// <summary>One mapped tag of an asset and its own counts.</summary>
public sealed record TagMovement(
    string TagId, string Technology, int GatewayCount, int MovementCount, int ReadingCount, DateTime? LastSeenAt, List<string> GatewayIds);

/// <param name="GatewayCount">Distinct gateways that heard any of the asset's tags in the period (two gateways = 2).</param>
/// <param name="ZoneCount">Distinct zones of those gateways (unplotted gateways are not counted).</param>
/// <param name="MovementCount">Changes from one zone (or unplotted gateway) to another, in time order.</param>
/// <param name="ReadingCount">NetworkTimeSeries readings of the asset's tags in the period.</param>
/// <param name="History">Visit by visit, oldest first; null unless requested.</param>
public sealed record AssetMovement(
    string AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    List<string> TagIds,
    List<string> Technologies,
    List<TagMovement> Tags,
    int GatewayCount,
    int ZoneCount,
    int MovementCount,
    int ReadingCount,
    DateTime? FirstSeenAt,
    DateTime? LastSeenAt,
    CurrentLocation? CurrentLocation,
    List<GatewaySeen> Gateways,
    List<MovementVisit>? History);

public sealed record CurrentLocation(List<string> GatewayIds, string? ZoneId, string? ZoneName, string? ZoneCode, string? FloorName, DateTime LastSeenAt);

/// <summary>A gateway that heard the asset, with the zone it is plotted in.</summary>
public sealed record GatewaySeen(
    string GatewayId,
    string? GatewayName,
    string? ZoneId,
    string? ZoneName,
    string? ZoneCode,
    string? FloorName,
    DateTime FirstSeen,
    DateTime LastSeen,
    int Readings);

/// <summary>A stay of the asset in one zone (or with one unplotted gateway). <see cref="FromPlace"/> is where it came from.</summary>
public sealed record MovementVisit(
    [property: System.Text.Json.Serialization.JsonIgnore] string PlaceKey,
    string? FromPlace,
    List<string> GatewayIds,
    string? ZoneId,
    string? ZoneName,
    string? ZoneCode,
    string? FloorName,
    DateTime From,
    DateTime To,
    double Minutes,
    int Readings);
