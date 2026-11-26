using A360.Repository.Repositories;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Dashboard.Repository.Repositories;

// Reads device status readings written by the network server; the data is maintained outside this service.
public sealed class NetworkStatusRepository : INetworkStatusRepository
{
    public const string CollectionName = "NetworkTimeSeries";

    // Assets link to a tag through mappeduniqueid; read here only to show the asset for each tag.
    private const string AssetsCollectionName = "assets";

    // Registered assets (Asset service) and tracking tags (Tracking service), read to resolve each tag's registration id.
    private const string AssetRegistrationCollectionName = "AssetRegistration";
    private const string TrackingTagCollectionName = "TrackingTag";

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    /// <summary>Assets per gateway: a tag counts at a gateway when its newest reading in this many hours came through it.</summary>
    public const int GatewayWindowHours = 24;

    private const string Online = "online";
    private const string Offline = "offline";

    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly IMongoDatabase _database;

    private readonly ResolvedLocationReader _resolved;

    public NetworkStatusRepository(IMongoDatabase database, IConfiguration configuration)
    {
        _database = database;
        _resolved = new ResolvedLocationReader(database, configuration);
        _collection = database.GetCollection<BsonDocument>(CollectionName);
    }

    public async Task<NetworkStatusCounts> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        // Each tag reports many readings; count every tag once using its latest status.
        // Status is lower-cased so "Online"/"ONLINE" are counted together.
        var results = await _collection
            .Aggregate()
            .Match(Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Type("status", BsonType.String),
                Builders<BsonDocument>.Filter.Exists("meta.tagid"),
                Builders<BsonDocument>.Filter.Ne("device_trigger", "out")))
            .Sort(Builders<BsonDocument>.Sort.Descending("timestamp"))
            .Group(new BsonDocument
            {
                { "_id", "$meta.tagid" },
                { "status", new BsonDocument("$first", new BsonDocument("$toLower", "$status")) }
            })
            .Group(new BsonDocument
            {
                { "_id", "$status" },
                { "count", new BsonDocument("$sum", 1) }
            })
            .ToListAsync(cancellationToken);

        var counts = results.ToDictionary(
            result => result["_id"].AsString,
            result => result["count"].ToInt64());

        return new NetworkStatusCounts(
            counts.GetValueOrDefault(Online),
            counts.GetValueOrDefault(Offline));
    }

    public async Task<IReadOnlyCollection<TagRecordCount>> GetTagRecordCountsAsync(CancellationToken cancellationToken = default)
    {
        var results = await _collection
            .Aggregate()
            .Match(Builders<BsonDocument>.Filter.Exists("meta.tagid") & Builders<BsonDocument>.Filter.Ne("device_trigger", "out"))
            .Group(new BsonDocument
            {
                { "_id", "$meta.tagid" },
                { "count", new BsonDocument("$sum", 1) }
            })
            .Sort(new BsonDocument { { "count", -1 }, { "_id", 1 } })
            .Lookup(AssetsCollectionName, "_id", "mappeduniqueid", "assets")
            .ToListAsync(cancellationToken);

        var rows = results
            .Select(result =>
            {
                var asset = result["assets"].AsBsonArray.Select(value => value.AsBsonDocument).FirstOrDefault();
                return (TagId: result["_id"].ToString()!, Count: result["count"].ToInt64(),
                    AssetId: asset.GetStringOrNull("assetid"), AssetName: asset.GetStringOrNull("assetname"));
            })
            .ToList();

        var registrations = await ResolveRegistrationsAsync(rows.Select(row => (row.TagId, row.AssetId)).ToList(), cancellationToken);

        return rows
            .Select(row =>
            {
                registrations.TryGetValue(row.TagId, out var registration);
                return new TagRecordCount(
                    row.TagId,
                    row.Count,
                    row.AssetId,
                    row.AssetName ?? registration.AssetName,
                    registration.AssetRegistrationId);
            })
            .ToList();
    }

    /// <summary>
    /// Finds the registered asset of each tag: the Tracking service's tag assignment first, then an asset whose RFID /
    /// NFC / barcode / tag is the tag id, then the legacy mapping's asset id read as a registration id or asset tag.
    /// </summary>
    private async Task<Dictionary<string, (string? AssetRegistrationId, string? AssetName)>> ResolveRegistrationsAsync(
        IReadOnlyCollection<(string TagId, string? LegacyAssetId)> tags,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, (string?, string?)>(StringComparer.OrdinalIgnoreCase);
        if (tags.Count == 0)
        {
            return resolved;
        }

        var filter = Builders<BsonDocument>.Filter;
        var tagIds = new BsonArray(tags.Select(tag => tag.TagId));
        var notDeleted = filter.Ne("is_deleted", true);
        var options = new FindOptions { Collation = CaseInsensitive };

        var trackingTags = await _database.GetCollection<BsonDocument>(TrackingTagCollectionName)
            .Find(notDeleted & filter.In("tag_id", tagIds) & filter.Type("asset_registration_id", BsonType.String), options)
            .ToListAsync(cancellationToken);

        foreach (var tag in trackingTags)
        {
            resolved.TryAdd(tag.GetStringOrNull("tag_id")!, (tag.GetStringOrNull("asset_registration_id"), tag.GetStringOrNull("asset_name")));
        }

        var legacyIds = new BsonArray(tags.Select(tag => tag.LegacyAssetId).Where(id => !string.IsNullOrWhiteSpace(id)));
        var assets = await _database.GetCollection<BsonDocument>(AssetRegistrationCollectionName)
            .Find(notDeleted & filter.Or(
                filter.In("basic_information.rfid_tag_id", tagIds),
                filter.In("basic_information.nfc_uid", tagIds),
                filter.In("basic_information.barcode", tagIds),
                filter.In("basic_information.asset_tag", tagIds),
                filter.In("asset_registration_id", legacyIds),
                filter.In("basic_information.asset_tag", legacyIds)), options)
            .Project(Builders<BsonDocument>.Projection
                .Include("asset_registration_id")
                .Include("basic_information"))
            .ToListAsync(cancellationToken);

        foreach (var (tagId, legacyId) in tags.Where(tag => !resolved.ContainsKey(tag.TagId)))
        {
            var asset = assets.FirstOrDefault(candidate => Has(candidate, tagId, "rfid_tag_id", "nfc_uid", "barcode", "asset_tag"))
                ?? (legacyId is null ? null : assets.FirstOrDefault(candidate =>
                    string.Equals(candidate.GetStringOrNull("asset_registration_id"), legacyId, StringComparison.OrdinalIgnoreCase)
                    || Has(candidate, legacyId, "asset_tag")));

            if (asset is not null)
            {
                var basic = asset.TryGetValue("basic_information", out var value) && value.IsBsonDocument ? value.AsBsonDocument : null;
                resolved[tagId] = (asset.GetStringOrNull("asset_registration_id"), basic.GetStringOrNull("asset_name"));
            }
        }

        return resolved;
    }

    public async Task<AssetTagTelemetry?> GetAssetTagTelemetryAsync(string assetRegistrationIdOrTag, CancellationToken cancellationToken = default)
    {
        var id = assetRegistrationIdOrTag.Trim();
        var filter = Builders<BsonDocument>.Filter;
        var notDeleted = filter.Ne("is_deleted", true);
        var options = new FindOptions { Collation = CaseInsensitive };

        var asset = await _database.GetCollection<BsonDocument>(AssetRegistrationCollectionName)
            .Find(notDeleted & filter.Or(filter.Eq("asset_registration_id", id), filter.Eq("basic_information.asset_tag", id)), options)
            .Project(Builders<BsonDocument>.Projection.Include("asset_registration_id").Include("basic_information"))
            .FirstOrDefaultAsync(cancellationToken);
        var basic = asset is not null && asset.TryGetValue("basic_information", out var info) && info.IsBsonDocument ? info.AsBsonDocument : null;
        var registrationId = asset.GetStringOrNull("asset_registration_id") ?? id;

        // Tags assigned to the asset in Tag Management, then the asset's own identifiers that may report as a tag.
        // Tag Management stores the asset's tag (e.g. TB123) in asset_registration_id, older records the registration id.
        var assetKeys = new BsonArray(new[] { registrationId, basic.GetStringOrNull("asset_tag"), id }
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        var assigned = (await _database.GetCollection<BsonDocument>(TrackingTagCollectionName)
                .Find(notDeleted & filter.In("asset_registration_id", assetKeys), options)
                .Project(Builders<BsonDocument>.Projection.Include("tag_id"))
                .ToListAsync(cancellationToken))
            .Select(tag => tag.GetStringOrNull("tag_id")?.Trim())
            .OfType<string>()
            .Where(tagId => tagId.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var own = new[] { "rfid_tag_id", "nfc_uid", "barcode", "asset_tag" }
            .Select(field => basic.GetStringOrNull(field)?.Trim())
            .OfType<string>()
            .Where(value => value.Length > 0 && !assigned.Contains(value, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var readings = await LatestReadingsAsync(assigned.Concat(own).ToList(), cancellationToken);

        // Assigned tags are always listed (even before their first reading); own identifiers only once they report.
        var tags = assigned
            .Select(tagId => Reading(tagId, isAssigned: true))
            .Concat(own.Where(value => readings.ContainsKey(value)).Select(value => Reading(value, isAssigned: false)))
            .ToList();

        if (asset is null && tags.Count == 0)
        {
            return null;
        }

        return new AssetTagTelemetry(registrationId, basic.GetStringOrNull("asset_tag"), basic.GetStringOrNull("asset_name"), tags);

        TagLatestReading Reading(string tagId, bool isAssigned) =>
            readings.TryGetValue(tagId, out var reading)
                ? reading with { TagId = tagId, IsAssigned = isAssigned }
                : new TagLatestReading(tagId, isAssigned, null, null, null, null, null);
    }

    public async Task<IReadOnlyList<GatewayTags>> GetGatewayTagsAsync(CancellationToken cancellationToken = default)
    {
        // One shared device-status rule (DeviceStatusReader): last seen = newest reading of the device itself (a repeater also
        // counts a gateway hearing its beacon), Online <= 2 min, Idle <= 10 min, Offline after; assets in range = distinct tags
        // the device heard in the last 2 minutes. Plotted devices are listed even when they heard nothing.
        var reader = new DeviceStatusReader(_database);
        var status = await reader.GetAsync(null, cancellationToken);
        var plotted = (await _database.GetCollection<BsonDocument>("maps_plots")
                .Find(new BsonDocument { { "kind", "device" }, { "is_deleted", new BsonDocument("$ne", true) } })
                .Project(Builders<BsonDocument>.Projection.Include("device_id").Include("item_id"))
                .ToListAsync(cancellationToken))
            .Select(plot => plot.GetStringOrNull("device_id") ?? plot.GetStringOrNull("item_id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => DeviceStatusReader.Normalize(id!))
            .Distinct()
            .Where(id => !status.ContainsKey(id))
            .ToList();
        foreach (var (id, device) in await reader.GetAsync(plotted, cancellationToken))
        {
            status[id] = device;
        }

        var tagIds = status.Values.SelectMany(device => device.TagIds).Distinct().ToList();
        var assets = await ResolveRegistrationsAsync(tagIds.Select(tag => (tag, (string?)null)).ToList(), cancellationToken);

        return status.Values
            .Select(device => new GatewayTags(
                device.DeviceId,
                device.TagIds
                    .Select(tag =>
                    {
                        assets.TryGetValue(tag, out var asset);
                        return new GatewayTag(tag, asset.AssetRegistrationId, asset.AssetName, device.LastSeen);
                    })
                    .OrderBy(tag => tag.AssetName ?? tag.TagId, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                device.LastSeen,
                device.Status))
            .OrderBy(gateway => gateway.GatewayId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The newest reading of each tag id, keyed ignoring case. Readings store meta.tagid as the device reports it
    /// (BLE MACs in lower case), so each id is also matched in lower and upper case.
    /// </summary>
    private async Task<Dictionary<string, TagLatestReading>> LatestReadingsAsync(IReadOnlyCollection<string> tagIds, CancellationToken cancellationToken)
    {
        var latest = new Dictionary<string, TagLatestReading>(StringComparer.OrdinalIgnoreCase);
        if (tagIds.Count == 0)
        {
            return latest;
        }

        var variants = new BsonArray(tagIds
            .SelectMany(tagId => new[] { tagId, tagId.ToLowerInvariant(), tagId.ToUpperInvariant() })
            .Distinct());

        var rows = await _collection
            .Aggregate()
            .Match(Builders<BsonDocument>.Filter.In("meta.tagid", variants) & Builders<BsonDocument>.Filter.Ne("device_trigger", "out"))
            .Sort(Builders<BsonDocument>.Sort.Descending("timestamp"))
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument("$toLower", "$meta.tagid") },
                { "tagId", new BsonDocument("$first", "$meta.tagid") },
                { "gatewayId", new BsonDocument("$first", "$meta.gatewayid") },
                { "timestamp", new BsonDocument("$first", "$timestamp") },
                { "status", new BsonDocument("$first", "$status") },
                { "battery", new BsonDocument("$first", "$battery") },
                { "rssi", new BsonDocument("$first", "$rssi") }
            })
            .ToListAsync(cancellationToken);

        // The gateway shown for a tag is the device the worker resolved for it (the newest reading is a tie between devices);
        // a state older than the presence window means no live gateway.
        var states = _resolved.Enabled ? await _resolved.GetAsync(null, cancellationToken) : null;
        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            var tagId = row.GetStringOrNull("tagId");
            if (tagId is null)
            {
                continue;
            }

            var gatewayId = row.GetStringOrNull("gatewayId");
            if (states is not null && states.TryGetValue(tagId.ToLowerInvariant(), out var state))
            {
                gatewayId = state.HasLocation && !_resolved.IsStale(state, now) ? state.DeviceId : null;
            }

            latest[tagId] = new TagLatestReading(
                tagId,
                false,
                gatewayId,
                row.TryGetValue("timestamp", out var time) && time.IsValidDateTime ? time.ToUniversalTime() : null,
                row.GetStringOrNull("status"),
                Number(row, "battery"),
                Number(row, "rssi"));
        }

        return latest;

        static double? Number(BsonDocument document, string name) =>
            document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToDouble() : null;
    }

    private static bool Has(BsonDocument asset, string value, params string[] basicFields)
    {
        var basic = asset.TryGetValue("basic_information", out var info) && info.IsBsonDocument ? info.AsBsonDocument : null;
        return basicFields.Any(field => string.Equals(basic.GetStringOrNull(field), value, StringComparison.OrdinalIgnoreCase));
    }
}
