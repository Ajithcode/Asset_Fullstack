using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Alerts;

/// <summary>A floor plan zone (Maps &amp; Floor Plans) an alert rule can watch.</summary>
/// <param name="Label">e.g. "zone1 · Building A1 &gt; BuildingA2 &gt; Floor2".</param>
public sealed record MapZoneRef(string ZoneId, string FloorPlanId, string Name, string Code, string Label, string? SiteId, string? SiteName, string? LocationPath);

/// <summary>Reads drawn zones and the gateways plotted in them (maps_zones / maps_plots, owned by Administration; read-only).</summary>
public sealed class MapZoneLookup(IMongoDatabase database)
{
    private const string Zones = "maps_zones";
    private const string Plots = "maps_plots";

    public async Task<MapZoneRef?> GetAsync(string zoneId, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("zone_id", zoneId.Trim()) & Builders<BsonDocument>.Filter.Ne("is_deleted", true);
        var zone = await database.GetCollection<BsonDocument>(Zones).Find(filter).FirstOrDefaultAsync(cancellationToken);
        return zone is null ? null : ToRef(zone);
    }

    public async Task<Dictionary<string, MapZoneRef>> GetManyAsync(IEnumerable<string> zoneIds, CancellationToken cancellationToken)
    {
        var ids = zoneIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<BsonDocument>.Filter.In("zone_id", ids) & Builders<BsonDocument>.Filter.Ne("is_deleted", true);
        var zones = await database.GetCollection<BsonDocument>(Zones).Find(filter).ToListAsync(cancellationToken);
        return zones.Select(ToRef).ToDictionary(z => z.ZoneId);
    }

    /// <summary>Gateway (device) ids plotted in each zone, lower case.</summary>
    public async Task<Dictionary<string, List<(string GatewayId, string DeviceId)>>> GatewaysByZoneAsync(IEnumerable<string> zoneIds, CancellationToken cancellationToken)
    {
        var ids = zoneIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => new List<(string, string)>());
        if (ids.Count == 0)
        {
            return result;
        }

        var filter = Builders<BsonDocument>.Filter.In("zone_id", ids)
            & Builders<BsonDocument>.Filter.Eq("kind", "device")
            & Builders<BsonDocument>.Filter.Ne("is_deleted", true);
        var plots = await database.GetCollection<BsonDocument>(Plots).Find(filter).ToListAsync(cancellationToken);
        foreach (var plot in plots)
        {
            var zoneId = Text(plot, "zone_id");
            var deviceId = Text(plot, "item_id");
            if (zoneId is not null && deviceId is not null && result.TryGetValue(zoneId, out var list))
            {
                list.Add((deviceId.ToLowerInvariant(), deviceId));
            }
        }

        return result;
    }

    private static MapZoneRef ToRef(BsonDocument zone)
    {
        var location = zone.TryGetValue("location", out var l) && l.IsBsonDocument ? l.AsBsonDocument : null;
        var name = Text(zone, "name") ?? Text(zone, "code") ?? "Zone";
        var path = location is null ? null : Text(location, "location_path");
        var floor = location is null ? null : Text(location, "floor_name");
        return new MapZoneRef(
            Text(zone, "zone_id")!,
            Text(zone, "floor_plan_id") ?? string.Empty,
            name,
            Text(zone, "code") ?? string.Empty,
            floor is null ? name : $"{name} · {floor}",
            location is null ? null : Text(location, "site_id"),
            location is null ? null : Text(location, "site_name"),
            path);
    }

    private static string? Text(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString) ? value.AsString : null;
}
