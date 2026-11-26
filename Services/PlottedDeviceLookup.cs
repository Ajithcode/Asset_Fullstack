using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

public record PlottedDevice(string DeviceId, string ZoneId, string? ZoneName, string? FloorPlanId);

/// <summary>
/// Plotted devices (maps_plots, kind=device, not deleted) that sit in a zone, by lower-case device id.
/// Same plot rule as the tracking API (device_id, falling back to item_id; zone_id, falling back to location.zone_id;
/// first plot wins). Devices without a zone are left out: they can't win a zone. Cached for PlotCacheSeconds.
/// </summary>
public class PlottedDeviceLookup(IMongoDatabase database, IOptions<LocationSettings> options, ILogger<PlottedDeviceLookup> logger)
{
    private Dictionary<string, PlottedDevice> _devices = new();
    private HashSet<string> _allPlotted = new();
    private DateTime _loadedAt = DateTime.MinValue;

    public async Task<(IReadOnlyDictionary<string, PlottedDevice> InZone, IReadOnlySet<string> AllPlotted)> GetAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow - _loadedAt > TimeSpan.FromSeconds(options.Value.PlotCacheSeconds))
        {
            try
            {
                var rows = await database.GetCollection<BsonDocument>("maps_plots")
                    .Find(new BsonDocument { { "kind", "device" }, { "is_deleted", new BsonDocument("$ne", true) } })
                    .ToListAsync(ct);
                var devices = new Dictionary<string, PlottedDevice>();
                var all = new HashSet<string>();
                foreach (var r in rows)
                {
                    var id = Norm(Str(r, "device_id") ?? Str(r, "item_id"));
                    if (id is null) continue;
                    all.Add(id);
                    var zone = Str(r, "zone_id") ?? Str(r, "location", "zone_id");
                    if (zone is null || devices.ContainsKey(id)) continue;
                    devices[id] = new PlottedDevice(id, zone, Str(r, "zone_name") ?? Str(r, "location", "zone_name"), Str(r, "floor_plan_id"));
                }
                _devices = devices;
                _allPlotted = all;
                _loadedAt = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not reload maps_plots; using the previous list ({Count})", _devices.Count);
                _loadedAt = DateTime.UtcNow.AddSeconds(-options.Value.PlotCacheSeconds + 5);
            }
        }
        return (_devices, _allPlotted);
    }

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();

    private static string? Str(BsonDocument d, string a, string? b = null)
    {
        var v = d.GetValue(a, BsonNull.Value);
        if (b != null) v = v.IsBsonDocument ? v.AsBsonDocument.GetValue(b, BsonNull.Value) : BsonNull.Value;
        return v.IsString && !string.IsNullOrWhiteSpace(v.AsString) ? v.AsString : null;
    }
}
