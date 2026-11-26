using A360.Repository.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Repository.Repositories;

/// <summary>
/// The latest reading of a device in the NetworkTimeSeries collection, written by the gateways: whether the device
/// reported itself online or offline, its battery (%) and signal (RSSI, dBm), and the gateway that relayed it.
/// </summary>
public sealed record NetworkReading(
    string TagId,
    string? GatewayId,
    DateTime Timestamp,
    string? Status,
    int? Battery,
    int? Rssi,
    double? Latitude,
    double? Longitude);

public interface INetworkReadingRepository
{
    /// <summary>
    /// The latest reading of each of the given devices, keyed by <see cref="NetworkReadingKeys.Normalize"/> of the device id.
    /// A device is matched on meta.tagid; a gateway that never reports as a tag is matched on meta.gatewayid,
    /// using the latest reading it relayed.
    /// </summary>
    Task<IReadOnlyDictionary<string, NetworkReading>> GetLatestAsync(
        IEnumerable<string> deviceIds,
        CancellationToken cancellationToken = default);
}

public static class NetworkReadingKeys
{
    /// <summary>Gateways write hardware ids in lower case without separators (c3000071fe4c), so AC:23:3F… is matched too.</summary>
    public static string Normalize(string id)
    {
        return id.Trim().Replace(":", string.Empty).ToLowerInvariant();
    }

    /// <summary>The spellings of an id that may be stored in meta.tagid / meta.gatewayid.</summary>
    public static IEnumerable<string> Variants(string id)
    {
        var trimmed = id.Trim();
        var bare = trimmed.Replace(":", string.Empty);
        return new[] { trimmed, trimmed.ToLowerInvariant(), trimmed.ToUpperInvariant(), bare, bare.ToLowerInvariant(), bare.ToUpperInvariant() }
            .Distinct(StringComparer.Ordinal);
    }
}

public sealed class NetworkReadingRepository(IMongoDatabase database) : INetworkReadingRepository
{
    public const string CollectionName = "NetworkTimeSeries";

    private IMongoCollection<BsonDocument> Collection => database.GetCollection<BsonDocument>(CollectionName);

    public async Task<IReadOnlyDictionary<string, NetworkReading>> GetLatestAsync(
        IEnumerable<string> deviceIds,
        CancellationToken cancellationToken = default)
    {
        var ids = deviceIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        var latest = new Dictionary<string, NetworkReading>(StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return latest;
        }

        var variants = new BsonArray(ids.SelectMany(NetworkReadingKeys.Variants).Distinct());

        foreach (var reading in await LatestByAsync("meta.tagid", variants, cancellationToken))
        {
            latest[NetworkReadingKeys.Normalize(reading.TagId)] = reading;
        }

        // Gateways / repeaters relay readings rather than report as a tag: the shared device-status rule (A360.Repository
        // DeviceStatusReader) gives when they were last heard (own readings, or a repeater's beacon heard by a gateway).
        // This list only knows online / offline, so Idle (heard within 10 min) counts as online here.
        var status = await new DeviceStatusReader(database).GetAsync(ids, cancellationToken);
        foreach (var (deviceId, device) in status)
        {
            if (device.LastSeen is { } seen)
            {
                latest.TryAdd(deviceId, new NetworkReading(deviceId, null, seen, device.Status == "Offline" ? "offline" : "online", null, null, null, null));
            }
        }

        return latest;
    }

    private async Task<IReadOnlyList<NetworkReading>> LatestByAsync(string field, BsonArray values, CancellationToken cancellationToken)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument { { field, new BsonDocument("$in", values) }, { "device_trigger", new BsonDocument("$ne", "out") } }),
            new BsonDocument("$sort", new BsonDocument("timestamp", -1)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$toLower", "$" + field) },
                { "doc", new BsonDocument("$first", "$$ROOT") }
            })
        };

        var rows = await Collection.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken).ToListAsync(cancellationToken);
        return rows.Select(row => ToReading(row["doc"].AsBsonDocument)).ToList();
    }

    private static NetworkReading ToReading(BsonDocument document)
    {
        var meta = document.TryGetValue("meta", out var metaValue) && metaValue.IsBsonDocument ? metaValue.AsBsonDocument : new BsonDocument();
        return new NetworkReading(
            Text(meta, "tagid") ?? string.Empty,
            Text(meta, "gatewayid"),
            document.TryGetValue("timestamp", out var time) && time.IsValidDateTime ? time.ToUniversalTime() : DateTime.MinValue,
            Text(document, "status"),
            Int(document, "battery"),
            Int(document, "rssi"),
            Double(document, "latitude"),
            Double(document, "longitude"));
    }

    private static string? Text(BsonDocument document, string field)
    {
        return document.TryGetValue(field, out var value) && value.IsString ? value.AsString : null;
    }

    private static int? Int(BsonDocument document, string field)
    {
        return document.TryGetValue(field, out var value) && value.IsNumeric ? (int)Math.Round(value.ToDouble()) : null;
    }

    private static double? Double(BsonDocument document, string field)
    {
        return document.TryGetValue(field, out var value) && value.IsNumeric ? value.ToDouble() : null;
    }
}
