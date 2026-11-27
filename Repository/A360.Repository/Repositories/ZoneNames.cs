using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Repository.Repositories;

/// <summary>
/// One source of truth for a zone's display name: Master Data (master_zones.name), looked up by zone ID. Every reader resolves the
/// CURRENT name here at read time and keeps the stored copy (zone_name on plots, floor-plan zones, TagLocationState, ...) only as a
/// fallback. Keys: the MDM zone id, the floor-plan zone's own id (maps_zones.zone_id, the "polygon id" the worker and plots carry)
/// and its Mongo _id; the last two are linked to MDM through maps_zones.location.zone_id. The map is cached for
/// <see cref="CacheSeconds"/> seconds per process, so an MDM rename shows up on every page within that time (plus the page's own refresh).
/// </summary>
public sealed class ZoneNames
{
    public const int CacheSeconds = 30;

    private static readonly object Gate = new();
    private static ZoneNames? _cached;
    private static DateTime _loadedAt;

    private readonly Dictionary<string, string> _byKey;
    private ZoneNames(Dictionary<string, string> byKey) => _byKey = byKey;

    public static readonly ZoneNames Empty = new([]);

    /// <summary>Current MDM name for a zone id (MDM id or floor-plan zone id), else <paramref name="stored"/> (the old copy).</summary>
    public string? Resolve(string? zoneId, string? stored) =>
        !string.IsNullOrWhiteSpace(zoneId) && _byKey.TryGetValue(zoneId.Trim(), out var name) ? name : stored;

    /// <summary>
    /// Alert / rule geofence label for an indoor zone: stored "Name · Floor" (id "zone:&lt;floor-plan zone id&gt;") with the current MDM
    /// name; any other geofence (outdoor) keeps its stored name.
    /// </summary>
    public string? ResolveGeofence(string? geofenceId, string? stored)
    {
        const string prefix = "zone:";
        if (geofenceId is null || !geofenceId.StartsWith(prefix, StringComparison.Ordinal) || !TryGet(geofenceId[prefix.Length..], out var name)) return stored;
        var cut = stored?.IndexOf(" · ", StringComparison.Ordinal) ?? -1;
        return cut >= 0 ? name + stored![cut..] : name;
    }

    /// <summary>Current MDM zone name plus the stored location path with its trailing (old) zone name swapped for it.</summary>
    public (string? Name, string? Path) ResolveWithPath(string? zoneId, string? storedName, string? storedPath)
    {
        var name = Resolve(zoneId, storedName);
        if (!string.IsNullOrEmpty(storedPath) && !string.IsNullOrEmpty(storedName) && name is not null && name != storedName
            && storedPath.EndsWith(storedName, StringComparison.Ordinal))
        {
            return (name, storedPath[..^storedName.Length] + name);
        }

        return (name, storedPath);
    }

    public bool TryGet(string? zoneId, out string name)
    {
        name = string.Empty;
        return !string.IsNullOrWhiteSpace(zoneId) && _byKey.TryGetValue(zoneId.Trim(), out name!);
    }

    /// <summary>Drop this process's cache (the rename endpoint); other services refresh within <see cref="CacheSeconds"/>.</summary>
    public static void Invalidate() { lock (Gate) { _cached = null; } }

    public static async Task<ZoneNames> GetAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        lock (Gate)
        {
            if (_cached is not null && DateTime.UtcNow - _loadedAt < TimeSpan.FromSeconds(CacheSeconds)) return _cached;
        }

        try
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var live = Builders<BsonDocument>.Filter.Ne("is_deleted", true);
            var master = await database.GetCollection<BsonDocument>("master_zones").Find(live)
                .Project(new BsonDocument { { "name", 1 } }).ToListAsync(ct);
            foreach (var z in master)
            {
                if (z.GetValue("name", BsonNull.Value) is { IsString: true } n && n.AsString.Length > 0) map[z["_id"].ToString()!] = n.AsString;
            }

            var drawn = await database.GetCollection<BsonDocument>("maps_zones").Find(live)
                .Project(new BsonDocument { { "zone_id", 1 }, { "location.zone_id", 1 } }).ToListAsync(ct);
            foreach (var z in drawn)
            {
                if (z.GetValue("location", BsonNull.Value) is not { IsBsonDocument: true } loc) continue;
                if (loc.AsBsonDocument.GetValue("zone_id", BsonNull.Value) is not { IsString: true } mid || !map.TryGetValue(mid.AsString, out var name)) continue;
                map[z["_id"].ToString()!] = name;
                if (z.GetValue("zone_id", BsonNull.Value) is { IsString: true } pid && pid.AsString.Length > 0) map[pid.AsString] = name;
            }

            var result = new ZoneNames(map);
            lock (Gate) { _cached = result; _loadedAt = DateTime.UtcNow; }
            return result;
        }
        catch (Exception) when (_cached is not null)
        {
            return _cached; // keep the last good map if Mongo hiccups
        }
    }
}
