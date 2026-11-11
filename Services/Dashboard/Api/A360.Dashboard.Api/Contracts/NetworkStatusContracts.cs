using A360.Dashboard.Repository.Repositories;

namespace A360.Dashboard.Api.Contracts;

public sealed record NetworkStatusCountResponse(
    long TotalOnline,
    long TotalOffline)
{
    public static NetworkStatusCountResponse FromCounts(NetworkStatusCounts counts)
    {
        return new NetworkStatusCountResponse(counts.Online, counts.Offline);
    }
}

/// <summary>
/// GET /api/dashboards/network-status/assets/{id} (and each message of its /live WebSocket): the asset and the latest
/// NetworkTimeSeries reading of each of its tags.
/// </summary>
public sealed record AssetTelemetryResponse(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    IReadOnlyList<TagTelemetryResponse> Tags)
{
    /// <summary>A tag with no reading for this long counts as Idle (same rule as the Asset dashboard).</summary>
    public const int IdleAfterMinutes = 10;

    public static AssetTelemetryResponse FromTelemetry(AssetTagTelemetry telemetry, DateTime utcNow) => new(
        telemetry.AssetRegistrationId,
        telemetry.AssetTag,
        telemetry.AssetName,
        telemetry.Tags.Select(tag => TagTelemetryResponse.FromReading(tag, utcNow)).ToList());
}

/// <param name="Status">The status on the newest reading, as stored (e.g. "online").</param>
/// <param name="Connection">Online (newest reading online, within 10 min), Offline (newest reading not online),
/// Idle (no reading for 10 min) or No data (never reported).</param>
/// <param name="Timestamp">Time of the newest reading (UTC).</param>
public sealed record TagTelemetryResponse(
    string TagId,
    bool IsAssigned,
    string? GatewayId,
    DateTime? Timestamp,
    string? Status,
    string Connection,
    double? Battery,
    double? Rssi)
{
    public static TagTelemetryResponse FromReading(TagLatestReading reading, DateTime utcNow)
    {
        var connection = reading.Timestamp is not { } time ? "No data"
            : !string.Equals(reading.Status, "online", StringComparison.OrdinalIgnoreCase) ? "Offline"
            : utcNow - time > TimeSpan.FromMinutes(AssetTelemetryResponse.IdleAfterMinutes) ? "Idle"
            : "Online";

        return new TagTelemetryResponse(reading.TagId, reading.IsAssigned, reading.GatewayId, reading.Timestamp, reading.Status, connection,
            reading.Battery, reading.Rssi);
    }
}

/// <summary>
/// GET /api/dashboards/network-status/gateways (and each message of its /live WebSocket): one gateway, the assets
/// whose tags it currently hears and those tags.
/// </summary>
/// <param name="AssetCount">Distinct assets linked to the tags heard by this gateway (the count on the gateway's map icon).</param>
/// <param name="TagCount">Tags heard by this gateway, linked to an asset or not.</param>
/// <param name="LastSeenAt">Time of the newest reading that came through this gateway (any tag).</param>
public sealed record GatewayAssetsResponse(
    string GatewayId,
    int AssetCount,
    int TagCount,
    DateTime? LastSeenAt,
    IReadOnlyList<GatewayAssetResponse> Assets,
    IReadOnlyList<string> UnlinkedTagIds)
{
    public static GatewayAssetsResponse FromGateway(GatewayTags gateway)
    {
        var assets = gateway.Tags
            .Where(tag => tag.AssetId is not null)
            .GroupBy(tag => tag.AssetId!, StringComparer.OrdinalIgnoreCase)
            .Select(asset => new GatewayAssetResponse(
                asset.First().AssetId!,
                asset.Select(tag => tag.AssetName).FirstOrDefault(name => name is not null),
                asset.Select(tag => tag.TagId).ToList(),
                asset.Max(tag => tag.LastSeenAt)))
            .ToList();

        return new GatewayAssetsResponse(
            gateway.GatewayId,
            assets.Count,
            gateway.Tags.Count,
            gateway.Tags.Max(tag => tag.LastSeenAt),
            assets,
            gateway.Tags.Where(tag => tag.AssetId is null).Select(tag => tag.TagId).ToList());
    }
}

/// <param name="AssetId">Asset tag (e.g. TB123) or registration id, as stored on the tag link.</param>
public sealed record GatewayAssetResponse(string AssetId, string? AssetName, IReadOnlyList<string> TagIds, DateTime? LastSeenAt);

public sealed record TagRecordCountResponse(
    string TagId,
    long Count,
    string? AssetId,
    string? AssetName,
    string? AssetRegistrationId)
{
    public static TagRecordCountResponse FromTagRecordCount(TagRecordCount tag)
    {
        return new TagRecordCountResponse(tag.TagId, tag.Count, tag.AssetId, tag.AssetName, tag.AssetRegistrationId);
    }
}
