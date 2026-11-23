using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Telemetry;

/// <summary>
/// Latest location of every tag: each unique tag's newest NetworkTimeSeries reading that carries a gateway id, joined
/// <list type="bullet">
/// <item>to where that gateway is plotted (maps_plots, kind "device", meta.gatewayid = device_id, falling back to item_id)
/// for its zone, floor, building and site;</item>
/// <item>to the tag registered in Tag Management (TrackingTag, meta.tagid = tag_id or value) and through its
/// asset_registration_id to the asset (AssetRegistration) for the asset's basic information.</item>
/// </list>
/// Ids are compared ignoring case, ':' , '-' and spaces (c3000099138f = C3:00:00:99:13:8F). Every link that could not
/// be made is listed in <see cref="TagLocation.MatchIssues"/>. Serves GET /api/real-time-location/tags and its /live WebSocket.
/// </summary>
public sealed class TagLocationService(IMongoDatabase database)
{
    public const string ReadingsCollection = "NetworkTimeSeries";
    public const string PlotsCollection = "maps_plots";
    public const string TrackingTagCollection = "TrackingTag";
    public const string AssetRegistrationCollection = "AssetRegistration";

    /// <summary>A tag whose newest reading is older than this is Idle (same rule as the dashboard).</summary>
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(10);

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);
    private static readonly string[] AssetOwnTagFields = ["asset_tag", "rfid_tag_id", "nfc_uid", "barcode"];

    /// <param name="since">Only tags heard at or after this time.</param>
    /// <param name="zonedOnly">Only tags whose gateway is plotted on a floor plan.</param>
    public async Task<TagLocationSnapshot> GetLatestAsync(
        DateTime since,
        string? siteId,
        string? floorPlanId,
        string? zoneId,
        bool zonedOnly,
        CancellationToken ct)
    {
        var readings = await LatestPerTagAsync(since, ct);
        var plots = await GatewayPlotsAsync(ct);

        // Filter on the gateway first, so assets are only looked up for the tags that are returned.
        var kept = new List<(LatestReading Reading, GatewayPlot? Plot)>(readings.Count);
        foreach (var r in readings)
        {
            plots.TryGetValue(Normalize(r.GatewayId)!, out var p);
            if (zonedOnly && p is null) continue;
            if (!string.IsNullOrWhiteSpace(siteId) && !string.Equals(p?.SiteId, siteId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(floorPlanId) && !string.Equals(p?.FloorPlanId, floorPlanId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(zoneId) && !string.Equals(p?.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase)) continue;
            kept.Add((r, p));
        }

        var links = await AssetLinksAsync(kept.Select(k => k.Reading.TagId).ToList(), ct);
        var now = DateTime.UtcNow;

        var tags = new List<TagLocation>(kept.Count);
        foreach (var (r, p) in kept)
        {
            var link = links[r.TagId];
            var issues = new List<string>(link.Issues);
            if (p is null) issues.Add(MatchIssue.GatewayNotPlotted);
            else
            {
                if (p.PlotCount > 1) issues.Add(MatchIssue.GatewayPlottedMultipleTimes);
                if (p.ZoneId is null && p.ZoneName is null) issues.Add(MatchIssue.GatewayPlotHasNoZone);
            }

            var connection = !string.Equals(r.Status, "online", StringComparison.OrdinalIgnoreCase) ? "Offline"
                : now - r.Timestamp > IdleAfter ? "Idle"
                : "Online";

            tags.Add(new TagLocation(
                r.TagId, r.GatewayId, p?.DeviceId, p?.GatewayName, r.Timestamp, r.Status, connection, r.Rssi, r.Battery,
                p is not null, p?.ZoneId, p?.ZoneName, p?.ZoneCode, p?.FloorPlanId, p?.FloorName, p?.BuildingName,
                p?.SiteId, p?.SiteName, p?.LocationPath, p?.X, p?.Y,
                link.TrackingTagId, link.TagStatus, link.Asset, link.AssetSource, issues));
        }

        return new TagLocationSnapshot(
            tags.Count,
            tags.Count(t => t.Connection == "Online"),
            tags.Count(t => t.Plotted),
            tags.Count(t => t.Asset is not null),
            tags);
    }

    /// <summary>Newest reading of each tag (lower-case tag id) that has a gateway id, newest first.</summary>
    private async Task<List<LatestReading>> LatestPerTagAsync(DateTime since, CancellationToken ct)
    {
        var notBlank = new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" });
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "timestamp", new BsonDocument("$gte", since) },
                { "meta.tagid", notBlank },
                { "meta.gatewayid", notBlank }
            }),
            new BsonDocument("$sort", new BsonDocument("timestamp", -1)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$toLower", "$meta.tagid") },
                { "gatewayid", new BsonDocument("$first", new BsonDocument("$toLower", "$meta.gatewayid")) },
                { "timestamp", new BsonDocument("$first", "$timestamp") },
                { "status", new BsonDocument("$first", "$status") },
                { "rssi", new BsonDocument("$first", "$rssi") },
                { "battery", new BsonDocument("$first", "$battery") }
            }),
            new BsonDocument("$sort", new BsonDocument("timestamp", -1))
        };

        var rows = await database.GetCollection<BsonDocument>(ReadingsCollection)
            .Aggregate<BsonDocument>(pipeline, new AggregateOptions { AllowDiskUse = true }, ct)
            .ToListAsync(ct);

        return rows.Select(d => new LatestReading(
                d["_id"].AsString,
                d["gatewayid"].AsString,
                d["timestamp"].ToUniversalTime(),
                d.GetValue("status", BsonNull.Value) is { IsString: true } s ? s.AsString : null,
                Number(d.GetValue("rssi", BsonNull.Value)),
                Number(d.GetValue("battery", BsonNull.Value))))
            .ToList();
    }

    /// <summary>Plotted gateways (devices) keyed by normalized device id (device_id, falling back to item_id).</summary>
    private async Task<Dictionary<string, GatewayPlot>> GatewayPlotsAsync(CancellationToken ct)
    {
        var rows = await database.GetCollection<BsonDocument>(PlotsCollection)
            .Find(new BsonDocument { { "kind", "device" }, { "is_deleted", new BsonDocument("$ne", true) } })
            .ToListAsync(ct);

        var result = new Dictionary<string, GatewayPlot>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            var deviceId = Str(r, "device_id") ?? Str(r, "item_id");
            var key = Normalize(deviceId);
            if (key is null) continue;

            // The same gateway plotted more than once: keep the first, but report it (its zone may be the wrong one).
            if (result.TryGetValue(key, out var existing))
            {
                result[key] = existing with { PlotCount = existing.PlotCount + 1 };
                continue;
            }

            result[key] = new GatewayPlot(
                deviceId!,
                Str(r, "name"),
                Str(r, "zone_id") ?? Str(r, "location", "zone_id"),
                Str(r, "zone_name") ?? Str(r, "location", "zone_name"),
                Str(r, "zone_code") ?? Str(r, "location", "zone_code"),
                Str(r, "floor_plan_id"),
                Str(r, "location", "floor_name"),
                Str(r, "location", "building_name"),
                Str(r, "location", "site_id"),
                Str(r, "location", "site_name"),
                Str(r, "location", "location_path"),
                Number(r.GetValue("x", BsonNull.Value)),
                Number(r.GetValue("y", BsonNull.Value)),
                1);
        }

        return result;
    }

    /// <summary>
    /// For each tag id: the TrackingTag registered for it (tag_id or value) and the asset its asset_registration_id
    /// points to (asset_registration_id, or basic_information.asset_tag, since Tag Management can store either).
    /// A tag with no TrackingTag falls back to an asset whose own tag fields (asset_tag, rfid_tag_id, nfc_uid,
    /// barcode) carry the tag id.
    /// </summary>
    private async Task<Dictionary<string, AssetLink>> AssetLinksAsync(IReadOnlyList<string> tagIds, CancellationToken ct)
    {
        var result = new Dictionary<string, AssetLink>(StringComparer.Ordinal);
        if (tagIds.Count == 0) return result;

        // TrackingTag: small (one row per registered tag), so read it whole and match in memory on normalized ids.
        var trackingTags = await database.GetCollection<BsonDocument>(TrackingTagCollection)
            .Find(new BsonDocument("is_deleted", new BsonDocument("$ne", true)))
            .Project(Builders<BsonDocument>.Projection
                .Include("tag_id").Include("value").Include("status")
                .Include("asset_registration_id").Include("asset_code").Include("asset_name"))
            .ToListAsync(ct);

        var tagsByKey = new Dictionary<string, List<BsonDocument>>(StringComparer.Ordinal);
        foreach (var tt in trackingTags)
        {
            foreach (var key in new[] { Normalize(Str(tt, "tag_id")), Normalize(Str(tt, "value")) }.Distinct())
            {
                if (key is null) continue;
                if (!tagsByKey.TryGetValue(key, out var list)) tagsByKey[key] = list = [];
                list.Add(tt);
            }
        }

        // Which TrackingTag each tag id resolves to.
        var resolved = new Dictionary<string, (BsonDocument? Tag, List<string> Issues)>(StringComparer.Ordinal);
        foreach (var tagId in tagIds)
        {
            var issues = new List<string>();
            BsonDocument? tag = null;
            if (tagsByKey.TryGetValue(Normalize(tagId)!, out var candidates))
            {
                // Two registrations for one physical tag: prefer one linked to an asset, report the duplicate.
                if (candidates.Count > 1) issues.Add(MatchIssue.TagMatchesMultipleTrackingTags);
                tag = candidates.FirstOrDefault(c => Str(c, "asset_registration_id") is not null) ?? candidates[0];
            }

            resolved[tagId] = (tag, issues);
        }

        // AssetRegistration: only the assets that can match, by registration id / asset tag, or by the tag id itself.
        var assetKeys = resolved.Values
            .Select(v => v.Tag is null ? null : Str(v.Tag, "asset_registration_id"))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unregisteredTagVariants = resolved.Where(kv => kv.Value.Tag is null).SelectMany(kv => Variants(kv.Key)).Distinct().ToList();

        var f = Builders<BsonDocument>.Filter;
        var anyOf = new List<FilterDefinition<BsonDocument>>();
        if (assetKeys.Count > 0)
        {
            anyOf.Add(f.In("asset_registration_id", assetKeys));
            anyOf.Add(f.In("basic_information.asset_tag", assetKeys));
        }
        if (unregisteredTagVariants.Count > 0)
        {
            anyOf.AddRange(AssetOwnTagFields.Select(field => f.In($"basic_information.{field}", unregisteredTagVariants)));
        }

        var assets = anyOf.Count == 0
            ? []
            : await database.GetCollection<BsonDocument>(AssetRegistrationCollection)
                .Find(f.Ne("is_deleted", true) & f.Or(anyOf), new FindOptions { Collation = CaseInsensitive })
                .Project(Builders<BsonDocument>.Projection
                    .Include("asset_registration_id").Include("status").Include("basic_information")
                    .Include("classification.category").Include("classification.asset_type").Include("classification.department"))
                .ToListAsync(ct);

        var byRegistrationId = Index(assets, a => [Str(a, "asset_registration_id")]);
        var byAssetTag = Index(assets, a => [Str(a, "basic_information", "asset_tag")]);
        var byOwnTag = Index(assets, a => AssetOwnTagFields.Select(field => Str(a, "basic_information", field)));

        foreach (var (tagId, (tag, issues)) in resolved)
        {
            if (tag is not null)
            {
                var trackingTagId = Str(tag, "tag_id");
                var tagStatus = Str(tag, "status");
                var assetKey = Str(tag, "asset_registration_id");
                if (assetKey is null)
                {
                    issues.Add(MatchIssue.TagNotAssigned);
                    result[tagId] = new AssetLink(trackingTagId, tagStatus, null, null, issues);
                    continue;
                }

                var matches = Lookup(byRegistrationId, assetKey);
                if (matches.Count == 0) matches = Lookup(byAssetTag, assetKey);
                if (matches.Count > 1) issues.Add(MatchIssue.AssetMatchesMultipleRegistrations);

                if (matches.Count > 0)
                {
                    result[tagId] = new AssetLink(trackingTagId, tagStatus, ToAsset(matches[0]), AssetSources.TrackingTag, issues);
                }
                else
                {
                    // The tag points to an asset that no longer exists (deleted, or a wrong id): show what the tag remembers.
                    issues.Add(MatchIssue.AssetNotFound);
                    var remembered = new TagLocationAsset(assetKey, Str(tag, "asset_code"), Str(tag, "asset_name"),
                        null, null, null, null, null, null, null, null, null);
                    result[tagId] = new AssetLink(trackingTagId, tagStatus, remembered, AssetSources.TrackingTagOnly, issues);
                }
                continue;
            }

            issues.Add(MatchIssue.TagNotRegistered);
            var own = Lookup(byOwnTag, tagId);
            if (own.Count > 1) issues.Add(MatchIssue.AssetMatchesMultipleRegistrations);
            result[tagId] = own.Count > 0
                ? new AssetLink(null, null, ToAsset(own[0]), AssetSources.AssetRegistration, issues)
                : new AssetLink(null, null, null, null, issues);
        }

        return result;
    }

    private static TagLocationAsset ToAsset(BsonDocument a) => new(
        Str(a, "asset_registration_id")!,
        Str(a, "basic_information", "asset_tag"),
        Str(a, "basic_information", "asset_name"),
        Str(a, "classification", "category"),
        Str(a, "classification", "asset_type"),
        Str(a, "classification", "department"),
        Str(a, "basic_information", "status") ?? Str(a, "status"),
        Str(a, "basic_information", "condition"),
        Str(a, "basic_information", "criticality"),
        Str(a, "basic_information", "manufacturer"),
        Str(a, "basic_information", "model"),
        Str(a, "basic_information", "serial_number"));

    private static Dictionary<string, List<BsonDocument>> Index(IEnumerable<BsonDocument> docs, Func<BsonDocument, IEnumerable<string?>> keys)
    {
        var index = new Dictionary<string, List<BsonDocument>>(StringComparer.Ordinal);
        foreach (var doc in docs)
        {
            foreach (var key in keys(doc).Select(Normalize).OfType<string>().Distinct())
            {
                if (!index.TryGetValue(key, out var list)) index[key] = list = [];
                list.Add(doc);
            }
        }
        return index;
    }

    private static List<BsonDocument> Lookup(Dictionary<string, List<BsonDocument>> index, string key) =>
        Normalize(key) is { } k && index.TryGetValue(k, out var list) ? list : [];

    /// <summary>Lower-case, without ':', '-' and spaces, so a MAC matches however it was typed. Null when blank.</summary>
    public static string? Normalize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var chars = id.Where(c => c is not (':' or '-' or ' ')).Select(char.ToLowerInvariant).ToArray();
        return chars.Length == 0 ? null : new string(chars);
    }

    /// <summary>Spellings a tag id may be stored under on an asset: as read, and as a colon MAC (AC:23:3F:12:AB:01).</summary>
    private static IEnumerable<string> Variants(string tagId)
    {
        yield return tagId;
        var plain = Normalize(tagId)!;
        if (plain.Length == 12 && plain.All(Uri.IsHexDigit))
        {
            yield return string.Join(':', Enumerable.Range(0, 6).Select(i => plain.Substring(i * 2, 2)));
        }
    }

    private static string? Str(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var v) && v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString.Trim() : null;

    private static string? Str(BsonDocument doc, string parent, string field) =>
        doc.TryGetValue(parent, out var p) && p.IsBsonDocument ? Str(p.AsBsonDocument, field) : null;

    private static double? Number(BsonValue value) => value.IsNumeric ? value.ToDouble() : null;

    private sealed record LatestReading(string TagId, string GatewayId, DateTime Timestamp, string? Status, double? Rssi, double? Battery);

    private sealed record GatewayPlot(
        string DeviceId, string? GatewayName, string? ZoneId, string? ZoneName, string? ZoneCode, string? FloorPlanId, string? FloorName,
        string? BuildingName, string? SiteId, string? SiteName, string? LocationPath, double? X, double? Y, int PlotCount);

    private sealed record AssetLink(string? TrackingTagId, string? TagStatus, TagLocationAsset? Asset, string? AssetSource, List<string> Issues);
}

/// <summary>Where <see cref="TagLocation.Asset"/> came from.</summary>
public static class AssetSources
{
    /// <summary>TrackingTag (tag_id / value) → asset_registration_id → AssetRegistration. The normal path.</summary>
    public const string TrackingTag = "TrackingTag";

    /// <summary>The TrackingTag names an asset that is not in AssetRegistration; only its id / code / name are known.</summary>
    public const string TrackingTagOnly = "TrackingTagOnly";

    /// <summary>No TrackingTag; an asset's own tag field (asset_tag, rfid_tag_id, nfc_uid, barcode) carries the tag id.</summary>
    public const string AssetRegistration = "AssetRegistration";
}

/// <summary>Codes in <see cref="TagLocation.MatchIssues"/>: a link that could not be made, or was ambiguous.</summary>
public static class MatchIssue
{
    /// <summary>No TrackingTag has this tag id as its tag_id or value.</summary>
    public const string TagNotRegistered = "tag-not-registered";

    /// <summary>More than one TrackingTag has this tag id; the one linked to an asset was used.</summary>
    public const string TagMatchesMultipleTrackingTags = "tag-matches-multiple-tracking-tags";

    /// <summary>The TrackingTag has no asset_registration_id (in stock / Available).</summary>
    public const string TagNotAssigned = "tag-not-assigned";

    /// <summary>The TrackingTag's asset_registration_id matches no (non-deleted) AssetRegistration.</summary>
    public const string AssetNotFound = "asset-not-found";

    /// <summary>More than one asset matched; the first was used.</summary>
    public const string AssetMatchesMultipleRegistrations = "asset-matches-multiple-registrations";

    /// <summary>No device plot in maps_plots has this gateway id as device_id (or item_id): zone / floor unknown.</summary>
    public const string GatewayNotPlotted = "gateway-not-plotted";

    /// <summary>The gateway is plotted more than once; the first plot's zone was used.</summary>
    public const string GatewayPlottedMultipleTimes = "gateway-plotted-multiple-times";

    /// <summary>The gateway's plot has no zone id or zone name.</summary>
    public const string GatewayPlotHasNoZone = "gateway-plot-has-no-zone";
}

/// <summary>One message of the tag-locations feed.</summary>
/// <param name="Total">Tags in the list.</param>
/// <param name="Online">Of those, Online.</param>
/// <param name="Plotted">Of those, heard by a gateway that is plotted on a floor plan (so the zone is known).</param>
/// <param name="WithAsset">Of those, linked to an asset.</param>
public sealed record TagLocationSnapshot(int Total, int Online, int Plotted, int WithAsset, IReadOnlyList<TagLocation> Tags);

/// <summary>A tag's newest reading, where its gateway is plotted and the asset it is attached to.</summary>
/// <param name="TagId">Lower-case, as stored in NetworkTimeSeries (meta.tagid).</param>
/// <param name="GatewayId">Lower-case gateway id of the newest reading (meta.gatewayid).</param>
/// <param name="DeviceId">device_id of the matching maps_plots device (as stored); null when the gateway is not plotted.</param>
/// <param name="GatewayName">Name of the gateway's plot; null when the gateway is not plotted.</param>
/// <param name="Timestamp">Time of the newest reading (UTC).</param>
/// <param name="Status">Status on the newest reading, as stored (e.g. "online").</param>
/// <param name="Connection">Online, Idle (no reading for 10 min) or Offline (status not online).</param>
/// <param name="Plotted">The gateway matches a device plot in maps_plots; the zone / floor fields are null otherwise.</param>
/// <param name="X">Gateway position on the floor plan, percent of the plan (0-100).</param>
/// <param name="TrackingTagId">tag_id of the matching TrackingTag (e.g. BLE-002901); null when the tag is not registered.</param>
/// <param name="TagStatus">Status of that TrackingTag: Available, Assigned or Inactive.</param>
/// <param name="Asset">The asset the tag is attached to; null when none could be found.</param>
/// <param name="AssetSource">How <paramref name="Asset"/> was found, see <see cref="AssetSources"/>.</param>
/// <param name="MatchIssues">Links that failed or were ambiguous, see <see cref="MatchIssue"/>; empty when all matched.</param>
public sealed record TagLocation(
    string TagId,
    string GatewayId,
    string? DeviceId,
    string? GatewayName,
    DateTime Timestamp,
    string? Status,
    string Connection,
    double? Rssi,
    double? Battery,
    bool Plotted,
    string? ZoneId,
    string? ZoneName,
    string? ZoneCode,
    string? FloorPlanId,
    string? FloorName,
    string? BuildingName,
    string? SiteId,
    string? SiteName,
    string? LocationPath,
    double? X,
    double? Y,
    string? TrackingTagId,
    string? TagStatus,
    TagLocationAsset? Asset,
    string? AssetSource,
    IReadOnlyList<string> MatchIssues);

/// <summary>Basic information of the asset a tag is attached to (AssetRegistration).</summary>
public sealed record TagLocationAsset(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? Category,
    string? AssetType,
    string? Department,
    string? Status,
    string? Condition,
    string? Criticality,
    string? Manufacturer,
    string? Model,
    string? SerialNumber);
