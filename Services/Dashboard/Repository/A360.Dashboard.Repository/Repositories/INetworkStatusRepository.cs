namespace A360.Dashboard.Repository.Repositories;

public interface INetworkStatusRepository
{
    Task<NetworkStatusCounts> GetStatusCountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TagRecordCount>> GetTagRecordCountsAsync(CancellationToken cancellationToken = default);
}

public sealed record NetworkStatusCounts(long Online, long Offline);

/// <summary>
/// <see cref="AssetId"/> is the id stored in the legacy assets mapping; <see cref="AssetRegistrationId"/> is the
/// registered asset (e.g. AR000006) the tag belongs to, or null when the tag is not linked to one.
/// </summary>
public sealed record TagRecordCount(string TagId, long Count, string? AssetId, string? AssetName, string? AssetRegistrationId);
