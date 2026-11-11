namespace A360.Dashboard.Repository.Repositories;

public interface INetworkStatusRepository
{
    Task<NetworkStatusCounts> GetStatusCountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TagRecordCount>> GetTagRecordCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The asset (by registration id or asset tag) and the newest NetworkTimeSeries reading of each of its tags;
    /// null when no asset matches and nothing reports under that id.
    /// </summary>
    Task<AssetTagTelemetry?> GetAssetTagTelemetryAsync(string assetRegistrationIdOrTag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every gateway (meta.gatewayid) with the tags whose newest NetworkTimeSeries reading (last 24 h) came through it, and the asset
    /// each tag is linked to (Tag Management assignment, or an asset whose RFID / NFC / barcode / tag is the tag id).
    /// </summary>
    Task<IReadOnlyList<GatewayTags>> GetGatewayTagsAsync(CancellationToken cancellationToken = default);
}

/// <summary>A gateway and the tags it currently hears (by each tag's newest reading).</summary>
public sealed record GatewayTags(string GatewayId, IReadOnlyList<GatewayTag> Tags);

/// <param name="AssetId">The linked asset as stored on the link (asset tag or registration id); null when the tag is not linked.</param>
public sealed record GatewayTag(string TagId, string? AssetId, string? AssetName, DateTime? LastSeenAt);

public sealed record NetworkStatusCounts(long Online, long Offline);

/// <summary>
/// <see cref="AssetId"/> is the id stored in the legacy assets mapping; <see cref="AssetRegistrationId"/> is the
/// registered asset (e.g. AR000006) the tag belongs to, or null when the tag is not linked to one.
/// </summary>
public sealed record TagRecordCount(string TagId, long Count, string? AssetId, string? AssetName, string? AssetRegistrationId);

/// <summary>A registered asset and the latest reading of every tag that identifies it.</summary>
public sealed record AssetTagTelemetry(string AssetRegistrationId, string? AssetTag, string? AssetName, IReadOnlyList<TagLatestReading> Tags);

/// <summary>
/// The newest NetworkTimeSeries reading of one tag (meta.tagid). <see cref="IsAssigned"/>: assigned to the asset in Tag
/// Management (TrackingTag); otherwise it is one of the asset's own identifiers (RFID / NFC / barcode / asset tag).
/// Reading fields are null when the tag has not reported yet.
/// </summary>
public sealed record TagLatestReading(
    string TagId,
    bool IsAssigned,
    string? GatewayId,
    DateTime? Timestamp,
    string? Status,
    double? Battery,
    double? Rssi);
