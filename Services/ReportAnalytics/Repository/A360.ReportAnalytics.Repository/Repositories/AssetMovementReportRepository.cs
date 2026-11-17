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

/// <summary>One tag reading joined to its registered asset and to the zone of the gateway that heard it.</summary>
public sealed record AssetMovementRecord(
    DateTime Timestamp,
    string TagId,
    string? AssetMongoId,
    string? AssetRegistrationId,
    string? AssetName,
    string? Category,
    string? SubCategory,
    string? DeviceId,
    string? ZoneName,
    string? ZoneCode);

public sealed record AssetMovementResult(IReadOnlyList<AssetMovementRecord> Records, bool Truncated);

public interface IAssetMovementReportRepository
{
    Task<AssetMovementResult> GetAsync(AssetMovementQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads NetworkTimeSeries (written by the network server), matches meta.tagid to AssetRegistration.basic_information.asset_tag
/// and meta.gatewayid to maps_plots.device_id. Nothing is copied; all three collections are read live.
/// </summary>
public sealed class AssetMovementReportRepository : IAssetMovementReportRepository
{
    private const string AssetRegistrationCollection = "AssetRegistration";
    private const string NetworkTimeSeriesCollection = "NetworkTimeSeries";
    private const string MapPlotCollection = "maps_plots";

    // Tags and device ids are matched ignoring case (the network server writes lower case, plots hold upper case).
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    private readonly IMongoDatabase _database;

    public AssetMovementReportRepository(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<AssetMovementResult> GetAsync(AssetMovementQuery query, CancellationToken cancellationToken = default)
    {
        var assets = await GetAssetsByTagAsync(query, cancellationToken);
        if (assets.Count == 0)
        {
            return new AssetMovementResult([], false);
        }

        var filter = new BsonDocument("meta.tagid", new BsonDocument("$in", new BsonArray(assets.Keys)));
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
            filter["timestamp"] = time;
        }

        // One more than the cap tells us whether the report was cut short.
        var readings = await _database.GetCollection<BsonDocument>(NetworkTimeSeriesCollection)
            .Find(filter, new FindOptions { Collation = CaseInsensitive })
            .Project(new BsonDocument { { "timestamp", 1 }, { "meta", 1 } })
            .Sort(new BsonDocument { { "timestamp", 1 }, { "_id", 1 } })
            .Limit(query.MaxRecords + 1)
            .ToListAsync(cancellationToken);

        var truncated = readings.Count > query.MaxRecords;
        if (truncated)
        {
            readings.RemoveAt(readings.Count - 1);
        }

        var gateways = readings
            .Select(reading => Text(Meta(reading), "gatewayid"))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var zones = await GetZonesByDeviceAsync(gateways, cancellationToken);

        var records = new List<AssetMovementRecord>(readings.Count);
        foreach (var reading in readings)
        {
            var meta = Meta(reading);
            var tagId = Text(meta, "tagid");
            if (tagId is null || !reading.TryGetValue("timestamp", out var timestamp) || !timestamp.IsValidDateTime
                || !assets.TryGetValue(tagId, out var asset))
            {
                continue;
            }

            var deviceId = Text(meta, "gatewayid");
            var zone = deviceId is not null && zones.TryGetValue(deviceId, out var plotted) ? plotted : default;

            records.Add(new AssetMovementRecord(
                timestamp.ToUniversalTime(),
                tagId,
                asset.MongoId,
                asset.RegistrationId,
                asset.Name,
                asset.Category,
                asset.SubCategory,
                deviceId,
                zone.Name,
                zone.Code));
        }

        return new AssetMovementResult(records, truncated);
    }

    private async Task<Dictionary<string, TaggedAsset>> GetAssetsByTagAsync(AssetMovementQuery query, CancellationToken cancellationToken)
    {
        var match = new BsonDocument
        {
            { "is_deleted", new BsonDocument("$ne", true) },
            { "basic_information.asset_tag", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) }
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
            // The registration id (AR000001), the tag, or the Mongo _id.
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

        var assets = new Dictionary<string, TaggedAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var basic = Sub(row, "basic_information");
            var tag = Text(basic, "asset_tag");
            if (tag is null)
            {
                continue;
            }

            var classification = Sub(row, "classification");
            assets.TryAdd(tag, new TaggedAsset(
                row["_id"].ToString(),
                Text(row, "asset_registration_id"),
                Text(basic, "asset_name"),
                Text(classification, "category"),
                Text(classification, "sub_category")));
        }

        return assets;
    }

    private async Task<Dictionary<string, (string? Name, string? Code)>> GetZonesByDeviceAsync(
        IReadOnlyCollection<string> deviceIds,
        CancellationToken cancellationToken)
    {
        var zones = new Dictionary<string, (string? Name, string? Code)>(StringComparer.OrdinalIgnoreCase);
        if (deviceIds.Count == 0)
        {
            return zones;
        }

        var plots = await _database.GetCollection<BsonDocument>(MapPlotCollection)
            .Find(
                new BsonDocument
                {
                    { "is_deleted", new BsonDocument("$ne", true) },
                    { "device_id", new BsonDocument("$in", new BsonArray(deviceIds)) }
                },
                new FindOptions { Collation = CaseInsensitive })
            .Project(new BsonDocument { { "device_id", 1 }, { "zone_name", 1 }, { "zone_code", 1 }, { "location", 1 } })
            // A device plotted more than once: the latest plot wins.
            .Sort(new BsonDocument { { "updated_at", -1 }, { "created_at", -1 }, { "_id", -1 } })
            .ToListAsync(cancellationToken);

        foreach (var plot in plots)
        {
            var deviceId = Text(plot, "device_id");
            if (deviceId is null || zones.ContainsKey(deviceId))
            {
                continue;
            }

            // The drawn zone first, else the Master Data zone the plot sits in.
            var location = Sub(plot, "location");
            zones[deviceId] = (
                Text(plot, "zone_name") ?? Text(location, "zone_name"),
                Text(plot, "zone_code"));
        }

        return zones;
    }

    private static BsonDocument? Meta(BsonDocument reading) => Sub(reading, "meta");

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

    private sealed record TaggedAsset(string MongoId, string? RegistrationId, string? Name, string? Category, string? SubCategory);
}
