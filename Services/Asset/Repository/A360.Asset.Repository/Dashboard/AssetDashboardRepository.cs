using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

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

    // Site master data, written by the MasterDataManagement service.
    private const string SiteCollection = "master_sites";

    // Ids and tags are matched ignoring case, as everywhere else in the services.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<AssetRegistration> _assets;

    public AssetDashboardRepository(IMongoDatabase database)
    {
        _database = database;
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
            .Include(asset => asset.LocationAssignment.Location.ZoneName)
            .Include(asset => asset.LocationAssignment.Location.LocationPath)
            .Include(asset => asset.LocationAssignment.Location.Latitude)
            .Include(asset => asset.LocationAssignment.Location.Longitude);

        var assets = await _assets
            .Find(asset => !asset.IsDeleted)
            .Project<AssetRegistration>(projection)
            .ToListAsync(cancellationToken);

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
                asset.LocationAssignment?.Location?.LocationPath,
                asset.LocationAssignment?.Location?.BuildingName,
                asset.LocationAssignment?.Location?.FloorName,
                asset.LocationAssignment?.Location?.ZoneName,
                asset.Classification?.Category,
                asset.Classification?.SubCategory,
                asset.Classification?.Criticality,
                asset.LocationAssignment?.Assignment?.AssignedToName,
                asset.LocationAssignment?.Assignment?.CustodianDepartment,
                (asset.PurchaseWarranty?.Documents?.Count ?? 0) + (asset.AdditionalInformation?.Attachments?.Count ?? 0)))
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
            .Find(Builders<BsonDocument>.Filter.Eq("meta.tagid", tagId.Trim()), new FindOptions { Collation = CaseInsensitive })
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
            .Match(filter)
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

        // Tags differing only in case or spacing are one tag; their readings are combined.
        var tags = new Dictionary<string, NetworkTagSummary>(StringComparer.OrdinalIgnoreCase);
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

            tags[tagId] = tags.TryGetValue(tagId, out var existing) ? Combine(existing, summary) : summary;
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
