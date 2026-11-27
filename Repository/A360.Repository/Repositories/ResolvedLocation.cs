using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Repository.Repositories;

/// <summary>A tag's resolved device / zone, written by WorkerNetworkService (collection TagLocationState).</summary>
/// <param name="UpdatedAt">Window time the tag was last heard by any device.</param>
/// <param name="Since">When the tag entered the current zone (zone_in).</param>
public sealed record ResolvedTagLocation(
    string TagId, string? DeviceId, string? ZoneId, string? ZoneName, string? FloorPlanId,
    DateTime? Since, DateTime UpdatedAt, double? Rssi, double? Battery, string? Status)
{
    public bool HasLocation => DeviceId is not null;
}

/// <summary>
/// Reads the zone the worker resolved per tag (median RSSI + hysteresis). Location:ResolverEnabled=false (same switch as the
/// worker) tells the callers to use their old "newest reading" logic instead. A state not updated within the presence window
/// (Location:StaleSeconds, else Alerts:PresenceSeconds, else 60, min 15 - same rule as the alert engine) is Stale: the
/// tag has no live location.
/// </summary>
public sealed class ResolvedLocationReader(IMongoDatabase database, IConfiguration configuration)
{
    public const string CollectionName = "TagLocationState";

    public bool Enabled => !string.Equals(configuration["Location:ResolverEnabled"], "false", StringComparison.OrdinalIgnoreCase);

    public TimeSpan StaleAfter
    {
        get
        {
            var raw = configuration["Location:StaleSeconds"] ?? configuration["Alerts:PresenceSeconds"];
            var seconds = int.TryParse(raw, out var s) ? s : 60;
            return TimeSpan.FromSeconds(Math.Max(15, seconds));
        }
    }

    public bool IsStale(ResolvedTagLocation state, DateTime now) => now - state.UpdatedAt > StaleAfter;

    /// <summary>All tags (lower-case id) updated at or after <paramref name="since"/> (null = all).</summary>
    public async Task<Dictionary<string, ResolvedTagLocation>> GetAsync(DateTime? since, CancellationToken ct)
    {
        var filter = since is { } s
            ? new BsonDocument("updated_at", new BsonDocument("$gte", s))
            : new BsonDocument();
        var rows = await database.GetCollection<BsonDocument>(CollectionName).Find(filter).ToListAsync(ct);
        var zoneNames = await ZoneNames.GetAsync(database, ct);
        var result = new Dictionary<string, ResolvedTagLocation>(StringComparer.Ordinal);
        foreach (var d in rows)
        {
            if (!d["_id"].IsString || !d.GetValue("updated_at", BsonNull.Value).IsValidDateTime) continue;
            result[d["_id"].AsString.ToLowerInvariant()] = new ResolvedTagLocation(
                d["_id"].AsString.ToLowerInvariant(), Str(d, "device_id"), Str(d, "zone_id"), zoneNames.Resolve(Str(d, "zone_id"), Str(d, "zone_name")), Str(d, "floor_plan_id"),
                d.GetValue("since", BsonNull.Value).IsValidDateTime ? d["since"].ToUniversalTime() : null,
                d["updated_at"].ToUniversalTime(), Num(d, "rssi"), Num(d, "battery"), Str(d, "status"));
        }
        return result;
    }

    private static string? Str(BsonDocument d, string n) => d.GetValue(n, BsonNull.Value) is { IsString: true } v && v.AsString.Length > 0 ? v.AsString : null;
    private static double? Num(BsonDocument d, string n) => d.GetValue(n, BsonNull.Value) is { IsNumeric: true } v ? v.ToDouble() : null;
}
