using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.ReportAnalytics.Repository.Repositories;

/// <summary>Which readings the Asset Movement report covers. Null bounds and filters mean no limit.</summary>
public sealed record AssetMovementQuery(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    string? TenantId,
    string? ClientId,
    string? AssetId,
    string? Category,
    string? SubCategory,
    int MaxRecords);

/// <summary>
/// One stay of an asset in one zone (or with one gateway that is not plotted in a zone): the first reading there is the zone
/// in, the last one the zone out.
/// </summary>
public sealed record AssetZoneVisit(
    string AssetId,
    string? AssetName,
    string? Category,
    string? SubCategory,
    IReadOnlyList<string> TagIds,
    string? ZoneName,
    IReadOnlyList<string> GatewayIds,
    DateTime ZoneInUtc,
    DateTime ZoneOutUtc,
    int ReadingCount);

public sealed record AssetMovementResult(IReadOnlyList<AssetZoneVisit> Visits, bool Truncated);

public interface IAssetMovementReportRepository
{
    Task<AssetMovementResult> GetAsync(AssetMovementQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Assets are found through their tag mappings: Tag Management (TrackingTag.asset_registration_id, matched on tag_id or value)
/// and the asset's own tag (AssetRegistration.basic_information.asset_tag). Their NetworkTimeSeries readings (meta.tagid) are
/// placed in zones through the gateway that heard them (meta.gatewayid = maps_plots.device_id) and folded into zone visits.
/// Nothing is copied; every collection is read live.
/// </summary>
public sealed class AssetMovementReportRepository : IAssetMovementReportRepository
{
    private const string AssetRegistrationCollection = "AssetRegistration";
    private const string TrackingTagCollection = "TrackingTag";
    private const string NetworkTimeSeriesCollection = "NetworkTimeSeries";
    private const string MapPlotCollection = "maps_plots";

    /// <summary>No reading in the same zone for longer than this ends the visit (the asset left and came back).</summary>
    private static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(10);

    // Tags and device ids are matched ignoring case (the network server writes lower case, plots hold upper case).
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    private readonly IMongoDatabase _database;

    public AssetMovementReportRepository(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<AssetMovementResult> GetAsync(AssetMovementQuery query, CancellationToken cancellationToken = default)
    {
        var assetsByKey = await GetAssetsByTagKeyAsync(query, cancellationToken);
        if (assetsByKey.Count == 0)
        {
            return new AssetMovementResult([], false);
        }

        var buckets = await GetBucketsAsync(assetsByKey.Keys.ToList(), query, cancellationToken);
        var truncated = buckets.Count > query.MaxRecords;
        if (truncated)
        {
            buckets.RemoveRange(query.MaxRecords, buckets.Count - query.MaxRecords);
        }

        var zones = await GetZonesByDeviceAsync(
            buckets.Select(bucket => bucket.GatewayId).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            cancellationToken);

        var visits = new List<AssetZoneVisit>();
        var perAsset = buckets
            .Where(bucket => assetsByKey.ContainsKey(bucket.TagKey))
            .GroupBy(bucket => assetsByKey[bucket.TagKey].Asset.AssetId, StringComparer.OrdinalIgnoreCase);

        foreach (var readings in perAsset)
        {
            var asset = assetsByKey[readings.First().TagKey].Asset;
            Visit? open = null;
            foreach (var reading in readings.OrderBy(bucket => bucket.From).ThenBy(bucket => bucket.GatewayId))
            {
                var zone = zones.GetValueOrDefault(reading.GatewayId);
                var place = zone is null ? $"gw:{reading.GatewayId}" : $"zone:{zone}";
                var tagId = assetsByKey[reading.TagKey].TagId;

                if (open is not null
                    && string.Equals(open.Place, place, StringComparison.OrdinalIgnoreCase)
                    && reading.From - open.Out <= MaxGap)
                {
                    open.Add(reading, tagId);
                    continue;
                }

                if (open is not null)
                {
                    visits.Add(open.ToVisit(asset));
                }

                open = new Visit(place, zone, reading, tagId);
            }

            if (open is not null)
            {
                visits.Add(open.ToVisit(asset));
            }
        }

        return new AssetMovementResult(
            visits.OrderBy(visit => visit.ZoneInUtc).ThenBy(visit => visit.AssetId, StringComparer.OrdinalIgnoreCase).ToList(),
            truncated);
    }

    /// <summary>Every id a reading may carry for a mapped tag (lower case, also without ':' / '-') → the tag and its asset.</summary>
    private async Task<Dictionary<string, (string TagId, Asset Asset)>> GetAssetsByTagKeyAsync(
        AssetMovementQuery query,
        CancellationToken cancellationToken)
    {
        var assets = await GetAssetsAsync(query, cancellationToken);
        var result = new Dictionary<string, (string TagId, Asset Asset)>(StringComparer.OrdinalIgnoreCase);
        if (assets.Count == 0)
        {
            return result;
        }

        // A mapping may name the asset by its registration id (AR000001), its asset tag or its _id: all resolve to the asset.
        var byReference = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            byReference.TryAdd(asset.AssetId, asset);
            byReference.TryAdd(asset.MongoId, asset);
            if (asset.AssetTag is not null)
            {
                byReference.TryAdd(asset.AssetTag, asset);
            }
        }

        var mappings = await _database.GetCollection<BsonDocument>(TrackingTagCollection)
            .Find(new BsonDocument
            {
                { "is_deleted", new BsonDocument("$ne", true) },
                { "asset_registration_id", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) }
            })
            .Project(new BsonDocument { { "tag_id", 1 }, { "value", 1 }, { "asset_registration_id", 1 } })
            .ToListAsync(cancellationToken);

        foreach (var mapping in mappings)
        {
            var tagId = Text(mapping, "tag_id");
            var reference = Text(mapping, "asset_registration_id");
            if (tagId is null || reference is null || !byReference.TryGetValue(reference, out var asset))
            {
                continue;
            }

            foreach (var key in Keys(tagId, Text(mapping, "value")))
            {
                result.TryAdd(key, (tagId, asset));
            }
        }

        // The tag written on the asset itself, when Tag Management has no mapping for it.
        foreach (var asset in assets.Where(asset => asset.AssetTag is not null))
        {
            foreach (var key in Keys(asset.AssetTag!, null))
            {
                result.TryAdd(key, (asset.AssetTag!, asset));
            }
        }

        return result;
    }

    private async Task<List<Asset>> GetAssetsAsync(AssetMovementQuery query, CancellationToken cancellationToken)
    {
        var match = new BsonDocument
        {
            { "is_deleted", new BsonDocument("$ne", true) },
            { "asset_registration_id", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) }
        };

        if (!string.IsNullOrWhiteSpace(query.TenantId))
        {
            match["tenant_id"] = query.TenantId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.ClientId))
        {
            match["client_id"] = query.ClientId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            match["classification.category"] = query.Category.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.SubCategory))
        {
            match["classification.sub_category"] = query.SubCategory.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.AssetId))
        {
            // The registration id (AR000001), the asset tag, or the Mongo _id.
            var value = query.AssetId.Trim();
            var anyOf = new BsonArray
            {
                new BsonDocument("asset_registration_id", value),
                new BsonDocument("basic_information.asset_tag", value)
            };

            if (ObjectId.TryParse(value, out var objectId))
            {
                anyOf.Add(new BsonDocument("_id", objectId));
            }

            match["$or"] = anyOf;
        }

        var rows = await _database.GetCollection<BsonDocument>(AssetRegistrationCollection)
            .Find(match, new FindOptions { Collation = CaseInsensitive })
            .Project(new BsonDocument
            {
                { "asset_registration_id", 1 },
                { "basic_information.asset_name", 1 },
                { "basic_information.asset_tag", 1 },
                { "classification.category", 1 },
                { "classification.sub_category", 1 }
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var basic = Sub(row, "basic_information");
                var classification = Sub(row, "classification");
                return new Asset(
                    row["_id"].ToString()!,
                    Text(row, "asset_registration_id")!,
                    Text(basic, "asset_tag"),
                    Text(basic, "asset_name"),
                    Text(classification, "category"),
                    Text(classification, "sub_category"));
            })
            .Where(asset => asset.AssetId is not null)
            .ToList();
    }

    /// <summary>Readings of the tags in the period, grouped per tag, gateway and minute; one more than the cap when cut short.</summary>
    private async Task<List<Bucket>> GetBucketsAsync(List<string> tagKeys, AssetMovementQuery query, CancellationToken cancellationToken)
    {
        var match = new BsonDocument
        {
            { "meta.tagid", new BsonDocument("$in", new BsonArray(tagKeys)) },
            { "meta.gatewayid", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) }
        };

        var time = new BsonDocument();
        if (query.FromUtc is not null)
        {
            time["$gte"] = query.FromUtc.Value;
        }

        if (query.ToUtcExclusive is not null)
        {
            time["$lt"] = query.ToUtcExclusive.Value;
        }

        if (time.ElementCount > 0)
        {
            match["timestamp"] = time;
        }

        var pipeline = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "tag", new BsonDocument("$toLower", "$meta.tagid") },
                        { "gw", new BsonDocument("$toLower", "$meta.gatewayid") },
                        { "slot", new BsonDocument("$dateTrunc", new BsonDocument { { "date", "$timestamp" }, { "unit", "minute" } }) }
                    } },
                { "from", new BsonDocument("$min", "$timestamp") },
                { "to", new BsonDocument("$max", "$timestamp") },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument { { "from", 1 }, { "_id.gw", 1 } }),
            new BsonDocument("$limit", query.MaxRecords + 1)
        };

        var rows = await _database.GetCollection<BsonDocument>(NetworkTimeSeriesCollection)
            .Aggregate<BsonDocument>(pipeline, new AggregateOptions { AllowDiskUse = true, Collation = CaseInsensitive }, cancellationToken)
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => row["_id"]["tag"].IsString && row["_id"]["gw"].IsString)
            .Select(row => new Bucket(
                row["_id"]["tag"].AsString,
                row["_id"]["gw"].AsString,
                row["from"].ToUniversalTime(),
                row["to"].ToUniversalTime(),
                row["count"].ToInt32()))
            .ToList();
    }

    /// <summary>Zone name of each plotted gateway (maps_plots by device_id, or item_id of a device plot), latest plot first.</summary>
    private async Task<Dictionary<string, string>> GetZonesByDeviceAsync(
        IReadOnlyCollection<string> deviceIds,
        CancellationToken cancellationToken)
    {
        var zones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (deviceIds.Count == 0)
        {
            return zones;
        }

        var ids = new BsonArray(deviceIds);
        var plots = await _database.GetCollection<BsonDocument>(MapPlotCollection)
            .Find(
                new BsonDocument
                {
                    { "is_deleted", new BsonDocument("$ne", true) },
                    { "$or", new BsonArray
                        {
                            new BsonDocument("device_id", new BsonDocument("$in", ids)),
                            new BsonDocument { { "kind", "device" }, { "item_id", new BsonDocument("$in", ids) } }
                        } }
                },
                new FindOptions { Collation = CaseInsensitive })
            .Project(new BsonDocument { { "device_id", 1 }, { "item_id", 1 }, { "zone_id", 1 }, { "zone_name", 1 }, { "location", 1 } })
            .Sort(new BsonDocument { { "updated_at", -1 }, { "created_at", -1 }, { "_id", -1 } })
            .ToListAsync(cancellationToken);

        var zoneNames = await A360.Repository.Repositories.ZoneNames.GetAsync(_database, cancellationToken);
        foreach (var plot in plots)
        {
            var deviceId = Text(plot, "device_id") ?? Text(plot, "item_id");
            // Current Master Data name by zone id; the stored copy only as a fallback.
            var zone = zoneNames.Resolve(Text(plot, "zone_id"), Text(plot, "zone_name") ?? Text(Sub(plot, "location"), "zone_name"));
            if (deviceId is not null && zone is not null)
            {
                zones.TryAdd(deviceId, zone);
            }
        }

        return zones;
    }

    // "C3:00:00:99:13:8F" is written by the network server as "c3000099138f".
    private static IEnumerable<string> Keys(string tagId, string? value)
    {
        foreach (var id in new[] { tagId, value })
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            yield return id.Trim().ToLowerInvariant();
            yield return id.Replace(":", "").Replace("-", "").Replace(" ", "").Trim().ToLowerInvariant();
        }
    }

    private static BsonDocument? Sub(BsonDocument? document, string name) =>
        document is not null && document.TryGetValue(name, out var value) && value.IsBsonDocument ? value.AsBsonDocument : null;

    private static string? Text(BsonDocument? document, string name)
    {
        if (document is null || !document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            return null;
        }

        var text = value.ToString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private sealed record Asset(string MongoId, string AssetId, string? AssetTag, string? Name, string? Category, string? SubCategory);

    private sealed record Bucket(string TagKey, string GatewayId, DateTime From, DateTime To, int Count);

    private sealed class Visit(string place, string? zone, Bucket first, string tagId)
    {
        private readonly List<string> _tags = [tagId];
        private readonly List<string> _gateways = [first.GatewayId];
        private int _readings = first.Count;

        public string Place { get; } = place;
        public DateTime Out { get; private set; } = first.To;

        public void Add(Bucket reading, string tagId)
        {
            if (!_tags.Contains(tagId, StringComparer.OrdinalIgnoreCase)) _tags.Add(tagId);
            if (!_gateways.Contains(reading.GatewayId, StringComparer.OrdinalIgnoreCase)) _gateways.Add(reading.GatewayId);
            if (reading.To > Out) Out = reading.To;
            _readings += reading.Count;
        }

        public AssetZoneVisit ToVisit(Asset asset) =>
            new(asset.AssetId, asset.Name, asset.Category, asset.SubCategory, _tags, zone, _gateways, first.From, Out, _readings);
    }
}
