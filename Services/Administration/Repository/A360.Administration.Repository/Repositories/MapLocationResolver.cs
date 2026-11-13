using MongoDB.Bson;
using MongoDB.Driver;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Repository.Repositories;

/// <summary>
/// Fills the location (site &gt; building &gt; floor &gt; Master Data zone) of drawn zones and plotted items, and the
/// device / asset ids of plots. Reads the Master Data Management and Tracking collections of the same database
/// (read-only; those services own them).
/// </summary>
public interface IMapLocationResolver
{
    /// <summary>Site / building / floor of a floor plan's location (floor, building or site).</summary>
    Task<MapLocation> ForPlanAsync(FloorPlan plan, CancellationToken cancellationToken = default);

    /// <summary>The plan's location plus the Master Data zone on that floor with the drawn zone's code.</summary>
    Task<MapLocation> ForZoneAsync(FloorPlan plan, string zoneCode, CancellationToken cancellationToken = default);

    /// <summary>Location, drawn-zone name / code and device / asset ids of a plotted item.</summary>
    Task ApplyToPlotAsync(FloorPlan plan, MapPlot plot, CancellationToken cancellationToken = default);
}

public sealed class MapLocationResolver : IMapLocationResolver
{
    // Owned by Master Data Management and Tracking
    private const string Sites = "master_sites";
    private const string Buildings = "master_buildings";
    private const string Floors = "master_floors";
    private const string Zones = "master_zones";
    private const string Devices = "IoTDevice";

    private readonly IMongoDatabase _database;
    private readonly IMapZoneRepository _zones;

    public MapLocationResolver(IMongoDatabase database, IMapZoneRepository zones)
    {
        _database = database;
        _zones = zones;
    }

    public async Task<MapLocation> ForPlanAsync(FloorPlan plan, CancellationToken cancellationToken = default)
    {
        var location = new MapLocation();
        var type = plan.LocationType?.Trim().ToLowerInvariant();
        var id = plan.LocationId;

        if (type == "floor")
        {
            var floor = await ByIdAsync(Floors, id, cancellationToken);
            if (floor is not null)
            {
                location.FloorId = id;
                location.FloorName = Text(floor, "name");
                location.BuildingId = Text(floor, "building_id");
                location.BuildingName = Text(floor, "building_name");
                location.SiteId = RefId(floor, "site");
                location.SiteName = RefName(floor, "site");
            }
        }

        if (type == "building" || (type == "floor" && location.BuildingId is not null))
        {
            var building = await ByIdAsync(Buildings, type == "building" ? id : location.BuildingId, cancellationToken);
            if (building is not null)
            {
                location.BuildingId = building["_id"].ToString();
                location.BuildingName = Text(building, "name") ?? location.BuildingName;
                location.SiteId ??= RefId(building, "site");
                location.SiteName ??= RefName(building, "site");
            }
        }

        var site = await ByIdAsync(Sites, type == "site" ? id : location.SiteId, cancellationToken);
        if (site is not null)
        {
            location.SiteId = site["_id"].ToString();
            location.SiteName = Text(site, "site_name") ?? Text(site, "name") ?? location.SiteName;
        }

        location.LocationPath = Path(location) ?? plan.LocationPath;
        return location;
    }

    public async Task<MapLocation> ForZoneAsync(FloorPlan plan, string zoneCode, CancellationToken cancellationToken = default)
    {
        var location = await ForPlanAsync(plan, cancellationToken);
        if (string.IsNullOrWhiteSpace(zoneCode))
        {
            return location;
        }

        // The Master Data zone on this floor (or building) with the drawn zone's code
        var builder = Builders<BsonDocument>.Filter;
        var filter = builder.Eq("code", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(zoneCode.Trim())}$", "i"))
            & builder.Ne("is_deleted", true);
        filter &= location.FloorId is not null
            ? builder.Eq("floor_id", location.FloorId)
            : builder.Eq("building_id", location.BuildingId ?? plan.LocationId);

        var zone = await _database.GetCollection<BsonDocument>(Zones).Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (zone is not null)
        {
            location.ZoneId = zone["_id"].ToString();
            location.ZoneName = Text(zone, "name");
            location.ZoneCode = Text(zone, "code");
        }

        location.LocationPath = Path(location) ?? location.LocationPath;
        return location;
    }

    public async Task ApplyToPlotAsync(FloorPlan plan, MapPlot plot, CancellationToken cancellationToken = default)
    {
        // Zone the item was dropped in: its drawn zone carries the full location; else the floor's
        var drawn = string.IsNullOrWhiteSpace(plot.ZoneId) ? null : await _zones.GetByZoneIdAsync(plot.ZoneId, cancellationToken);
        if (drawn is not null && !drawn.IsDeleted)
        {
            plot.ZoneName = drawn.Name;
            plot.ZoneCode = drawn.Code;
            plot.Location = drawn.Location?.Copy() ?? await ForZoneAsync(plan, drawn.Code, cancellationToken);
        }
        else
        {
            plot.ZoneName = null;
            plot.ZoneCode = null;
            plot.Location = await ForPlanAsync(plan, cancellationToken);
        }

        if (string.Equals(plot.Kind, "device", StringComparison.OrdinalIgnoreCase))
        {
            var filter = Builders<BsonDocument>.Filter.Eq("device_id", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(plot.ItemId)}$", "i"))
                & Builders<BsonDocument>.Filter.Ne("is_deleted", true);
            var device = await _database.GetCollection<BsonDocument>(Devices).Find(filter).FirstOrDefaultAsync(cancellationToken);
            plot.DeviceId = device is null ? plot.ItemId : Text(device, "device_id") ?? plot.ItemId;
            plot.DeviceRecordId = device?["_id"].ToString();
            plot.AssetRegistrationId = device is null ? null : Text(device, "asset_registration_id");
        }
        else
        {
            plot.DeviceId = null;
            plot.DeviceRecordId = null;
            plot.AssetRegistrationId = plot.ItemId;
        }
    }

    private async Task<BsonDocument?> ByIdAsync(string collection, string? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out var objectId))
        {
            return null;
        }

        return await _database.GetCollection<BsonDocument>(collection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", objectId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? Text(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString) ? value.AsString : null;

    // Master Data references are stored as { id, name }
    private static string? RefId(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value.IsBsonDocument ? Text(value.AsBsonDocument, "id") : null;

    private static string? RefName(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value.IsBsonDocument ? Text(value.AsBsonDocument, "name") : null;

    private static string? Path(MapLocation l)
    {
        var parts = new[] { l.SiteName, l.BuildingName, l.FloorName, l.ZoneName }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return parts.Count > 0 ? string.Join(" > ", parts) : null;
    }
}
