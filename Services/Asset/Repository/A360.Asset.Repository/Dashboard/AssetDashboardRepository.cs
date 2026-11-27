using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Repository.Repositories;
using Microsoft.Extensions.Configuration;

namespace A360.Asset.Repository.Dashboard;

public sealed class AssetDashboardRepository : IAssetDashboardRepository
{
    // Collections written by the Tracking service (same names as its repositories use).
    private const string IoTDeviceCollection = "IoTDevice";
    private const string DeviceHealthCollection = "DeviceHealth";
    private const string FirmwareVersionCollection = "FirmwareVersion";
    private const string SensorCollection = "Sensor";
    private const string LocationEventCollection = "LocationEvent";
    private const string IoTAlertCollection = "IoTAlert";
    private const string AssetUtilizationCollection = "AssetUtilization";
    private const string TelemetryReadingCollection = "TelemetryReading";

    // Device readings written by the network server; each reading names its tag in meta.tagid.
    private const string NetworkTimeSeriesCollection = "NetworkTimeSeries";

    // Tag Management (Tracking service): which tag is attached to which asset. Same source as Real-Time Location.
    private const string TrackingTagCollection = "TrackingTag";

    // Site master data, written by the MasterDataManagement service.
    private const string SiteCollection = "master_sites";

    // Work orders, written by the Maintenance service; asset_id holds the asset tag (or registration id).
    private const string WorkOrderCollection = "work_orders";
    private static readonly string[] ClosedWorkOrderStatuses = ["Completed", "Cancelled", "Canceled", "Closed"];

    // Ids and tags are matched ignoring case, as everywhere else in the services.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<AssetRegistration> _assets;

    private readonly ResolvedLocationReader _resolved;

    public AssetDashboardRepository(IMongoDatabase database, IConfiguration configuration)
    {
        _database = database;
        _resolved = new ResolvedLocationReader(database, configuration);
        _assets = database.GetCollection<AssetRegistration>(AssetRegistrationRepository.CollectionName);
    }

    public async Task<IReadOnlyList<DashboardAsset>> GetAssetsAsync(CancellationToken cancellationToken = default)
    {
        var projection = Builders<AssetRegistration>.Projection
            .Include(asset => asset.AssetRegistrationId)
            .Include(asset => asset.BasicInformation.AssetTag)
            .Include(asset => asset.BasicInformation.AssetName)
            .Include(asset => asset.BasicInformation.Status)
            .Include(asset => asset.Classification.AssetType)
            .Include(asset => asset.Classification.Category)
            .Include(asset => asset.Classification.SubCategory)
            .Include(asset => asset.Classification.Criticality)
            .Include(asset => asset.LocationAssignment.Assignment.AssignedToName)
            .Include(asset => asset.LocationAssignment.Assignment.CustodianDepartment)
            .Include(asset => asset.PurchaseWarranty.Documents)
            .Include(asset => asset.AdditionalInformation.Attachments)
            .Include(asset => asset.LocationAssignment.Location.SiteId)
            .Include(asset => asset.LocationAssignment.Location.SiteName)
            .Include(asset => asset.LocationAssignment.Location.BuildingName)
            .Include(asset => asset.LocationAssignment.Location.FloorName)
            .Include(asset => asset.LocationAssignment.Location.ZoneId)
            .Include(asset => asset.LocationAssignment.Location.ZoneName)
            .Include(asset => asset.LocationAssignment.Location.LocationPath)
            .Include(asset => asset.LocationAssignment.Location.Latitude)
            .Include(asset => asset.LocationAssignment.Location.Longitude)
            .Include(asset => asset.PurchaseWarranty.Purchase.PurchaseDate)
            .Include(asset => asset.AdditionalInformation.Operational.NextMaintenanceDate);

        var assets = await _assets
            .Find(asset => !asset.IsDeleted)
            .Project<AssetRegistration>(projection)
            .ToListAsync(cancellationToken);

        var tagIdsByAsset = await GetTagIdsByAssetAsync(assets, cancellationToken);
        var zoneNames = await ZoneNames.GetAsync(_database, cancellationToken);

        return assets.Select(asset => new DashboardAsset(
                asset.AssetRegistrationId,
                asset.BasicInformation?.AssetTag,
                asset.BasicInformation?.AssetName,
                asset.BasicInformation?.Status,
                asset.Classification?.AssetType,
                asset.LocationAssignment?.Location?.SiteName,
                asset.LocationAssignment?.Location?.Latitude,
                asset.LocationAssignment?.Location?.Longitude,
                asset.LocationAssignment?.Location?.SiteId,
                zoneNames.ResolveWithPath(asset.LocationAssignment?.Location?.ZoneId, asset.LocationAssignment?.Location?.ZoneName, asset.LocationAssignment?.Location?.LocationPath).Path,
                asset.LocationAssignment?.Location?.BuildingName,
                asset.LocationAssignment?.Location?.FloorName,
                zoneNames.Resolve(asset.LocationAssignment?.Location?.ZoneId, asset.LocationAssignment?.Location?.ZoneName),
                asset.Classification?.Category,
                asset.Classification?.SubCategory,
                asset.Classification?.Criticality,
                asset.LocationAssignment?.Assignment?.AssignedToName,
                asset.LocationAssignment?.Assignment?.CustodianDepartment,
                (asset.PurchaseWarranty?.Documents?.Count ?? 0) + (asset.AdditionalInformation?.Attachments?.Count ?? 0),
                asset.PurchaseWarranty?.Purchase?.PurchaseDate,
                asset.AdditionalInformation?.Operational?.NextMaintenanceDate,
                tagIdsByAsset.GetValueOrDefault(asset.AssetRegistrationId).Ids,
                tagIdsByAsset.GetValueOrDefault(asset.AssetRegistrationId).Registered))
            .ToList();
    }

    /// <summary>
    /// The tag ids (normalized, see <see cref="TagKey"/>) of every asset, resolved once the way Real-Time Location does:
    /// a TrackingTag (tag_id or value) whose asset_registration_id is the asset's registration id or asset tag (Tag
    /// Management can store either). Older data without a TrackingTag falls back to the asset's own asset_tag, unless
    /// that id is a registered tag of some asset.
    /// </summary>
    private async Task<Dictionary<string, (IReadOnlyList<string> Ids, bool Registered)>> GetTagIdsByAssetAsync(
        IReadOnlyList<AssetRegistration> assets, CancellationToken cancellationToken)
    {
        var trackingTags = await _database.GetCollection<BsonDocument>(TrackingTagCollection)
            .Find(Builders<BsonDocument>.Filter.Ne("is_deleted", true))
            .Project(Builders<BsonDocument>.Projection.Include("tag_id").Include("value").Include("asset_registration_id"))
            .ToListAsync(cancellationToken);

        static string? Text(BsonDocument doc, string name) =>
            doc.TryGetValue(name, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString) ? value.AsString.Trim() : null;

        // asset key (registration id or asset tag, any case) -> its registered tag ids
        var byAssetKey = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in trackingTags)
        {
            var ids = new[] { TagKey.Normalize(Text(tag, "tag_id")), TagKey.Normalize(Text(tag, "value")) }.OfType<string>().ToList();
            registered.UnionWith(ids);
            var key = Text(tag, "asset_registration_id");
            if (key is null || ids.Count == 0)
            {
                continue;
            }

            if (!byAssetKey.TryGetValue(key, out var set))
            {
                byAssetKey[key] = set = new HashSet<string>(StringComparer.Ordinal);
            }

            set.UnionWith(ids);
        }

        var result = new Dictionary<string, (IReadOnlyList<string> Ids, bool Registered)>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (byAssetKey.TryGetValue(asset.AssetRegistrationId, out var byId))
            {
                ids.UnionWith(byId);
            }

            // True when Tag Management links at least one tag to the asset (as opposed to the asset_tag fallback).
            var isRegistered = ids.Count > 0;

            var assetTag = asset.BasicInformation?.AssetTag?.Trim();
            if (!string.IsNullOrEmpty(assetTag))
            {
                if (byAssetKey.TryGetValue(assetTag, out var byTag))
                {
                    ids.UnionWith(byTag);
                    isRegistered = true;
                }

                // Fallback for data without Tag Management: the asset tag is the tag id.
                if (TagKey.Normalize(assetTag) is { } own && !registered.Contains(own))
                {
                    ids.Add(own);
                }
            }

            if (ids.Count > 0)
            {
                result[asset.AssetRegistrationId] = (ids.ToList(), isRegistered);
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<DashboardOpenWorkOrder>> GetOpenWorkOrdersDueAsync(
        IReadOnlyCollection<string> assetKeys, DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
    {
        if (assetKeys.Count == 0)
        {
            return [];
        }

        var filter = Builders<BsonDocument>.Filter;
        var rows = await _database.GetCollection<BsonDocument>(WorkOrderCollection)
            .Find(filter.Ne("is_deleted", true)
                & filter.In("asset_id", assetKeys)
                & filter.Nin("status", ClosedWorkOrderStatuses))
            .Project(Builders<BsonDocument>.Projection
                .Include("work_order_id").Include("asset_id").Include("status")
                .Include("due_date").Include("scheduled_date").Include("planned_start_date"))
            .ToListAsync(cancellationToken);

        // due_date, else scheduled_date, else planned_start_date; orders with none of them are not "due"
        static DateTime? DateOf(BsonDocument row, string name) =>
            row.TryGetValue(name, out var value) && value.IsValidDateTime ? value.ToUniversalTime() : null;

        return rows
            .Select(row => (Row: row, Due: DateOf(row, "due_date") ?? DateOf(row, "scheduled_date") ?? DateOf(row, "planned_start_date")))
            .Where(item => item.Due is not null && item.Due < dueBeforeUtc)
            .Select(item => new DashboardOpenWorkOrder(
                item.Row.GetValue("work_order_id", BsonNull.Value).IsString ? item.Row["work_order_id"].AsString : item.Row["_id"].ToString()!,
                item.Row["asset_id"].AsString,
                item.Row.GetValue("status", BsonNull.Value).IsString ? item.Row["status"].AsString : null,
                item.Due!.Value))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> CountDocumentRecordsByAssetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _database.GetCollection<BsonDocument>(AssetDocumentRepository.CollectionName)
            .Aggregate()
            .Match(Builders<BsonDocument>.Filter.Ne("is_deleted", true))
            .Group(new BsonDocument { { "_id", "$asset_registration_id" }, { "count", new BsonDocument("$sum", 1) } })
            .ToListAsync(cancellationToken);

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var id = row["_id"].IsBsonNull ? null : row["_id"].ToString()?.Trim();
            if (!string.IsNullOrEmpty(id))
            {
                counts[id] = counts.GetValueOrDefault(id) + row["count"].ToInt32();
            }
        }

        return counts;
    }

    public async Task<IReadOnlyDictionary<string, DashboardSite>> GetSitesAsync(CancellationToken cancellationToken = default)
    {
        var sites = await Collection<DashboardSite>(SiteCollection)
            .Find(Builders<DashboardSite>.Filter.Ne(site => site.IsDeleted, true))
            .ToListAsync(cancellationToken);

        return sites.ToDictionary(site => site.Id, StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyDictionary<string, NetworkTagSummary>> GetNetworkTagsAsync(CancellationToken cancellationToken = default)
    {
        return SummariseNetworkTagsAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Exists("meta.tagid"),
                Builders<BsonDocument>.Filter.Ne("meta.tagid", BsonNull.Value)),
            cancellationToken);
    }

    public async Task<NetworkTagSummary?> GetNetworkTagAsync(string tagId, CancellationToken cancellationToken = default)
    {
        var tags = await SummariseNetworkTagsAsync(Builders<BsonDocument>.Filter.Eq("meta.tagid", tagId.Trim()), cancellationToken);
        return tags.Values.FirstOrDefault();
    }

    public async Task<IReadOnlyList<NetworkTagReading>> GetNetworkTagReadingsAsync(string tagId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await _database.GetCollection<BsonDocument>(NetworkTimeSeriesCollection)
            .Find(Builders<BsonDocument>.Filter.Eq("meta.tagid", tagId.Trim()) & Builders<BsonDocument>.Filter.Ne("device_trigger", "out"), new FindOptions { Collation = CaseInsensitive })
            .Sort(new BsonDocument { { "timestamp", -1 }, { "_id", -1 } })
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new NetworkTagReading(
                row.TryGetValue("timestamp", out var time) && time.IsValidDateTime ? time.ToUniversalTime() : null,
                row.TryGetValue("meta", out var meta) && meta.IsBsonDocument && meta.AsBsonDocument.TryGetValue("gatewayid", out var gateway) && !gateway.IsBsonNull ? gateway.ToString() : null,
                row.TryGetValue("status", out var status) && !status.IsBsonNull ? status.ToString()?.Trim().ToLowerInvariant() : null,
                row.TryGetValue("rssi", out var rssi) && rssi.IsNumeric ? rssi.ToDouble() : null,
                row.TryGetValue("raw", out var raw) && !raw.IsBsonNull ? raw.ToString() : null))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, NetworkTagSummary>> SummariseNetworkTagsAsync(
        FilterDefinition<BsonDocument> filter,
        CancellationToken cancellationToken)
    {
        // Grouped in Mongo so only one row per tag comes back, however many readings there are.
        var newestFirst = new BsonDocument("timestamp", -1);
        var rows = await _database.GetCollection<BsonDocument>(NetworkTimeSeriesCollection)
            .Aggregate(new AggregateOptions { Collation = CaseInsensitive })
            .Match(filter & Builders<BsonDocument>.Filter.Ne("device_trigger", "out"))
            .Group(new BsonDocument
            {
                { "_id", "$meta.tagid" },
                { "firstSeen", new BsonDocument("$min", "$timestamp") },
                { "lastSeen", new BsonDocument("$max", "$timestamp") },
                { "lastStatus", new BsonDocument("$top", new BsonDocument { { "sortBy", newestFirst }, { "output", "$status" } }) },
                { "lastGateway", new BsonDocument("$top", new BsonDocument { { "sortBy", newestFirst }, { "output", "$meta.gatewayid" } }) },
                { "readings", new BsonDocument("$sum", 1) }
            })
            .ToListAsync(cancellationToken);

        // Tags differing only in case, spacing, ':' or '-' are one tag (key = TagKey.Normalize); their readings are combined.
        var tags = new Dictionary<string, NetworkTagSummary>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var tagId = row["_id"].ToString()?.Trim();
            if (string.IsNullOrEmpty(tagId))
            {
                continue;
            }

            var summary = new NetworkTagSummary(
                tagId,
                DateOrNull(row, "firstSeen"),
                DateOrNull(row, "lastSeen"),
                TextOrNull(row, "lastStatus")?.ToLowerInvariant(),
                TextOrNull(row, "lastGateway"),
                row["readings"].ToInt64());

            var key = TagKey.Normalize(tagId) ?? tagId;
            tags[key] = tags.TryGetValue(key, out var existing) ? Combine(existing, summary) : summary;
        }

        // The gateway of a tag is the device the worker resolved for it (the newest reading is a tie between devices).
        if (_resolved.Enabled && tags.Count > 0)
        {
            var states = await _resolved.GetAsync(null, cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var (key, summary) in tags.ToList())
            {
                var stateKey = summary.TagId.Replace(":", "").Replace("-", "").Trim().ToLowerInvariant();
                if (states.TryGetValue(stateKey, out var state))
                {
                    tags[key] = summary with { LastGatewayId = state.HasLocation && !_resolved.IsStale(state, now) ? state.DeviceId : null };
                }
            }
        }

        return tags;

        static DateTime? DateOrNull(BsonDocument row, string name) =>
            row.TryGetValue(name, out var value) && value.IsValidDateTime ? value.ToUniversalTime() : null;

        static string? TextOrNull(BsonDocument row, string name) =>
            row.TryGetValue(name, out var value) && !value.IsBsonNull ? value.ToString()?.Trim() : null;

        static NetworkTagSummary Combine(NetworkTagSummary a, NetworkTagSummary b)
        {
            var newest = (b.LastSeenAt ?? DateTime.MinValue) > (a.LastSeenAt ?? DateTime.MinValue) ? b : a;
            DateTime? first = a.FirstSeenAt is null ? b.FirstSeenAt : b.FirstSeenAt is null ? a.FirstSeenAt : a.FirstSeenAt < b.FirstSeenAt ? a.FirstSeenAt : b.FirstSeenAt;
            return newest with { FirstSeenAt = first, ReadingCount = a.ReadingCount + b.ReadingCount };
        }
    }

    public async Task<NetworkActivity> GetNetworkActivityAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        IReadOnlyCollection<string>? tagIds,
        CancellationToken cancellationToken = default)
    {
        var empty = new NetworkActivity(new Dictionary<string, NetworkSeen>(), new Dictionary<string, NetworkSeen>());
        if (tagIds is { Count: 0 })
        {
            return empty;
        }

        // The worker's "out" documents (old gateway, same timestamp as the new "in") are not readings.
        var filter = new BsonDocument("device_trigger", new BsonDocument("$ne", "out"));
        if (tagIds is not null)
        {
            // Matched with the case-insensitive collation below.
            filter["meta.tagid"] = new BsonDocument("$in", new BsonArray(tagIds.Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)));
        }

        var time = new BsonDocument();
        if (fromUtc is not null)
        {
            time["$gte"] = fromUtc.Value;
        }

        if (toUtc is not null)
        {
            time["$lt"] = toUtc.Value;
        }

        if (time.ElementCount > 0)
        {
            filter["timestamp"] = time;
        }

        BsonDocument[] SeenBy(string field) =>
        [
            new("$match", new BsonDocument(field, new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }))),
            new("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$toLower", new BsonDocument("$trim", new BsonDocument("input", new BsonDocument("$toString", "$" + field)))) },
                { "first", new BsonDocument("$min", "$timestamp") },
                { "last", new BsonDocument("$max", "$timestamp") },
                { "lastStatus", new BsonDocument("$top", new BsonDocument
                    {
                        { "sortBy", new BsonDocument { { "timestamp", -1 }, { "_id", -1 } } },
                        { "output", "$status" }
                    }) }
            })
        ];

        var stages = new[]
        {
            new BsonDocument("$match", filter),
            new BsonDocument("$facet", new BsonDocument
            {
                { "tags", new BsonArray(SeenBy("meta.tagid")) },
                { "gateways", new BsonArray(SeenBy("meta.gatewayid")) }
            })
        };

        var result = await _database.GetCollection<BsonDocument>(NetworkTimeSeriesCollection)
            .Aggregate(
                PipelineDefinition<BsonDocument, BsonDocument>.Create(stages),
                new AggregateOptions { Collation = CaseInsensitive, AllowDiskUse = true },
                cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return empty;
        }

        return new NetworkActivity(Seen(result["tags"].AsBsonArray), Seen(result["gateways"].AsBsonArray));

        static IReadOnlyDictionary<string, NetworkSeen> Seen(BsonArray rows)
        {
            var seen = new Dictionary<string, NetworkSeen>(StringComparer.Ordinal);
            foreach (var row in rows.Select(value => value.AsBsonDocument))
            {
                var id = TagKey.Normalize(row["_id"].ToString());
                if (string.IsNullOrEmpty(id) || !row["first"].IsValidDateTime || !row["last"].IsValidDateTime)
                {
                    continue;
                }

                var status = row.TryGetValue("lastStatus", out var value) && !value.IsBsonNull ? value.ToString()?.Trim().ToLowerInvariant() : null;
                seen[id] = new NetworkSeen(row["first"].ToUniversalTime(), row["last"].ToUniversalTime(), status);
            }

            return seen;
        }
    }

    public async Task<IReadOnlyList<NetworkTagGatewayChanges>> CountGatewayChangesAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        IReadOnlyCollection<string>? tagIds,
        CancellationToken cancellationToken = default)
    {
        if (tagIds is { Count: 0 })
        {
            return [];
        }

        var readingFilter = new BsonDocument
        {
            { "meta.tagid", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) },
            { "meta.gatewayid", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) },
            { "device_trigger", new BsonDocument("$ne", "out") }
        };
        // Gateway changes = changes of the device the worker resolved: only its "in" readings, not every device that heard the tag.
        if (_resolved.Enabled) readingFilter["device_trigger"] = "in";
        if (tagIds is not null)
        {
            // Matched with the case-insensitive collation below, as GetNetworkTagAsync does.
            readingFilter["meta.tagid"] = new BsonDocument("$in", new BsonArray(tagIds.Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)));
        }

        // Only readings before the window's end matter. Readings from a day before the window start are kept so a
        // window's first reading can still be compared with the tag's previous reading; older history is left out,
        // otherwise the per-tag sort below runs over the whole collection and exceeds Mongo's sort memory limit.
        var readingTime = new BsonDocument();
        if (fromUtc is not null)
        {
            readingTime["$gte"] = fromUtc.Value.AddDays(-1);
        }

        if (toUtc is not null)
        {
            readingTime["$lt"] = toUtc.Value;
        }

        if (readingTime.ElementCount > 0)
        {
            readingFilter["timestamp"] = readingTime;
        }

        // The readings are streamed and paired per tag here rather than with $setWindowFields: that stage sorts every
        // reading in memory, and the Atlas tier caps sorts at 32 MB without allowing external sorting (500 for a 30-day window).
        var projection = new BsonDocument
        {
            { "timestamp", 1 }, { "meta.tagid", 1 }, { "meta.gatewayid", 1 }, { "latitude", 1 }, { "longitude", 1 }
        };
        var readingsByTag = new Dictionary<string, List<GatewayReading>>();
        using (var cursor = await _database.GetCollection<BsonDocument>(NetworkTimeSeriesCollection)
                   .Find(readingFilter, new FindOptions { Collation = CaseInsensitive, BatchSize = 5000 })
                   .Project(projection)
                   .ToCursorAsync(cancellationToken))
        {
            while (await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var row in cursor.Current)
                {
                    if (!row.TryGetValue("timestamp", out var time) || !time.IsValidDateTime
                        || !row.TryGetValue("meta", out var meta) || !meta.IsBsonDocument)
                    {
                        continue;
                    }

                    var tag = Normalised(meta.AsBsonDocument, "tagid");
                    var gateway = Normalised(meta.AsBsonDocument, "gatewayid");
                    if (tag is null || gateway is null)
                    {
                        continue;
                    }

                    if (!readingsByTag.TryGetValue(tag, out var list))
                    {
                        readingsByTag[tag] = list = [];
                    }

                    list.Add(new GatewayReading(
                        time.ToUniversalTime(),
                        row.TryGetValue("_id", out var id) ? id : BsonNull.Value,
                        gateway,
                        Number(row, "latitude"),
                        Number(row, "longitude")));
                }
            }
        }

        var results = new List<NetworkTagGatewayChanges>();
        foreach (var (tag, readings) in readingsByTag)
        {
            // Oldest first, as the reading order the gateway changes are counted in.
            readings.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Id.CompareTo(b.Id));

            long changes = 0;
            GatewayReading? lastFrom = null;
            GatewayReading? lastTo = null;
            for (var i = 1; i < readings.Count; i++)
            {
                var previous = readings[i - 1];
                var current = readings[i];
                if (previous.Gateway == current.Gateway || (fromUtc is not null && current.At < fromUtc.Value))
                {
                    continue;
                }

                changes++;
                lastFrom = previous;
                lastTo = current;
            }

            if (changes > 0)
            {
                results.Add(new NetworkTagGatewayChanges(
                    tag,
                    changes,
                    lastTo!.At,
                    lastFrom!.Gateway,
                    lastTo.Gateway,
                    lastFrom.Latitude,
                    lastFrom.Longitude,
                    lastTo.Latitude,
                    lastTo.Longitude));
            }
        }

        return results
            .OrderByDescending(row => row.Changes)
            .ThenBy(row => row.TagId, StringComparer.Ordinal)
            .ToList();

        static string? Normalised(BsonDocument meta, string field) =>
            meta.TryGetValue(field, out var value) && !value.IsBsonNull && value.ToString() is { } text
                && text.Trim() is { Length: > 0 } trimmed
                ? trimmed.ToLowerInvariant()
                : null;

        static double? Number(BsonDocument document, string field) =>
            document.TryGetValue(field, out var value) && value.IsNumeric ? value.ToDouble() : null;
    }

    public Task<IReadOnlyList<DashboardDevice>> GetDevicesAsync(IReadOnlyCollection<string> assetRegistrationIds, CancellationToken cancellationToken = default)
    {
        return FindAsync(
            Collection<DashboardDevice>(IoTDeviceCollection),
            Builders<DashboardDevice>.Filter.In(device => device.AssetRegistrationId, assetRegistrationIds),
            cancellationToken);
    }

    public Task<IReadOnlyList<DashboardDeviceHealth>> GetDeviceHealthAsync(IReadOnlyCollection<string> deviceIds, CancellationToken cancellationToken = default)
    {
        return FindAsync(
            Collection<DashboardDeviceHealth>(DeviceHealthCollection),
            Builders<DashboardDeviceHealth>.Filter.In(health => health.DeviceId, deviceIds),
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, DashboardFirmware>> GetLatestFirmwareByModelAsync(CancellationToken cancellationToken = default)
    {
        var firmware = await FindAsync(Collection<DashboardFirmware>(FirmwareVersionCollection), FilterDefinition<DashboardFirmware>.Empty, cancellationToken);

        return firmware
            .Where(record => !string.IsNullOrWhiteSpace(record.DeviceModel))
            .GroupBy(record => record.DeviceModel.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(record => record.ReleaseDate ?? DateTime.MinValue).First(),
                StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<DashboardSensor>> GetSensorsAsync(IReadOnlyCollection<string> assetRegistrationIds, string? status, CancellationToken cancellationToken = default)
    {
        var filter = Builders<DashboardSensor>.Filter.In(sensor => sensor.AssetRegistrationId, assetRegistrationIds);
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= Builders<DashboardSensor>.Filter.Eq(sensor => sensor.Status, status.Trim());
        }

        return FindAsync(Collection<DashboardSensor>(SensorCollection), filter, cancellationToken);
    }

    public Task<IReadOnlyList<DashboardLocationEvent>> GetLocationEventsAsync(IReadOnlyCollection<string> assetKeys, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<DashboardLocationEvent>.Filter;
        return FindAsync(
            Collection<DashboardLocationEvent>(LocationEventCollection),
            filter.In(locationEvent => locationEvent.AssetId, assetKeys) & filter.Gte(locationEvent => locationEvent.EventTime, fromUtc),
            cancellationToken);
    }

    public async Task<IReadOnlyList<DashboardAlert>> GetAlertsAsync(
        IReadOnlyCollection<string> assetRegistrationIds, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<DashboardAlert>.Filter;
        var match = filter.In(alert => alert.AssetRegistrationId, assetRegistrationIds);

        if (fromUtc is not null)
        {
            match &= filter.Gte(alert => alert.RaisedAt, fromUtc.Value);
        }

        if (toUtc is not null)
        {
            match &= filter.Lt(alert => alert.RaisedAt, toUtc.Value);
        }

        var alerts = await FindAsync(Collection<DashboardAlert>(IoTAlertCollection), match, cancellationToken);
        return alerts.OrderByDescending(alert => alert.RaisedAt).ToList();
    }

    public Task<IReadOnlyList<DashboardUtilization>> GetUtilizationAsync(
        IReadOnlyCollection<string> assetRegistrationIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<DashboardUtilization>.Filter;
        return FindAsync(
            Collection<DashboardUtilization>(AssetUtilizationCollection),
            filter.In(record => record.AssetRegistrationId, assetRegistrationIds)
                & filter.Gte(record => record.Date, fromUtc)
                & filter.Lt(record => record.Date, toUtc),
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetLatestHealthScoresAsync(CancellationToken cancellationToken = default)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("is_deleted", false)),
            new BsonDocument("$sort", new BsonDocument("assessment_date", -1)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$asset_registration_id" },
                { "health_score", new BsonDocument("$first", "$health_score") }
            })
        };

        var rows = await _database.GetCollection<BsonDocument>(AssetConditionAssessmentRepository.CollectionName)
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => row["_id"].IsString && row["health_score"].IsNumeric)
            .ToDictionary(row => row["_id"].AsString, row => row["health_score"].ToDecimal(), StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<DashboardDailyCount>> CountTelemetryPerDayAsync(
        IReadOnlyCollection<string> assetRegistrationIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return CountPerDayAsync(TelemetryReadingCollection, "recorded_at", "asset_registration_id", assetRegistrationIds, fromUtc, toUtc, null, cancellationToken);
    }

    public Task<IReadOnlyList<DashboardDailyCount>> CountAlertsPerDayAsync(
        IReadOnlyCollection<string> assetRegistrationIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return CountPerDayAsync(IoTAlertCollection, "raised_at", "asset_registration_id", assetRegistrationIds, fromUtc, toUtc, null, cancellationToken);
    }

    public Task<IReadOnlyList<DashboardDailyCount>> CountLocationEventsPerDayAsync(
        IReadOnlyCollection<string> assetKeys, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return CountPerDayAsync(LocationEventCollection, "event_time", "asset_id", assetKeys, fromUtc, toUtc, "event_type", cancellationToken);
    }

    /// <summary>Counts records per UTC day in Mongo (optionally split by <paramref name="keyField"/>), so timelines never load the records.</summary>
    private async Task<IReadOnlyList<DashboardDailyCount>> CountPerDayAsync(
        string collectionName,
        string dateField,
        string assetField,
        IReadOnlyCollection<string> assetValues,
        DateTime fromUtc,
        DateTime toUtc,
        string? keyField,
        CancellationToken cancellationToken)
    {
        if (assetValues.Count == 0)
        {
            return [];
        }

        var groupId = new BsonDocument("date", new BsonDocument("$dateToString", new BsonDocument
        {
            { "format", "%Y-%m-%d" },
            { "date", "$" + dateField }
        }));

        if (keyField is not null)
        {
            groupId.Add("key", "$" + keyField);
        }

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "is_deleted", false },
                { assetField, new BsonDocument("$in", new BsonArray(assetValues)) },
                { dateField, new BsonDocument { { "$gte", fromUtc }, { "$lt", toUtc } } }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", groupId },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var rows = await _database.GetCollection<BsonDocument>(collectionName)
            .Aggregate<BsonDocument>(pipeline, new AggregateOptions { Collation = CaseInsensitive }, cancellationToken)
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            var id = row["_id"].AsBsonDocument;
            var key = id.TryGetValue("key", out var keyValue) && keyValue.IsString ? keyValue.AsString : null;
            return new DashboardDailyCount(id["date"].AsString, key, row["count"].ToInt32());
        }).ToList();
    }

    public async Task<AssetSummaryCounts?> GetAssetSummaryCountsAsync(string assetRegistrationIdOrTag, CancellationToken cancellationToken = default)
    {
        var key = assetRegistrationIdOrTag.Trim();
        var assetFilter = Builders<AssetRegistration>.Filter;
        var asset = await _assets
            .Find(
                assetFilter.Eq(item => item.IsDeleted, false)
                & assetFilter.Or(
                    assetFilter.Eq(item => item.AssetRegistrationId, key),
                    assetFilter.Eq(item => item.BasicInformation.AssetTag, key)),
                new FindOptions { Collation = CaseInsensitive })
            .Project<AssetRegistration>(Builders<AssetRegistration>.Projection
                .Include(item => item.AssetRegistrationId)
                .Include(item => item.BasicInformation.AssetTag)
                .Include(item => item.BasicInformation.AssetName)
                .Include(item => item.PurchaseWarranty.Documents)
                .Include(item => item.AdditionalInformation.Attachments))
            .FirstOrDefaultAsync(cancellationToken);

        if (asset is null)
        {
            return null;
        }

        var id = asset.AssetRegistrationId;
        var tag = string.IsNullOrWhiteSpace(asset.BasicInformation?.AssetTag) ? null : asset.BasicInformation.AssetTag.Trim();
        var filter = Builders<BsonDocument>.Filter;
        var notDeleted = filter.Ne("is_deleted", true);

        var components = CountAsync(AssetComponentRepository.CollectionName, notDeleted & filter.Eq("asset_registration_id", id), cancellationToken);
        var documentRecords = CountAsync(AssetDocumentRepository.CollectionName, notDeleted & filter.Eq("asset_registration_id", id), cancellationToken);
        var images = CountAsync(AssetImageRepository.CollectionName, notDeleted & filter.Eq("asset_registration_id", id), cancellationToken);
        var serviceRequests = CountAsync(AssetServiceRequestRepository.CollectionName, notDeleted & filter.Eq("asset.asset_registration_id", id), cancellationToken);
        var openAlerts = CountAsync(IoTAlertCollection, notDeleted & filter.Eq("asset_registration_id", id) & filter.Nin("status", new[] { "Resolved", "Closed" }), cancellationToken);
        var workOrders = CountAsync(
            WorkOrderCollection,
            notDeleted & filter.In("asset_id", tag is null ? new[] { id } : new[] { id, tag }),
            cancellationToken);
        var relationships = CountRelationshipsAsync(id, cancellationToken);

        await Task.WhenAll(components, documentRecords, images, serviceRequests, openAlerts, workOrders, relationships);

        var attachedDocuments = (asset.PurchaseWarranty?.Documents?.Count ?? 0) + (asset.AdditionalInformation?.Attachments?.Count ?? 0);

        return new AssetSummaryCounts(
            id,
            tag,
            asset.BasicInformation?.AssetName,
            components.Result,
            relationships.Result,
            documentRecords.Result + attachedDocuments,
            images.Result,
            workOrders.Result,
            serviceRequests.Result,
            openAlerts.Result);
    }

    private async Task<int> CountAsync(string collectionName, FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken) =>
        (int)await _database.GetCollection<BsonDocument>(collectionName)
            .CountDocumentsAsync(filter, new CountOptions { Collation = CaseInsensitive }, cancellationToken);

    /// <summary>Sub assets linked under this asset, plus the links that name it as someone else's sub asset.</summary>
    private async Task<int> CountRelationshipsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var filter = Builders<AssetRelationship>.Filter;
        var records = await _database.GetCollection<AssetRelationship>(AssetRelationshipRepository.CollectionName)
            .Find(
                filter.Eq(record => record.IsDeleted, false)
                & filter.Or(
                    filter.Eq(record => record.MainAssetRegistrationId, assetRegistrationId),
                    filter.Eq("sub_assets.asset_registration_id", assetRegistrationId)),
                new FindOptions { Collation = CaseInsensitive })
            .ToListAsync(cancellationToken);

        return records.Sum(record =>
            string.Equals(record.MainAssetRegistrationId, assetRegistrationId, StringComparison.OrdinalIgnoreCase)
                ? record.SubAssets.Count
                : record.SubAssets.Count(sub => string.Equals(sub.AssetRegistrationId, assetRegistrationId, StringComparison.OrdinalIgnoreCase)));
    }

    private IMongoCollection<TRecord> Collection<TRecord>(string name) => _database.GetCollection<TRecord>(name);

    private static async Task<IReadOnlyList<TRecord>> FindAsync<TRecord>(
        IMongoCollection<TRecord> collection, FilterDefinition<TRecord> filter, CancellationToken cancellationToken)
    {
        var notDeleted = Builders<TRecord>.Filter.Eq("is_deleted", false);
        return await collection
            .Find(notDeleted & filter, new FindOptions { Collation = CaseInsensitive })
            .ToListAsync(cancellationToken);
    }
}

internal sealed record GatewayReading(DateTime At, BsonValue Id, string Gateway, double? Latitude, double? Longitude);
