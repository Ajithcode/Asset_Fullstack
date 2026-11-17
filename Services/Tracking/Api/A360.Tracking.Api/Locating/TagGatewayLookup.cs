using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Locating;

public record TagGateway(string TagId, string GatewayId, DateTime LastSeen, int? Rssi = null);

/// <summary>
/// Finds the gateway to send a tag's commands through: of the NetworkTimeSeries readings of that tag within
/// {Locating:GatewayWindowSeconds} of its newest reading, the one with the strongest RSSI (several gateways usually
/// hear the same tag at the same moment).
/// </summary>
public class TagGatewayLookup(IMongoDatabase database, IOptions<LocatingSettings> options)
{
    private readonly IMongoCollection<BsonDocument> _collection =
        database.GetCollection<BsonDocument>(options.Value.TimeSeriesCollection);

    public async Task<TagGateway?> FindLatestAsync(string tagId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("meta.tagid", tagId),
            Builders<BsonDocument>.Filter.Exists("meta.gatewayid"));

        var docs = await _collection.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("timestamp"))
            .Project(Builders<BsonDocument>.Projection.Include("meta").Include("timestamp").Include("rssi"))
            .Limit(50)
            .ToListAsync(ct);

        var readings = docs
            .Select(d => (
                Gateway: d["meta"].AsBsonDocument.GetValue("gatewayid", BsonNull.Value),
                Time: d.GetValue("timestamp", BsonNull.Value),
                Rssi: d.GetValue("rssi", BsonNull.Value)))
            .Where(r => r.Gateway is { IsString: true } && !string.IsNullOrWhiteSpace(r.Gateway.AsString) && r.Time.IsValidDateTime)
            .Select(r => (Gateway: r.Gateway.AsString, Time: r.Time.ToUniversalTime(),
                Rssi: r.Rssi.IsNumeric ? (int?)r.Rssi.ToInt32() : null))
            .ToList();
        if (readings.Count == 0)
            return null;

        var newest = readings[0].Time;
        var windowStart = newest - TimeSpan.FromSeconds(options.Value.GatewayWindowSeconds);
        var best = readings
            .Where(r => r.Time >= windowStart)
            .OrderByDescending(r => r.Rssi ?? int.MinValue)
            .ThenByDescending(r => r.Time)
            .First();
        return new TagGateway(tagId, best.Gateway, best.Time, best.Rssi);
    }

    // "C3:00:00:99:13:8F" -> "c3000099138f", the format tag ids are stored in.
    public static string NormalizeId(string id) => id.Replace(":", "").Replace("-", "").Trim().ToLowerInvariant();
}
